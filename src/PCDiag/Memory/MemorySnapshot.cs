namespace PCDiag.Memory;

/// <summary>
/// A point-in-time snapshot of system memory state. Every value is nullable and each
/// source carries its own availability flag so the check can distinguish "healthy"
/// from "unavailable" and never fabricate data.
/// </summary>
public sealed record MemorySnapshot
{
    /// <summary>Total installed physical memory (Win32_OperatingSystem.TotalVisibleMemorySize, bytes).</summary>
    public long? TotalPhysicalBytes { get; init; }

    /// <summary>Available memory including reclaimable standby cache (bytes).</summary>
    public long? AvailableBytes { get; init; }

    /// <summary>Committed bytes (all committed virtual memory, bytes).</summary>
    public long? CommittedBytes { get; init; }

    /// <summary>Commit limit (physical + pagefile, bytes).</summary>
    public long? CommitLimitBytes { get; init; }

    /// <summary>Pages/sec at the instant of sampling.</summary>
    public long? PagesPerSecond { get; init; }

    /// <summary>Standby/system cache size (bytes).</summary>
    public long? CacheBytes { get; init; }

    /// <summary>Nonpaged kernel pool (bytes).</summary>
    public long? PoolNonpagedBytes { get; init; }

    /// <summary>Paged kernel pool (bytes).</summary>
    public long? PoolPagedBytes { get; init; }

    /// <summary>Sum of current pagefile usage across all pagefiles (bytes).</summary>
    public long? PagefileCurrentBytes { get; init; }

    /// <summary>Sum of allocated pagefile size across all pagefiles (bytes).</summary>
    public long? PagefileAllocatedBytes { get; init; }

    /// <summary>Highest pagefile usage recorded since boot (bytes).</summary>
    public long? PagefilePeakBytes { get; init; }

    /// <summary>True when the Win32_OperatingSystem row was readable.</summary>
    public bool OperatingSystemInfoAvailable { get; init; }

    /// <summary>True when the PerfOS memory counters were readable.</summary>
    public bool PerfCountersAvailable { get; init; }

    /// <summary>True when pagefile usage counters were readable.</summary>
    public bool PagefileUsageAvailable { get; init; }
}