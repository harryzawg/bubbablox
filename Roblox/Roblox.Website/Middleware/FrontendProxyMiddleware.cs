using System.Net;
using System.Text;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Net.Http.Headers;
using Microsoft.Extensions.Caching.Memory;
using Roblox.Logging;
using Roblox.Models.Sessions;
using Roblox.Models.Users;
using Roblox.Services;
using Roblox.Website.Lib;
using ServiceProvider = Microsoft.Extensions.DependencyInjection.ServiceProvider;

namespace Roblox.Website.Middleware;

public class FrontendProxyMiddleware
{
    private RequestDelegate _next;
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly IMemoryCache _cache;

	public FrontendProxyMiddleware(RequestDelegate next, IHttpClientFactory httpClientFactory, IMemoryCache cache)
	{
		_next = next;
		_httpClientFactory = httpClientFactory;
		_cache = cache;
	}
	
    public static List<string> BypassUrls = new()
    {
        "/apisite/",
        "/api/",
        "/api/economy-chat/",
		"/api/thumbnail/",
        // Razor Files
        "/feeds/getuserfeed",
        "/auth/",
        "/membership/notapproved.aspx",
        // Razor Public
        "/unsecuredcontent/",
        // Razor - Internal
        "/internal/year",
        "/internal/updates",
        "/internal/clothingstealer",
        "/internal/age",
		"/internal/promocodes",
        "/internal/report-abuse",
        "/internal/membership",
        "/internal/apply",
        "/internal/invite",
        "/internal/dev",
        "/internal/faq",
        "/internal/donate",
        "/internal/place-update",
        "/internal/create-place",
        "/internal/migrate-to-application",
        "/internal/collectibles",
        "/internal/contest/first-contest",
        "/auth/notapproved",
        // Admin
        "/admin-api/api",
        "/admin",
        // Web
        "/thumbs/avatar.ashx",
        "/thumbs/avatar-headshot.ashx",
        "/thumbs/asset.ashx",
        "/user-sponsorship/",
        "/users/inventory/list-json",
        "/users/favorites/list-json",
        "/userads/redirect",
        "/users/profile/robloxcollections-json",
        "/asset/toggle-profile",
        "/comments/get-json",
        "/comments/post",
        "/usercheck/show-tos",
        "/search/users/results",
		"/images/thumbnails",
		"/images",
        // Web - Game
        "/game/get-join-script",
        "/game/placelauncher.ashx",
		"/game/placelauncherbt.ashx",
        "/placelauncher.ashx",
        "/game/join.ashx",
        "/game/validate-machine",
        "/game/validateticket.ashx",
        "/game/get-join-script-debug",
        "/games/getgameinstancesjson",
		"/game/gamepass/gamepasshandler.ashx",
		"/game/luawebservice/handlesocialrequest.ashx",
		"/UserCheck/checkifinvalidusernameforsignup",
		"/develop/upload-version",
		"/develop/upload-icon",
		"/develop/upload",
		"/game/EggHunt.ashx",
        // gs
        "/gs/activity",
        "/gs/ping",
        "/gs/delete",
        "/gs/shutdown",
        "/gs/players/report",
        "/api/moderation/filtertext",
        // hubs
        "/chat",
        "/chat/negotiate",
    };

	private async Task<HttpResponseMessage> ProxyRequestAsync(string url)
	{
		var client = _httpClientFactory.CreateClient("FrontendProxy");
		return await client.GetAsync(url);
	}

    public async Task HandleProxyResult(string url, string? contentType, int statusCode, string? locationHeader, HttpContext ctx)
    {
        var frontendTimer = new MiddlewareTimer(ctx, "FProxy");
        ctx.Response.ContentType = contentType ?? "text/html";
        ctx.Response.StatusCode = statusCode;
        // required for redirects
        if (locationHeader != null)
        {
            ctx.Response.Headers["location"] = locationHeader;
        }
        // cache _next stuff
#if RELEASE
        if (url.StartsWith("/_next/") && statusCode == 200)
        {
            ctx.Response.Headers.CacheControl = new CacheControlHeaderValue()
            {
                MaxAge = TimeSpan.FromDays(30),
                Public = true,
            }.ToString();
        }
#endif
        // tell cloudflare to STOP CACHING 404 ERRORS ON NEW NEXTJS FILES!!!!!
        if (statusCode == 404 || (statusCode > 499 && statusCode < 599))
        {
            ctx.Response.Headers.CacheControl = new CacheControlHeaderValue()
            {
                MaxAge = TimeSpan.Zero,
                Public = true,
                NoCache = true,
                MustRevalidate = true,
            }.ToString();
        }
        frontendTimer.Stop();
    }
	
	private class CacheEntry
	{
		public string ContentType { get; set; } = "text/html";
		public string Content { get; set; } = "";
		public string LocationHeader { get; set; } = "";
		public int StatusCode { get; set; }
	}

	// this is the worst shit fucking ever
	private static readonly TimeSpan cacheExpiration = TimeSpan.FromMinutes(30);
	
	private string FixDoubleSlashes(string url)
	{
		return System.Text.RegularExpressions.Regex.Replace(
			url, 
			@"(?<!http:|https:)/{2,}", 
			"/"
		);
	}
	
	// BOOOOO stupid fucking proxy. i hate it why does it break the whole ufkcing site?
	public async Task InvokeAsync(HttpContext ctx)
	{
		var requestUrl = ctx.Request.GetEncodedPathAndQuery();
		
		var fixedUrl = FixDoubleSlashes(requestUrl);
		if (fixedUrl != requestUrl)
		{
			var uri = new Uri(fixedUrl, UriKind.RelativeOrAbsolute);
			ctx.Request.Path = uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString;

			if (uri.IsAbsoluteUri && !string.IsNullOrEmpty(uri.Query))
			{
				ctx.Request.QueryString = new QueryString(uri.Query);
			}

			requestUrl = fixedUrl;
		}

		foreach (var item in BypassUrls)
		{
			if (requestUrl.ToLower().StartsWith(item))
			{
				await _next(ctx);
				return;
			}
		}

	#if RELEASE
		if (_cache.TryGetValue(requestUrl, out CacheEntry cached))
		{
			ctx.Response.Headers.Add("x-cache-dbg", "f-2016; memv3;");
			await HandleProxyResult(requestUrl, cached.ContentType, cached.StatusCode, cached.LocationHeader, ctx);
			await ctx.Response.WriteAsync(cached.Content);
			return;
		}
	#endif

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
		
		try
		{
			var client = _httpClientFactory.CreateClient("FrontendProxy");
			var result = await client.GetAsync(requestUrl, cts.Token);

			var contentType = result.Content.Headers.ContentType?.ToString();
			var locationHeader = result.Headers.Location?.ToString();
			var statusCode = (int)result.StatusCode;

			var cacheable = contentType != null && result.IsSuccessStatusCode && (
				contentType.Contains("application/javascript") ||
				contentType.Contains("text/html"));
				
			if (requestUrl.ToLower().StartsWith("/forum/"))
				cacheable = false;

	#if RELEASE
			if (cacheable)
			{
				var contentStr = await result.Content.ReadAsStringAsync(cts.Token);

				var entry = new CacheEntry
				{
					ContentType = contentType!,
					Content = contentStr,
					LocationHeader = locationHeader ?? string.Empty,
					StatusCode = statusCode
				};

				_cache.Set(requestUrl, entry, TimeSpan.FromMinutes(30));

				await HandleProxyResult(requestUrl, contentType, statusCode, locationHeader, ctx);
				await ctx.Response.WriteAsync(contentStr);
			}
			else
			{
				await HandleProxyResult(requestUrl, contentType, statusCode, locationHeader, ctx);
				await result.Content.CopyToAsync(ctx.Response.Body, cts.Token);
			}
	#else
			await HandleProxyResult(requestUrl, contentType, statusCode, locationHeader, ctx);
			await result.Content.CopyToAsync(ctx.Response.Body, cts.Token);
	#endif
		}
		catch (TaskCanceledException)
		{
			Console.WriteLine($"timeout for: {requestUrl}");
			ctx.Response.StatusCode = 504;
			await ctx.Response.WriteAsync("Timeout");
		}
		catch (HttpRequestException ex)
		{
			Console.WriteLine($"HTTP error for {requestUrl}: {ex.Message}");
			ctx.Response.StatusCode = 502;
			await ctx.Response.WriteAsync("Bad gateway");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"error for {requestUrl}: {ex.Message}");
			ctx.Response.StatusCode = 500;
			await ctx.Response.WriteAsync("Internal Server Error");
		}
	}
}