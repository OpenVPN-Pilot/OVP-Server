using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using OpenVpnPilot.Server.Configuration;
using OpenVpnPilot.Server.Contracts;
using OpenVpnPilot.Server.Data;
using OpenVpnPilot.Server.Middleware;
using OpenVpnPilot.Server.OpenApi;

namespace OpenVpnPilot.Server.Extensions;

public static class ApiRegistration
{
    public static IServiceCollection AddPilotApi(this IServiceCollection services, ServerOptions options)
    {
        services
            .AddControllers()
            .ConfigureApiBehaviorOptions(api => api.InvalidModelStateResponseFactory = InvalidModel);

        services.AddHealthChecks().AddDbContextCheck<PilotServerDbContext>("database", tags: ["ready"]);

        if (options.Tls.Mode == TlsMode.Proxy)
        {
            services.Configure<ForwardedHeadersOptions>(forwarded =>
            {
                forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                forwarded.KnownIPNetworks.Clear();
                forwarded.KnownProxies.Clear();
                foreach (System.Net.IPNetwork network in options.Tls.TrustedProxies)
                {
                    forwarded.KnownIPNetworks.Add(network);
                }
            });
        }

        if (options.Api.SwaggerEnabled)
        {
            services.AddPilotSwagger();
        }

        return services;
    }

    // Binding and validation failures use the same problem shape and code as every other refusal.
    private static ObjectResult InvalidModel(ActionContext context)
    {
        Dictionary<string, string[]> errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors
                    .Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "The value is not valid." : e.ErrorMessage)
                    .ToArray());

        HttpContext http = context.HttpContext;
        ILogger logger = http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ApiRegistration));
        string fields = string.Join("; ", errors.Keys);
        PipelineLog.Refused(logger, http.Request.Method, http.Request.Path, StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed, fields);

        return new ObjectResult(ProblemResponses.Create(
            http, StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed, "The request is not valid.", errors))
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { ProblemResponses.ContentType },
        };
    }
}
