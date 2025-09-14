using System.Diagnostics;
using Microsoft.AspNetCore.Http.Extensions;
using Roblox.Website.Lib;

namespace Roblox.Website.Middleware;

public class RobloxLoggingMiddleware
{
    private RequestDelegate _next;
    public RobloxLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }
    
    public async Task InvokeAsync(HttpContext ctx)
    {
        var watch = new Stopwatch();
        watch.Start();
        await _next(ctx);
        watch.Stop();

        var str = $"[{ctx.Request.Method.ToUpper()}] {ctx.Request.GetEncodedUrl()} - {watch.ElapsedMilliseconds}ms";
		// Should we comment this later? It always shows errors in console and the requests just flood it so you can't really see the errors
        Console.WriteLine(str);
/* 		var url = ctx.Request.GetEncodedUrl();
        if (url.Contains("persistence", StringComparison.OrdinalIgnoreCase) ||
			url.Contains("marketplace", StringComparison.OrdinalIgnoreCase) ||
			url.Contains("badge", StringComparison.OrdinalIgnoreCase))
        {
            var str = $"[{ctx.Request.Method.ToUpper()}] {url} - {watch.ElapsedMilliseconds}ms";
            Console.WriteLine(str);
        } */
    }
}

public static class RobloxLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRobloxLoggingMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RobloxLoggingMiddleware>();
    }
}