using System.Reflection;
using System.Text.RegularExpressions;
using OpenVpnPilot.Server.Contracts;
using Serilog.Context;

namespace OpenVpnPilot.Server.Middleware;

// Gives every request an id before anything else runs, so every log line it causes can be found by
// the id the client sees in the response.
public sealed partial class RequestContextMiddleware(RequestDelegate next)
{
    public static readonly string ServerVersion =
        typeof(RequestContextMiddleware).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0] ?? "0.0.0";

    public async Task InvokeAsync(HttpContext context)
    {
        string? offered = context.Request.Headers[PilotHeaders.RequestId];
        string requestId = offered is not null && SafeId().IsMatch(offered) ? offered : Guid.NewGuid().ToString();
        context.TraceIdentifier = requestId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[PilotHeaders.RequestId] = requestId;
            context.Response.Headers[PilotHeaders.ServerVersion] = ServerVersion;
            context.Response.Headers[PilotHeaders.ApiVersion] = PilotHeaders.CurrentApiVersion;
            return Task.CompletedTask;
        });

        // Not called RequestId: the framework puts its own connection based id under that name into every
        // scope, and it would replace this one on every line the framework logs.
        using (LogContext.PushProperty("PilotRequestId", requestId))
        {
            await next(context);
        }
    }

    // A client's id goes into logs and headers, so only a plain token is taken over.
    [GeneratedRegex("^[A-Za-z0-9-]{8,64}$")]
    private static partial Regex SafeId();
}
