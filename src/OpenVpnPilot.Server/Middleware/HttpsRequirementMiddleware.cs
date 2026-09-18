using OpenVpnPilot.Server.Contracts;

namespace OpenVpnPilot.Server.Middleware;

// Behind a reverse proxy the server itself listens in the clear, so it has to refuse anything the
// proxy did not receive over HTTPS. Passwords and vault entries cross this API in plain text inside TLS.
public sealed class HttpsRequirementMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        // Health checks carry nothing and are asked by the container runtime from inside the container.
        if (context.Request.IsHttps || context.Request.Path.StartsWithSegments("/health"))
        {
            return next(context);
        }

        return ProblemResponses.WriteAsync(
            context,
            StatusCodes.Status400BadRequest,
            ErrorCodes.HttpsRequired,
            "This server only answers over HTTPS.");
    }
}
