using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opdeweg.Application.Features.Driving;
using Opdeweg.Application.Interfaces;
using Opdeweg.Application.Options;
using Opdeweg.Domain.Enums;
using Opdeweg.Domain.ValueObjects;
using Opdeweg.Infrastructure.Persistence;

namespace Opdeweg.Infrastructure.Services;

/// <summary>Hourly privacy and hygiene jobs: purge coarse areas, prune tokens, close orphaned sessions.</summary>
internal sealed partial class DataRetentionWorker(
    IServiceScopeFactory scopes,
    IPresenceStore store,
    IOptions<DrivingSessionOptions> drivingOptions,
    TimeProvider time,
    ILogger<DataRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), time);
        do
        {
            try
            {
                await RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogRetentionFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var areaCutoff = now.AddDays(-drivingOptions.Value.CoarseAreaRetentionDays);
        var purgedAreas = await db.DrivingSessions
            .Where(s => s.StartedAt < areaCutoff && s.CoarseStartArea != null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CoarseStartArea, (GeoPoint?)null), cancellationToken);

        var tokenCutoff = now.AddDays(-7);
        var prunedTokens = await db.RefreshTokens
            .Where(t => t.ExpiresAt < tokenCutoff || t.RevokedAt < tokenCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        // Sessions still "active" in the database long after their presence vanished (e.g. crash or Redis loss).
        var orphanCutoff = now - drivingOptions.Value.IdleTimeout - TimeSpan.FromHours(1);
        var candidates = await db.DrivingSessions
            .Where(s => s.EndedAt == null && s.StartedAt < orphanCutoff)
            .OrderBy(s => s.StartedAt)
            .Select(s => s.UserId)
            .Take(500)
            .ToListAsync(cancellationToken);

        var sessions = scope.ServiceProvider.GetRequiredService<DrivingSessionService>();
        var closed = 0;
        foreach (var userId in candidates)
        {
            if (await store.GetSessionAsync(userId, cancellationToken) is null)
            {
                await sessions.EndAsync(userId, DrivingSessionEndReason.Orphaned, cancellationToken);
                closed++;
            }
        }

        LogRetentionCompleted(logger, purgedAreas, prunedTokens, closed);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Retention: purged {Areas} coarse areas, {Tokens} refresh tokens, closed {Sessions} orphaned sessions")]
    private static partial void LogRetentionCompleted(ILogger logger, int areas, int tokens, int sessions);

    [LoggerMessage(Level = LogLevel.Error, Message = "Data retention run failed")]
    private static partial void LogRetentionFailed(ILogger logger, Exception exception);
}
