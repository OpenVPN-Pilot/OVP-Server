using System.Security.Cryptography.X509Certificates;
using OpenVpnPilot.Server.Auth;
using OpenVpnPilot.Server.Commands;
using OpenVpnPilot.Server.Configuration;
using OpenVpnPilot.Server.Data;
using OpenVpnPilot.Server.Extensions;
using OpenVpnPilot.Server.Logging;
using OpenVpnPilot.Server.Middleware;
using Serilog;

if (CommandLine.TryRun(args, out int commandExitCode))
{
    return commandExitCode;
}

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
ServerOptions options;
X509Certificate2? certificate;
try
{
    options = ServerOptions.Load(builder.Configuration);
    certificate = builder.ConfigurePilotKestrel(options.Tls);
}
catch (ConfigurationException exception)
{
    // Logging is not configured yet, and a configuration error is exactly what an operator must see.
    Console.Error.WriteLine(exception.Message);
    return 2;
}

builder.Services.AddSerilog((_, logging) => LoggingSetup.Configure(logging, options.Logging));
builder.Services.AddPilotServer(options);

WebApplication app = builder.Build();
ILogger<Program> logger = app.Services.GetRequiredService<ILogger<Program>>();
HostLog.Starting(logger, RequestContextMiddleware.ServerVersion, options.Auth.Mode, options.Tls.Mode, options.Api.SwaggerEnabled);

try
{
    // Built now rather than on the first sign in, so a broken user file stops the start, not a login.
    _ = app.Services.GetRequiredService<IAuthProvider>();
}
catch (ConfigurationException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}

HostingSetup.ReportListeners(logger, options.Tls, certificate, TimeProvider.System);

if (options.Database.MigrateOnStart)
{
    await DatabaseSetup.MigrateAsync(app.Services, logger, app.Lifetime.ApplicationStopping);
}
else
{
    HostLog.MigrationSkipped(logger);
}

app.UsePilotPipeline(options);
await app.RunAsync();
return 0;
