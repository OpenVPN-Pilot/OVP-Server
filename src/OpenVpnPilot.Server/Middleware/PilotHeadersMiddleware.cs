using System.Globalization;
using OpenVpnPilot.Server.Configuration;
using OpenVpnPilot.Server.Contracts;
using OpenVpnPilot.Server.Services;
using Serilog;
using Serilog.Context;

namespace OpenVpnPilot.Server.Middleware;

// Every API call says which client it is, in which version, and when it was sent. That lets the server
// turn away clients it cannot serve before they misread an answer, bind tokens to one installation,
// and reject a request replayed long after it was made.
public sealed class PilotHeadersMiddleware(RequestDelegate next, ApiOptions options, TimeProvider time)
{
    // Asked before a client knows what the server expects, so it cannot be required to send headers.
    private static readonly PathString InfoPath = "/api/v1/server/info";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments(InfoPath))
        {
            await next(context);
            return;
        }

        ClientContext client = Read(context.Request.Headers);
        context.SetClientContext(client);

        // The summary line for the request is written after this scope has closed, so it gets them this way.
        IDiagnosticContext diagnostics = context.RequestServices.GetRequiredService<IDiagnosticContext>();
        diagnostics.Set("ClientId", client.ClientId);
        diagnostics.Set("ClientVersion", client.ClientVersionText);

        using (LogContext.PushProperty("ClientId", client.ClientId))
        using (LogContext.PushProperty("ClientVersion", client.ClientVersionText))
        {
            await next(context);
        }
    }

    private ClientContext Read(IHeaderDictionary headers)
    {
        string apiVersion = Required(headers, PilotHeaders.ApiVersion);
        if (apiVersion != PilotHeaders.CurrentApiVersion)
        {
            throw ServiceException.BadRequest(
                ErrorCodes.ApiVersionUnsupported, $"This server speaks API version {PilotHeaders.CurrentApiVersion}, not {apiVersion}.");
        }

        string versionText = Required(headers, PilotHeaders.ClientVersion);
        Version version = ParseVersion(versionText) ?? throw Invalid(PilotHeaders.ClientVersion, "a version such as 1.9.0");
        if (version < options.MinimumClientVersion)
        {
            throw new ServiceException(
                StatusCodes.Status426UpgradeRequired,
                ErrorCodes.ClientOutdated,
                $"This server needs client version {options.MinimumClientVersion} or newer; this is {versionText}.");
        }

        if (!Guid.TryParse(Required(headers, PilotHeaders.ClientId), out Guid clientId) || clientId == Guid.Empty)
        {
            throw Invalid(PilotHeaders.ClientId, "a GUID that identifies the installation");
        }

        string platform = Required(headers, PilotHeaders.Platform).ToLowerInvariant();
        if (!PilotHeaders.Platforms.Contains(platform))
        {
            throw Invalid(PilotHeaders.Platform, "one of " + string.Join(", ", PilotHeaders.Platforms));
        }

        if (!DateTimeOffset.TryParse(
                Required(headers, PilotHeaders.Timestamp),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out DateTimeOffset timestamp))
        {
            throw Invalid(PilotHeaders.Timestamp, "an ISO 8601 time such as 2026-01-31T12:00:00Z");
        }

        DateTimeOffset now = time.GetUtcNow();
        if ((now - timestamp).Duration() > options.ClockSkew)
        {
            throw ServiceException.BadRequest(
                ErrorCodes.ClockSkew,
                $"The request was sent at {timestamp:O}, but the server time is {now:O}. Check the clock of this machine.");
        }

        return new ClientContext(version, versionText, clientId, platform, timestamp);
    }

    // Accepts semantic versions with a pre-release or build suffix; only the numbers are compared.
    private static Version? ParseVersion(string text)
    {
        string core = text.Split('-', '+')[0];
        return Version.TryParse(core, out Version? parsed) ? parsed : null;
    }

    private static string Required(IHeaderDictionary headers, string name)
    {
        string? value = headers[name];
        return string.IsNullOrWhiteSpace(value)
            ? throw ServiceException.BadRequest(ErrorCodes.HeaderMissing, $"The header {name} is required.")
            : value.Trim();
    }

    private static ServiceException Invalid(string name, string expected) =>
        ServiceException.BadRequest(ErrorCodes.HeaderInvalid, $"The header {name} must be {expected}.");
}
