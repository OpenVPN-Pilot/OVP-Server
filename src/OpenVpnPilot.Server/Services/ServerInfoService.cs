using OpenVpnPilot.Server.Configuration;
using OpenVpnPilot.Server.Contracts;
using OpenVpnPilot.Server.Contracts.Responses;
using OpenVpnPilot.Server.Mapping;
using OpenVpnPilot.Server.Middleware;

namespace OpenVpnPilot.Server.Services;

public interface IServerInfoService
{
    public ServerInfoResponse Describe();
}

public sealed class ServerInfoService(ServerOptions options) : IServerInfoService
{
    public const string ProductName = "OpenVPN Pilot Server";

    public ServerInfoResponse Describe()
    {
        EntraOptions? entra = options.Auth.Entra;
        return new ServerInfoResponse(
            ProductName,
            RequestContextMiddleware.ServerVersion,
            PilotHeaders.CurrentApiVersion,
            options.Api.MinimumClientVersion.ToString(),
            options.Auth.Mode.ToWire(),
            options.Auth.Mode is AuthMode.File or AuthMode.Ldap,
            entra is null
                ? null
                : new EntraInfoResponse(
                    entra.TenantId,
                    entra.ClientId,
                    entra.Scope,
                    entra.Authority));
    }
}
