using System.ComponentModel.DataAnnotations;

namespace Opdeweg.Application.Options;

/// <summary>
/// Proximity grouping thresholds. Join and leave distances are intentionally asymmetric
/// (hysteresis) so GPS noise around the boundary cannot cause JOIN → LEAVE → JOIN flapping.
/// </summary>
public sealed class ProximityOptions
{
    public const string SectionName = "Proximity";

    /// <summary>Drivers at or within this distance of every group member may join the group.</summary>
    [Range(10, 50_000)]
    public double JoinDistanceMeters { get; set; } = 1000;

    /// <summary>A member leaves once they are further than this from any other member.</summary>
    [Range(10, 50_000)]
    public double LeaveDistanceMeters { get; set; } = 1100;

    /// <summary>Upper bound on voice group size; keeps conversations intelligible and SFU rooms small.</summary>
    [Range(2, 100)]
    public int MaxGroupSize { get; set; } = 12;

    /// <summary>Maximum number of nearest candidates fetched from the spatial index per evaluation.</summary>
    [Range(4, 1000)]
    public int CandidateSearchLimit { get; set; } = 64;

    /// <summary>Drivers without an accepted update for this long are removed from proximity.</summary>
    [Range(10, 3600)]
    public int StaleAfterSeconds { get; set; } = 60;

    [Range(1, 600)]
    public int SweepIntervalSeconds { get; set; } = 10;

    /// <summary>Fixes worse than this only count as a liveness heartbeat; the stored position is kept.</summary>
    [Range(5, 10_000)]
    public double MaxUsableAccuracyMeters { get; set; } = 150;

    /// <summary>Drivers with a fix worse than this keep existing groups but cannot form or join new ones.</summary>
    [Range(5, 10_000)]
    public double MaxJoinAccuracyMeters { get; set; } = 75;

    /// <summary>Distances shown to other drivers are rounded to this granularity.</summary>
    [Range(1, 1000)]
    public int DistanceRoundingMeters { get; set; } = 50;

    /// <summary>Maximum number of nearby (not grouped) drivers returned in a snapshot.</summary>
    [Range(0, 200)]
    public int NearbyListLimit { get; set; } = 25;

    public TimeSpan StaleAfter => TimeSpan.FromSeconds(StaleAfterSeconds);
}
