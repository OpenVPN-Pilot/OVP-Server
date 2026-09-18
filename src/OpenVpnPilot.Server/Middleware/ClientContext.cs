namespace OpenVpnPilot.Server.Middleware;

// What the mandatory headers said about the calling installation, validated.
public sealed record ClientContext(Version ClientVersion, string ClientVersionText, Guid ClientId, string Platform, DateTimeOffset Timestamp);

public static class ClientContextExtensions
{
    public static void SetClientContext(this HttpContext context, ClientContext client) => context.Features.Set(client);

    // Every route that calls this sits behind the header middleware, so a missing context is a defect.
    public static ClientContext GetClientContext(this HttpContext context) =>
        context.Features.Get<ClientContext>()
            ?? throw new InvalidOperationException("The mandatory headers were not validated for this request.");
}
