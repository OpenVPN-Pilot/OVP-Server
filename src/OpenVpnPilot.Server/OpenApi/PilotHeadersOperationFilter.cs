using Microsoft.OpenApi;
using OpenVpnPilot.Server.Contracts;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OpenVpnPilot.Server.OpenApi;

// The mandatory headers are read by middleware, not bound by any action, so Swagger would not know
// about them. They are added to every operation that needs them.
public sealed class PilotHeadersOperationFilter : IOperationFilter
{
    private static readonly (string Name, string Description, string Example)[] Headers =
    [
        (PilotHeaders.ClientVersion, "The client's version. Older than the server's minimum is refused with 426.", "1.9.0"),
        (PilotHeaders.ApiVersion, "The API version the client speaks. Currently 1.", "1"),
        (PilotHeaders.ClientId, "A GUID the installation generates once and keeps. Tokens are bound to it.", "3f2c1b9e-8a4d-4f6b-9c1e-2d7a5b8c9e01"),
        (PilotHeaders.Platform, "windows, macos or linux.", "windows"),
        (PilotHeaders.Timestamp, "When the request was sent, ISO 8601 in UTC. Refused if off by more than the allowed skew.", "2026-01-31T12:00:00Z"),
    ];

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        string path = "/" + context.ApiDescription.RelativePath;
        if (path.StartsWith("/api/v1/server/info", StringComparison.OrdinalIgnoreCase))
        {
            operation.Security = [];
            return;
        }

        operation.Parameters ??= [];
        foreach ((string name, string description, string example) in Headers)
        {
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = name,
                In = ParameterLocation.Header,
                Required = true,
                Description = description + " Swagger UI fills it in when left empty.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
                Example = System.Text.Json.Nodes.JsonValue.Create(example),
            });
        }

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = PilotHeaders.RequestId,
            In = ParameterLocation.Header,
            Required = false,
            Description = "Optional id for this request, 8 to 64 letters, digits or dashes. Echoed back and written to every log line.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String },
        });

        bool anonymous = context.ApiDescription.ActionDescriptor.EndpointMetadata
            .OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>().Any();
        if (anonymous)
        {
            operation.Security = [];
        }
    }
}
