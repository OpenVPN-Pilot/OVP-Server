using OpenVpnPilot.Server.Background;
using OpenVpnPilot.Server.Configuration;
using OpenVpnPilot.Server.Data;
using OpenVpnPilot.Server.Logging;
using OpenVpnPilot.Server.Repositories;
using OpenVpnPilot.Server.Repositories.Interfaces;
using OpenVpnPilot.Server.Security;
using OpenVpnPilot.Server.Services;
using OpenVpnPilot.Server.Services.Auth;
using OpenVpnPilot.Server.Services.Preferences;
using OpenVpnPilot.Server.Services.Profiles;
using OpenVpnPilot.Server.Services.Sync;
using OpenVpnPilot.Server.Services.Users;
using OpenVpnPilot.Server.Services.Vault;

namespace OpenVpnPilot.Server.Extensions;

public static class ServiceRegistration
{
    public static IServiceCollection AddPilotServer(this IServiceCollection services, ServerOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton(options.Database);
        services.AddSingleton(options.Security);
        services.AddSingleton(options.Tls);
        services.AddSingleton(options.Auth);
        services.AddSingleton(options.Logging);
        services.AddSingleton(options.Api);
        services.AddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        services.AddHttpContextAccessor();

        services.AddDbContext<PilotServerDbContext>(db => DatabaseSetup.Configure(db, options.Database.ConnectionString));

        return services
            .AddPilotSecurity()
            .AddPilotRepositories()
            .AddPilotServices()
            .AddPilotAuthentication(options)
            .AddPilotApi(options)
            .AddPilotBackground();
    }

    private static IServiceCollection AddPilotSecurity(this IServiceCollection services) =>
        services
            .AddSingleton<ISecretCipher, AesGcmSecretCipher>()
            .AddSingleton<IPasswordHasher, Argon2PasswordHasher>();

    private static IServiceCollection AddPilotRepositories(this IServiceCollection services) =>
        services
            .AddScoped<IUnitOfWork, UnitOfWork>()
            .AddScoped<IUserRepository, UserRepository>()
            .AddScoped<IRefreshTokenRepository, RefreshTokenRepository>()
            .AddScoped<IProfileRepository, ProfileRepository>()
            .AddScoped<ITagRepository, TagRepository>()
            .AddScoped<IVaultRepository, VaultRepository>()
            .AddScoped<ISyncRepository, SyncRepository>()
            .AddScoped<IUserPreferenceRepository, UserPreferenceRepository>();

    private static IServiceCollection AddPilotServices(this IServiceCollection services) =>
        services
            .AddSingleton<IServerInfoService, ServerInfoService>()
            .AddScoped<IUserStateService, UserStateService>()
            .AddScoped<IUserAccountService, UserAccountService>()
            .AddScoped<ISessionService, SessionService>()
            .AddScoped<IAuthService, AuthService>()
            .AddScoped<ProfileFactory>()
            .AddScoped<TagAssigner>()
            .AddScoped<IProfileService, ProfileService>()
            .AddScoped<IProfileImportService, ProfileImportService>()
            .AddScoped<ITagService, TagService>()
            .AddScoped<VaultCodec>()
            .AddScoped<IVaultService, VaultService>()
            .AddScoped<ISyncService, SyncService>()
            .AddScoped<IPreferenceService, PreferenceService>()
            .AddScoped<ISettingsService, SettingsService>()
            .AddScoped<IUserAdminService, UserAdminService>();

    private static IServiceCollection AddPilotBackground(this IServiceCollection services) =>
        services
            .AddHostedService<LogRetentionService>()
            .AddHostedService<DataMaintenanceService>();
}
