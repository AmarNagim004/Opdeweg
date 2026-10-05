using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opdeweg.Application.Features.Driving;
using Opdeweg.Application.Features.Proximity;
using Opdeweg.Application.Interfaces;
using Opdeweg.Application.Options;
using Opdeweg.Domain.Enums;

namespace Opdeweg.Infrastructure.Services;

/// <summary>
/// Expires presence: drivers silent for <c>StaleAfter</c> leave proximity (their session survives
/// tunnels and dead zones); sessions silent for the idle timeout are ended.
/// </summary>
internal sealed partial class PresenceSweeper(
    IPresenceStore store,
    IProximityCommandQueue queue,
    IServiceScopeFactory scopes,
    IOptions<ProximityOptions> proximityOptions,
    IOptions<DrivingSessionOptions> drivingOptions,
    TimeProvider time,
    ILogger<PresenceSweeper> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(proximityOptions.Value.SweepIntervalSeconds), time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogSweepFailed(logger, ex);
            }
        }
    }

    internal async Task SweepAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();

        var stale = await store.GetDriversSeenBeforeAsync(now - proximityOptions.Value.StaleAfter, onlyIndexed: true, limit: 500, cancellationToken);
        foreach (var userId in stale)
        {
            queue.TryEnqueue(new RemoveDriverCommand(userId, ProximityChangeReason.Stale, EndSession: false));
        }

        if (stale.Count > 0)
        {
            LogStaleDrivers(logger, stale.Count);
        }

        var idle = await store.GetDriversSeenBeforeAsync(now - drivingOptions.Value.IdleTimeout, onlyIndexed: false, limit: 100, cancellationToken);
        foreach (var userId in idle)
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<DrivingSessionService>().EndAsync(userId, DrivingSessionEndReason.Idle, cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Removing {Count} stale drivers from proximity")]
    private static partial void LogStaleDrivers(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Presence sweep failed")]
    private static partial void LogSweepFailed(ILogger logger, Exception exception);
}
