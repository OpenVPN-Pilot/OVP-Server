using System.Reflection;
using Microsoft.OpenApi;
using OpenVpnPilot.Server.Middleware;

namespace OpenVpnPilot.Server.OpenApi;

public static class SwaggerSetup
{
    public const string DocumentName = "v1";

    public static IServiceCollection AddPilotSwagger(this IServiceCollection services) =>
        services.AddSwaggerGen(swagger =>
        {
            swagger.SwaggerDoc(DocumentName, new OpenApiInfo
            {
                Title = "OpenVPN Pilot Server",
                Version = RequestContextMiddleware.ServerVersion,
                Description =
                    "The REST API behind OpenVPN Pilot's remote mode: shared profiles, a shared vault of sign ins, and each "
                    + "user's favourites, shortcuts and settings. Every endpoint except GET /api/v1/server/info needs the "
                    + "X-Pilot-* headers; every endpoint except server info, login, Entra exchange, refresh and logout needs "
                    + "a bearer token. Errors are RFC 9457 problem details with a stable 'code'. A response carrying "
                    + "'X-Pilot-Directive: wipe' tells the client to erase everything it holds from this server.",
                License = new OpenApiLicense { Name = "MIT", Url = new Uri("https://opensource.org/licenses/MIT") },
            });

            swagger.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "The access token from POST /api/v1/auth/login, /entra/exchange or /refresh.",
            });
            swagger.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
            });

            swagger.OperationFilter<PilotHeadersOperationFilter>();
            swagger.SupportNonNullableReferenceTypes();

            string xml = Path.Combine(AppContext.BaseDirectory, Assembly.GetExecutingAssembly().GetName().Name + ".xml");
            swagger.IncludeXmlComments(xml, includeControllerXmlComments: true);
        });

    public static WebApplication UsePilotSwagger(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(ui =>
        {
            ui.SwaggerEndpoint($"/swagger/{DocumentName}/swagger.json", "OpenVPN Pilot Server v1");
            ui.DocumentTitle = "OpenVPN Pilot Server API";

            // Trying a request by hand needs a fresh timestamp every time, which nobody types. The
            // interceptor fills in whatever mandatory header the form left empty.
            ui.UseRequestInterceptor(RequestInterceptor);
        });
        return app;
    }

    private const string RequestInterceptor = """
        (request) => {
          const h = request.headers;
          if (!request.url.includes('/api/') || request.url.includes('/api/v1/server/info')) { return request; }
          let id = localStorage.getItem('ovp-swagger-client-id');
          if (!id) { id = crypto.randomUUID(); localStorage.setItem('ovp-swagger-client-id', id); }
          h['X-Pilot-Timestamp'] = h['X-Pilot-Timestamp'] || new Date().toISOString();
          h['X-Pilot-Client-Id'] = h['X-Pilot-Client-Id'] || id;
          h['X-Pilot-Api-Version'] = h['X-Pilot-Api-Version'] || '1';
          h['X-Pilot-Platform'] = h['X-Pilot-Platform'] || 'windows';
          h['X-Pilot-Client-Version'] = h['X-Pilot-Client-Version'] || '99.0.0';
          return request;
        }
        """;
}
