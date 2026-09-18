using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OpenVpnPilot.Server.OpenApi;

// Validation attributes of a positional record have to sit on the constructor parameter, where MVC
// reads them, and Swagger only looks at properties. This carries them over, so the document shows the
// same limits the server enforces.
public sealed class RecordConstraintsSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema target || target.Properties is null)
        {
            return;
        }

        ConstructorInfo? constructor = context.Type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
        foreach (ParameterInfo parameter in constructor?.GetParameters() ?? [])
        {
            string name = JsonNamingPolicy.CamelCase.ConvertName(parameter.Name ?? string.Empty);
            if (!target.Properties.TryGetValue(name, out IOpenApiSchema? property) || property is not OpenApiSchema field)
            {
                continue;
            }

            foreach (ValidationAttribute attribute in parameter.GetCustomAttributes<ValidationAttribute>())
            {
                Apply(target, name, field, attribute);
            }
        }
    }

    private static void Apply(OpenApiSchema owner, string name, OpenApiSchema field, ValidationAttribute attribute)
    {
        bool isArray = field.Type?.HasFlag(JsonSchemaType.Array) == true;
        switch (attribute)
        {
            case RequiredAttribute:
                owner.Required ??= new HashSet<string>();
                owner.Required.Add(name);
                break;
            case MaxLengthAttribute max when isArray:
                field.MaxItems = max.Length;
                break;
            case MaxLengthAttribute max:
                field.MaxLength = max.Length;
                break;
            case MinLengthAttribute min when isArray:
                field.MinItems = min.Length;
                break;
            case MinLengthAttribute min:
                field.MinLength = min.Length;
                break;
            case RangeAttribute range:
                field.Minimum = Convert.ToString(range.Minimum, CultureInfo.InvariantCulture);
                field.Maximum = Convert.ToString(range.Maximum, CultureInfo.InvariantCulture);
                break;
            case RegularExpressionAttribute pattern:
                field.Pattern = pattern.Pattern;
                break;
        }
    }
}
