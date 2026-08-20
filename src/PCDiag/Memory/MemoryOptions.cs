namespace PCDiag.Memory;

/// <summary>
/// Thresholds for the memory and pagefile classifiers. Chosen to be conservative and
/// contextual (ratios and fractions rather than bare counts). See SPEC.md Phase 9 for
/// the full rationale.
/// </summary>
public sealed record MemoryOptions
{
    public static readonly MemoryOptions Default = new();

    /// <summary>Commit ratio (committed / commit limit) at or above this is a large commit usage.</summary>
    public double CommitSuspiciousRatio { get; init; } = 0.70;

    /// <summary>Commit ratio at or above this is a warning (allocation failures become possible).</summary>
    public double CommitWarningRatio { get; init; } = 0.85;

    /// <summary>Available memory below this fraction of installed RAM is suspicious.</summary>
    public double AvailableSuspiciousFraction { get; init; } = 0.15;

    /// <summary>Available memory below this fraction of installed RAM is a warning.</summary>
    public double AvailableWarningFraction { get; init; } = 0.05;

    /// <summary>Available memory below this absolute amount is flagged regardless of the fraction.</summary>
    public long AbsoluteLowAvailableBytes { get; init; } = 1536L * 1024 * 1024;

    /// <summary>Pages/sec at or above this is treated as heavy paging.</summary>
    public double HeavyPagingPerSecond { get; init; } = 200;

    /// <summary>Fixed-size pagefile with current usage at or above this fraction of its allocated size is near full.</summary>
    public double PagefileUsageNearFullRatio { get; init; } = 0.95;

    /// <summary>Fixed-size pagefile with peak usage at or above this fraction of its allocated size is a concern.</summary>
    public double PagefilePeakHighRatio { get; init; } = 0.90;
}