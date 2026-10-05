using System.ComponentModel.DataAnnotations;

namespace Opdeweg.Application.Options;

public sealed class DrivingSessionOptions
{
    public const string SectionName = "DrivingSession";

    /// <summary>A session without any accepted update for this long is ended automatically.</summary>
    [Range(1, 24 * 60)]
    public int IdleTimeoutMinutes { get; set; } = 30;

    /// <summary>Persist a grid-snapped start cell (never a trail) for regional capacity planning.</summary>
    public bool StoreCoarseStartArea { get; set; } = true;

    [Range(0.01, 5)]
    public double CoarseAreaGridDegrees { get; set; } = 0.05;

    [Range(1, 3650)]
    public int CoarseAreaRetentionDays { get; set; } = 30;

    public TimeSpan IdleTimeout => TimeSpan.FromMinutes(IdleTimeoutMinutes);
}
