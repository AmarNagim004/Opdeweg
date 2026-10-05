using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Opdeweg.Application.Features.Auth;
using Opdeweg.Application.Features.Driving;
using Opdeweg.Application.Features.Location;
using Opdeweg.Application.Features.Profile;
using Opdeweg.Application.Features.Proximity;
using Opdeweg.Application.Features.Voice;
using Opdeweg.Application.Options;

namespace Opdeweg.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ProximityOptions>()
            .Bind(configuration.GetSection(ProximityOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => o.LeaveDistanceMeters > o.JoinDistanceMeters, "Proximity:LeaveDistanceMeters must be greater than JoinDistanceMeters (hysteresis).")
            .Validate(o => o.MaxJoinAccuracyMeters <= o.MaxUsableAccuracyMeters, "Proximity:MaxJoinAccuracyMeters must not exceed MaxUsableAccuracyMeters.")
            .ValidateOnStart();

        services.AddOptions<LocationOptions>()
            .Bind(configuration.GetSection(LocationOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => o.ClientMinIntervalMilliseconds > o.MinUpdateIntervalMilliseconds, "Location:ClientMinIntervalMilliseconds must exceed the server MinUpdateIntervalMilliseconds.")
            .ValidateOnStart();

        services.AddOptions<DrivingSessionOptions>()
            .Bind(configuration.GetSection(DrivingSessionOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        // Proximity pipeline (stateless singletons; the worker serialises processor calls).
        services.AddSingleton<IGroupIdGenerator, RandomGroupIdGenerator>();
        services.AddSingleton<ProximityGroupingEngine>();
        services.AddSingleton<ProximityProcessor>();
        services.AddSingleton<ProximityViewBuilder>();
        services.AddSingleton<ProximityQueryService>();
        services.AddSingleton<IProximityNotifier, ProximityNotifier>();
        services.AddSingleton<VoiceAccessService>();
        services.AddSingleton<LocationUpdateValidator>();

        // Request-scoped use cases.
        services.AddScoped<AuthService>();
        services.AddScoped<ProfileService>();
        services.AddScoped<DrivingSessionService>();
        services.AddScoped<LocationIngestionService>();

        return services;
    }
}
