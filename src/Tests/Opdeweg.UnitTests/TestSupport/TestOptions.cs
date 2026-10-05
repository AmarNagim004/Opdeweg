using Microsoft.Extensions.Options;
using Opdeweg.Application.Options;

namespace Opdeweg.UnitTests.TestSupport;

internal static class TestOptions
{
    public static IOptions<ProximityOptions> Proximity(Action<ProximityOptions>? configure = null)
    {
        var options = new ProximityOptions();
        configure?.Invoke(options);
        return Options.Create(options);
    }

    public static IOptions<LocationOptions> Location(Action<LocationOptions>? configure = null)
    {
        var options = new LocationOptions();
        configure?.Invoke(options);
        return Options.Create(options);
    }

    public static IOptions<DrivingSessionOptions> Driving(Action<DrivingSessionOptions>? configure = null)
    {
        var options = new DrivingSessionOptions();
        configure?.Invoke(options);
        return Options.Create(options);
    }
}
