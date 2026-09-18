using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenVpnPilot.Server.Configuration;
using OpenVpnPilot.Server.Middleware;
using OpenVpnPilot.Server.OpenApi;
using Serilog;
using Serilog.Events;

namespace OpenVpnPilot.Server.Extensions;

public static class PipelineSetup
{
    // The order is the design: an id first so everything is traceable, errors caught next so every
    // refusal has one shape, transport and headers checked before anyone is authenticated, and the
    // account checked after authentication and before any endpoint runs.
    public static WebApplication UsePilotPipeline(this WebApplication app, ServerOptions options)
    {
        if (options.Tls.Mode == TlsMode.Proxy)
        {
            app.UseForwardedHeaders();
        }

        app.UseMiddleware<RequestContextMiddleware>();
        app.UseSerilogRequestLogging(logging =>
        {
            logging.MessageTemplate = "{RequestMethod} {RequestPath} answered {StatusCode} in {Elapsed:0.0} ms";
            logging.GetLevel = LevelFor;
        });
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseMiddleware<HttpsRequirementMiddleware>();

        if (options.Api.SwaggerEnabled)
        {
            app.UsePilotSwagger();
        }

        app.UseMiddleware<PilotHeadersMiddleware>();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseMiddleware<AccountRevocationMiddleware>();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }).AllowAnonymous();
        return app;
    }

    // The container runtime asks for health every few seconds; those lines would drown everything else.
    private static LogEventLevel LevelFor(HttpContext context, double elapsed, Exception? exception) =>
        exception is not null || context.Response.StatusCode >= 500 ? LogEventLevel.Error
        : context.Request.Path.StartsWithSegments("/health") || context.Request.Path.StartsWithSegments("/swagger") ? LogEventLevel.Verbose
        : LogEventLevel.Information;
}
