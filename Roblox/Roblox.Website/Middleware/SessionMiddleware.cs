using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Net.Http;
using System.Collections.Concurrent;
using JWT;
using JWT.Algorithms;
using JWT.Exceptions;
using JWT.Serializers;
using Microsoft.AspNetCore.Http.Extensions;
using Roblox.Dto.Users;
using Roblox.Models.Sessions;
using Roblox.Models.Users;
using Roblox.Models.JWT;
using Roblox.Services;
using Roblox.Services.Exceptions;
using Roblox.Website.Controllers;
using Roblox.Website.Filters;
using Roblox.Website.Lib;
using Roblox.Libraries.Password;
using Roblox.Website.WebsiteModels.Session.IP;
using ServiceProvider = Roblox.Services.ServiceProvider;

namespace Roblox.Website.Middleware;

public class JwtEntry
{
    public string sessionId { get; set; }
    public long createdAt { get; set; }
}

public class SessionMiddleware
{
    private RequestDelegate _next;
    public const string CookieName = ".ROBLOSECURITY";
    // JWT Config
    private static readonly IJwtAlgorithm Algorithm = new HMACSHA512Algorithm();
    private static readonly IJsonSerializer Serializer = new JsonNetSerializer();
    private static readonly IBase64UrlEncoder UrlEncoder = new JwtBase64UrlEncoder();
    private static readonly IDateTimeProvider DateTimeProvider = new UtcDateTimeProvider();
    private static readonly IJwtValidator Validator = new JwtValidator(Serializer, DateTimeProvider);

    private static readonly IJwtEncoder Encoder = new JwtEncoder(Algorithm, Serializer, UrlEncoder);
    private static readonly IJwtDecoder Decoder = new JwtDecoder(Serializer, Validator, UrlEncoder, Algorithm);

    private static string cookieJwtKey { get; set; }

    public static void Configure(string newJwtKey)
    {
        if (cookieJwtKey != null) throw new Exception("Already configured");
        cookieJwtKey = newJwtKey;
    }


    public SessionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public static string CreateJwt<T>(T obj)
    {
        var token = Encoder.Encode(obj, cookieJwtKey);
        if (token == null) throw new NullReferenceException();
        return token;
    }

	public static T DecodeJwt<T>(string token)
	{
		try
		{
			var json = Decoder.Decode(token, cookieJwtKey, verify: true);
			if (json == null) throw new NullReferenceException("JWT is bad");
			
			var result = JsonSerializer.Deserialize<T>(json);
			if (result == null) throw new NullReferenceException("JWT is bad");
			
			return result;
		}
		catch (SignatureVerificationException ex)
		{
			throw new InvalidJWTSignature("Could not decode JWT signature", ex);
		}
		catch (NullReferenceException ex)
		{
			throw new InvalidJWTSignature("Could not decode JWT signature", ex);
		}
		catch (Exception ex) when (ex is ArgumentException or FormatException)
		{
			throw new InvalidJWTSignature("Could not decode JWT signature", ex);
		}
	}

    private async Task OnBadSession(HttpContext ctx)
    {
        ctx.Response.Cookies.Delete(CookieName);
        await _next(ctx);
    }
	
	private static readonly HttpClient http = new HttpClient();
    private static readonly ConcurrentDictionary<string, IPCacheEntry> IPCache = new();
    private static readonly TimeSpan IPCacheDuration = TimeSpan.FromMinutes(30);
	
	// is there a better way to do this? cause argon makes a random salt and i can't do anything about that
	private static string HashIp(string IP)
	{
		string salt = Roblox.Configuration.IPSalt;
		
		using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(salt));
		byte[] hashbytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(IP));
		
		return BitConverter.ToString(hashbytes).Replace("-", "").ToLower();
	}
	
	private async Task UpdateIPCacheAsync(string ip)
    {
        try
        {
            var blockstats = await GetIPBlockStats(ip);
            var hashedIP = HashIp(ip);

            var newcache = new IPCacheEntry 
            { 
                hashedIP = hashedIP,
                blockstats = blockstats,
                LastUpdated = DateTime.UtcNow 
            };

            IPCache.AddOrUpdate(ip, newcache, (_, _) => newcache);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"error updating IP cache for some reason: {ex.Message}");
        }
    }
	
	// TODO: Remove writeline stuff later cause it was just seeing how often 429s are served
	private async Task<int> GetIPBlockStats(string ip)
	{
		int retries = 2;
		int delay = 7500;
		
		for (int attempt = 1; attempt <= retries; attempt++)
		{
			try
			{
				//Console.WriteLine($"attempt {attempt} of getting IP stats");
				var request = new HttpRequestMessage(HttpMethod.Get, $"http://v2.api.iphub.info/ip/{ip}");
				request.Headers.Add("X-Key", Roblox.Configuration.IPHubApiKey);
				var response = await http.SendAsync(request);
				
				if ((int)response.StatusCode == 429)
				{
					//Console.WriteLine($"got 429 (attempt {attempt})");
					if (attempt < retries)
					{
						//Console.WriteLine($"waiting {delay}ms before retrying");
						await Task.Delay(delay);
						continue;
					}
				}
				
				response.EnsureSuccessStatusCode();
				
				var content = await response.Content.ReadAsStringAsync();
				var ipInfo = JsonSerializer.Deserialize<IPHubRes>(content);
				//Console.WriteLine($"got IP stats successfully");
				return ipInfo?.block ?? 0;
			}
			catch (HttpRequestException httpEx) when (httpEx.StatusCode == (System.Net.HttpStatusCode)429 && attempt < retries)
			{
				//Console.WriteLine($"429 on attempt {attempt}");
				//Console.WriteLine($"waiting {delay}ms before retrying");
				await Task.Delay(delay);
				continue;
			}
			catch (Exception ex)
			{
				//Console.WriteLine($"attempt {attempt} failed");
				if (attempt == retries)
				{
					//Console.WriteLine("max retries reached, returning 0");
					return 0;
				}
			}
		}
		
		Console.WriteLine("attempts completed without success, returning 0");
		return 0;
	}

    private async Task<string> GetHashedIP(string ip)
    {
        if (IPCache.TryGetValue(ip, out var cacheEntry))
        {
            if (DateTime.UtcNow - cacheEntry.LastUpdated > IPCacheDuration)
            {
                _ = Task.Run(() => UpdateIPCacheAsync(ip));
            }
            return cacheEntry.hashedIP;
        }

        // if not in cache just cache it and update
        var hashedIP = HashIp(ip);
        _ = Task.Run(() => UpdateIPCacheAsync(ip));
        return hashedIP;
    }
	
	public async Task InvokeAsync(HttpContext ctx)
	{
		var authTimer = new MiddlewareTimer(ctx, "au");
		var currentPath = ctx.Request.Path.ToString().ToLower();
		try
		{
			if (ctx.Request.Cookies.ContainsKey(CookieName))
			{
				var cookie = ctx.Request.Cookies[CookieName];
				if (string.IsNullOrWhiteSpace(cookie))
				{
					authTimer.Stop();
					await OnBadSession(ctx);
					return;
				}
				
				try
				{
					// PLEASE save the block status if the ip is the same, the api key gets exhausted like instantly
					var DecodedRes = DecodeJwt<JwtEntry>(cookie);
					if (!string.IsNullOrEmpty(DecodedRes.sessionId))
					{
						using var users = ServiceProvider.GetOrCreate<UsersService>();
						
						UserInfo userInfo;
                        try
                        {
                            var sessResult = await users.GetSessionById(DecodedRes.sessionId);
                            userInfo = await users.GetUserById(sessResult.userId);                         
							var IP = ControllerBase.GetRequesterIpRaw(ctx);
							var hashed = await GetHashedIP(IP);
							
							if (await users.IsUserPoisoned(hashed))
							{
								ctx.Response.StatusCode = 403;
								authTimer.Stop();
								return;
							}

							var cache = IPCache.TryGetValue(IP, out var entry) ? entry : null;
							var blockstats = cache?.blockstats ?? 0;

                            var hash = await users.GetUserHashedIp(userInfo.userId);
                            if (hash != hashed)
                            {
                                // do in background cause it likes to timeout
                                _ = Task.Run(async () => 
                                {
                                    try 
                                    {
                                        await users.UpdateUserHashedIp(userInfo.userId, hashed, blockstats);
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"error updating IP hash: {ex.Message}");
                                    }
                                });
                            }
                        }
						catch (RecordNotFoundException)
						{
							authTimer.Stop();
							await OnBadSession(ctx);
							return;
						}
						if (userInfo.accountStatus is AccountStatus.Forgotten or AccountStatus.MustValidateEmail)
						{
							authTimer.Stop();
							await OnBadSession(ctx);
							return;
						}

						ctx.Items[CookieName] = new UserSession(userInfo.userId, userInfo.username, userInfo.created, userInfo.accountStatus, 0, false, DecodedRes.sessionId);

						if (userInfo.accountStatus is AccountStatus.Suppressed or AccountStatus.Poisoned or AccountStatus.Deleted)
						{
							if (!currentPath.StartsWith("/auth/"))
							{
								authTimer.Stop();
								ctx.Response.StatusCode = 302;
								ctx.Response.Headers.Add("location", "/auth/notapproved");
								return;
							}
						}

						if (!currentPath.StartsWith("/thumbs/") && !currentPath.StartsWith("/images/"))
						{
							await users.EarnDailyRobuxNoVirusNoScamHindiSubtitles(userInfo.userId, await StaffFilter.IsStaff(userInfo.userId));
							await users.EarnDailyTickets(userInfo.userId);
							if (users.TrySetOnlineTimeUpdated(userInfo.userId))
							{
								await users.UpdateOnlineStatus(userInfo.userId);
							}
						}

						if (currentPath == "/")
						{
							ctx.Response.StatusCode = 302;
							ctx.Response.Headers.Add("location", "/home");
							return;
						}
						authTimer.Stop();
					}
				}
				catch (InvalidJWTSignature)
				{
					foreach (var cookieKey in ctx.Request.Cookies.Keys)
					{
						ctx.Response.Cookies.Delete(cookieKey);
					}
					authTimer.Stop();
					await OnBadSession(ctx);
					return;
				}
			}
		}
		catch (Exception e) when (e is InvalidTokenPartsException or NullReferenceException or FormatException)
		{
			ctx.Response.Cookies.Delete(CookieName);
		}
		await _next(ctx);
	}
}

public static class SessionMiddlewareExtensions
{
    public static IApplicationBuilder UseRobloxSessionMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SessionMiddleware>();
    }
}