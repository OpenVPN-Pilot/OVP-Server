using Microsoft.AspNetCore.Mvc;
using OpenVpnPilot.Server.Contracts;

namespace OpenVpnPilot.Server.Middleware;

// Every refusal, wherever it comes from, leaves in the same shape: problem details with a stable code
// and the request id, so a client can branch on the code and an operator can find the log line.
public static class ProblemResponses
{
    public const string ContentType = "application/problem+json";

    public static ProblemDetails Create(
        HttpContext context, int status, string code, string detail, IReadOnlyDictionary<string, string[]>? errors = null)
    {
        ProblemDetails problem = new()
        {
            Type = "urn:openvpnpilot:error:" + code,
            Title = code,
            Status = status,
            Detail = detail,
            Instance = context.Request.Path,
        };
        problem.Extensions["code"] = code;
        problem.Extensions["requestId"] = context.TraceIdentifier;
        if (errors is not null)
        {
            problem.Extensions["errors"] = errors;
        }

        return problem;
    }

    public static Task WriteAsync(
        HttpContext context,
        int status,
        string code,
        string detail,
        bool wipe = false,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        if (context.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        context.Response.StatusCode = status;
        if (wipe)
        {
            context.Response.Headers[PilotHeaders.Directive] = PilotHeaders.WipeDirective;
        }

        return context.Response.WriteAsJsonAsync(
            Create(context, status, code, detail, errors), (System.Text.Json.JsonSerializerOptions?)null, ContentType);
    }
}
