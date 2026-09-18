namespace OpenVpnPilot.Server.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            IHeaderDictionary headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";

            // Answers carry tokens, vault entries and configurations with private keys. No proxy, browser
            // or HTTP library cache may keep any of them.
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                headers.CacheControl = "no-store";
                headers.Pragma = "no-cache";
            }

            // Behind a proxy this reflects what the proxy received, which is HTTPS or was refused.
            if (context.Request.IsHttps)
            {
                headers.StrictTransportSecurity = "max-age=31536000";
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}
