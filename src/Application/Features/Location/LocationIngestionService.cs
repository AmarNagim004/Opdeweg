using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opdeweg.Application.Common;
using Opdeweg.Application.DTOs;
using Opdeweg.Application.Features.Driving;
using Opdeweg.Application.Features.Proximity;
using Opdeweg.Application.Interfaces;
using Opdeweg.Application.Logging;
using Opdeweg.Application.Options;
using Opdeweg.Domain.ValueObjects;

namespace Opdeweg.Application.Features.Location;

/// <summary>
/// Accepts a client location fix: validate → store latest position (no history) → evaluate
/// proximity → answer with the fresh proximity snapshot so the client stays in sync even when
/// its realtime connection is down (e.g. while backgrounded).
/// </summary>
public sealed class LocationIngestionService(
    IPresenceStore store,
    LocationUpdateValidator validator,
    IProximityCommandQueue proximityQueue,
    ProximityQueryService query,
    IDrivingSessionRepository sessions,
    IOptions<LocationOptions> locationOptions,
    IOptions<ProximityOptions> proximityOptions,
    IOptions<DrivingSessionOptions> drivingOptions,
    TimeProvider time,
    ILogger<LocationIngestionService> logger)
{
    public async Task<LocationUpdateResponse> IngestAsync(Guid userId, LocationUpdateRequest request, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var session = await store.GetSessionAsync(userId, cancellationToken)
            ?? throw AppException.Conflict("no_active_session", "Start eerst je rit, dan kun je je locatie delen.");

        var previous = await store.GetPositionAsync(userId, cancellationToken);
        var verdict = validator.Validate(request, previous, now);
        var timeouts = PresenceTimeoutsFactory.Create(proximityOptions.Value, drivingOptions.Value);

        switch (verdict.Outcome)
        {
            case LocationVerdictOutcome.Rejected:
                if (verdict.ImpliedSpeedMetersPerSecond is { } impliedSpeed)
                {
                    Log.ImplausibleMovement(logger, userId, impliedSpeed);
                }
                else
                {
                    Log.LocationRejected(logger, userId, verdict.Reason ?? "unknown");
                }

                throw verdict.ToException();

            case LocationVerdictOutcome.Throttled:
                Log.LocationThrottled(logger, userId);
                return new LocationUpdateResponse(LocationUpdateStatus.Throttled, null);

            case LocationVerdictOutcome.LowAccuracy:
                Log.LocationLowAccuracy(logger, userId, request.Accuracy);
                await store.TouchAsync(userId, now, timeouts, cancellationToken);
                return new LocationUpdateResponse(LocationUpdateStatus.LowAccuracy, await query.GetSnapshotAsync(userId, includeVoice: false, cancellationToken));
        }

        var point = verdict.Point!.Value;
        var position = new StoredPosition(point, request.Accuracy, request.Speed, request.Heading, request.Timestamp!.Value, now, now);
        await store.SavePositionAsync(userId, position, timeouts, cancellationToken);
        Log.LocationAccepted(logger, userId, request.Accuracy);

        if (!session.CoarseAreaRecorded && drivingOptions.Value.StoreCoarseStartArea)
        {
            var coarse = GeoMath.SnapToGrid(point, drivingOptions.Value.CoarseAreaGridDegrees);
            await sessions.RecordCoarseStartAreaAsync(session.SessionId, coarse, cancellationToken);
            await store.MarkCoarseAreaRecordedAsync(userId, cancellationToken);
        }

        var timeout = TimeSpan.FromMilliseconds(locationOptions.Value.EvaluationTimeoutMilliseconds);
        await proximityQueue.EnqueueAndWaitAsync(new EvaluateDriverCommand(userId), timeout, cancellationToken);

        return new LocationUpdateResponse(LocationUpdateStatus.Accepted, await query.GetSnapshotAsync(userId, includeVoice: false, cancellationToken));
    }
}
