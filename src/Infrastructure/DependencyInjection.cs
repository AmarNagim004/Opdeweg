using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Opdeweg.Application.Interfaces;
using Opdeweg.Infrastructure.LiveKit;
using Opdeweg.Infrastructure.Persistence;
using Opdeweg.Infrastructure.Persistence.Repositories;
using Opdeweg.Infrastructure.Redis;
using Opdeweg.Infrastructure.Security;
using Opdeweg.Infrastructure.Services;
using StackExchange.Redis;

namespace Opdeweg.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // PostgreSQL + PostGIS
        var database = configuration.GetConnectionString("Database");
        if (string.IsNullOrWhiteSpace(database))
        {
            throw new InvalidOperationException("ConnectionStrings:Database is not configured (set DATABASE_CONNECTION_STRING).");
        }

        services.AddDbContext<AppDbContext>(options => PersistenceSetup.Configure(options, database));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IDrivingSessionRepository, DrivingSessionRepository>();

        // Redis presence, spatial index and locking
        var redis = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redis))
        {
            throw new InvalidOperationException("ConnectionStrings:Redis is not configured (set REDIS_CONNECTION_STRING).");
        }

        services.AddOptions<RedisOptions>().Bind(configuration.GetSection(RedisOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redis);
            options.AbortOnConnectFail = false;
            options.ClientName ??= "opdeweg-api";
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddSingleton<RedisKeys>();
        services.AddSingleton<IPresenceStore, RedisPresenceStore>();
        services.AddSingleton<IDistributedLock, RedisDistributedLock>();

        // Authentication primitives
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => o.HasStrongSecret, "Jwt:Secret (JWT_SECRET) must be at least 32 bytes.")
            .ValidateOnStart();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();

        // LiveKit SFU
        services.AddOptions<LiveKitOptions>()
            .Bind(configuration.GetSection(LiveKitOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => o.IsValid(), "LiveKit:Url (LIVEKIT_URL) must be a ws(s):// or http(s):// URL; LiveKit:ApiUrl, if set, http(s)://.")
            .ValidateOnStart();
        services.AddSingleton<LiveKitJwt>();
        services.AddSingleton<IVoiceTokenIssuer, LiveKitTokenIssuer>();
        services.AddSingleton<IVoiceRoomAdmin, LiveKitRoomAdmin>();
        services.AddSingleton(sp => new LiveKitWebhookVerifier(sp.GetRequiredService<LiveKitJwt>()));
        services.AddHttpClient(LiveKitRoomAdmin.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(5));

        // Proximity processing pipeline
        services.AddSingleton<ChannelProximityCommandQueue>();
        services.AddSingleton<IProximityCommandQueue>(sp => sp.GetRequiredService<ChannelProximityCommandQueue>());
        services.AddHostedService<ProximityWorker>();
        services.AddSingleton<VoiceRoomReconciler>();
        services.AddSingleton<IVoiceRoomReconciler>(sp => sp.GetRequiredService<VoiceRoomReconciler>());
        services.AddHostedService(sp => sp.GetRequiredService<VoiceRoomReconciler>());
        services.AddHostedService<PresenceSweeper>();
        services.AddHostedService<DataRetentionWorker>();

        return services;
    }
}
