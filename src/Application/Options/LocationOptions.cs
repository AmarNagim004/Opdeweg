using System.ComponentModel.DataAnnotations;

namespace Opdeweg.Application.Options;

public sealed class LocationOptions
{
    public const string SectionName = "Location";

    // ---- Client transmission policy (served to the app via /api/v1/config) ----

    /// <summary>Transmit after moving at least this far since the last transmitted fix.</summary>
    [Range(5, 5000)]
    public double ClientDistanceThresholdMeters { get; set; } = 50;

    /// <summary>Transmit at least this often while a driving session is active (liveness heartbeat).</summary>
    [Range(2, 300)]
    public int ClientMaxIntervalSeconds { get; set; } = 15;

    /// <summary>Never transmit more often than this (unless forced by a lifecycle event).</summary>
    [Range(500, 60_000)]
    public int ClientMinIntervalMilliseconds { get; set; } = 2500;

    [Range(5, 180)]
    public double ClientHeadingChangeDegrees { get; set; } = 45;

    [Range(0.5, 100)]
    public double ClientSpeedChangeMetersPerSecond { get; set; } = 5;

    // ---- Server-side validation ----

    /// <summary>Updates closer together than this are dropped as throttled.</summary>
    [Range(0, 60_000)]
    public int MinUpdateIntervalMilliseconds { get; set; } = 1000;

    /// <summary>Client timestamps older than this are rejected as stale.</summary>
    [Range(1, 3600)]
    public int MaxTimestampAgeSeconds { get; set; } = 60;

    /// <summary>Tolerated client clock skew into the future.</summary>
    [Range(0, 3600)]
    public int MaxTimestampSkewSeconds { get; set; } = 30;

    /// <summary>Implied speeds above this between consecutive fixes are treated as spoofing/teleporting.</summary>
    [Range(10, 400)]
    public double MaxPlausibleSpeedMetersPerSecond { get; set; } = 90;

    [Range(1, 100_000)]
    public double MaxReportedAccuracyMeters { get; set; } = 10_000;

    /// <summary>Slack added to the plausible-movement check to absorb GPS jitter and network jitter.</summary>
    [Range(0, 10_000)]
    public double MovementToleranceMeters { get; set; } = 50;

    /// <summary>How long a location request waits for its proximity evaluation before answering.</summary>
    [Range(0, 30_000)]
    public int EvaluationTimeoutMilliseconds { get; set; } = 2000;
}
