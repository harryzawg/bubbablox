using Microsoft.AspNetCore.Http.Extensions;

namespace Roblox.Website.Middleware;

public class RobloxPlayerCorsMiddleware
{
    private RequestDelegate _next;
    public RobloxPlayerCorsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    private string GenerateCspHeader(bool isAuthenticated)
    {
        var connectSrc = "'self' https://*.bbblox.org https://bbblox.org https://*.zawg.ca https://zawg.ca wss://*.localhost:90 https://hcaptcha.com https://*.hcaptcha.com https://*.cdn.com";
#if DEBUG
        connectSrc += " ws://localhost:*";
#endif

        // Images
        var imgSrc = "'self' data:";
        if (isAuthenticated)
        {
            imgSrc += "  https://*.cdn.bbblox.org";
        }
        
        // Scripts
        
        // unsafe-eval required by nextjs
        var scriptSrc =
            "'unsafe-eval' 'self' https://hcaptcha.com https://*.hcaptcha.com https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/js/bootstrap.bundle.min.js http://localhost:5000";
        
        return "default-src 'self'; img-src https://bbblox.org https://*.zawg.ca http://bbblox.org https://*.bbblox.org data:; child-src 'self'; script-src https://esm.sh "+scriptSrc+"; frame-src 'self' https://hcaptcha.com https://*.hcaptcha.com https://*.bbblox.org https://bbblox.org https://*.zawg.ca https://zawg.ca http://zawg.ca http://*.zawg.ca; style-src 'unsafe-inline' 'self' https://fonts.googleapis.com https://hcaptcha.com https://*.hcaptcha.com https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/css/bootstrap.min.css; font-src 'self' fonts.gstatic.com; connect-src "+connectSrc+"; worker-src 'self';";
    }
    
    public async Task InvokeAsync(HttpContext ctx)
    {
        var isAuthenticated = ctx.Items.ContainsKey(".ROBLOSECURITY");
        ctx.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
        ctx.Response.Headers["Cross-Origin-Resource-Policy"] = "cross-origin";
        ctx.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
        ctx.Response.Headers["X-XSS-Protection"] = "1; mode=block";
        ctx.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
        ctx.Response.Headers["Content-Security-Policy"] = GenerateCspHeader(isAuthenticated);
        await _next(ctx);
    }
}

public static class RobloxPlayerCorsMiddlewareExtensions
{
    public static IApplicationBuilder UseRobloxPlayerCorsMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RobloxPlayerCorsMiddleware>();
    }
}
