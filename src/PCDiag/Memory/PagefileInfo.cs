namespace PCDiag.Memory;

/// <summary>Usage counters for a single pagefile (from Win32_PageFileUsage, MB values converted to bytes).</summary>
public sealed record PagefileEntry
{
    /// <summary>Location, e.g. C:\pagefile.sys.</summary>
    public required string Location { get; init; }

    /// <summary>Allocated size in bytes.</summary>
    public long? AllocatedBytes { get; init; }

    /// <summary>Current usage in bytes.</summary>
    public long? CurrentBytes { get; init; }

    /// <summary>Highest usage since boot in bytes.</summary>
    public long? PeakBytes { get; init; }
}

/// <summary>
/// Pagefile configuration parsed from the read-only registry value
/// HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PagingFiles.
/// </summary>
public sealed record PagefileConfig
{
    /// <summary>True when Windows manages the pagefile size automatically.</summary>
    public required bool IsSystemManaged { get; init; }

    /// <summary>Raw registry entries, each "path [min max]". Empty means no pagefile is configured.</summary>
    public required IReadOnlyList<string> Entries { get; init; }

    /// <summary>True when the registry value could be read.</summary>
    public required bool Available { get; init; }
}

/// <summary>Combined pagefile configuration and usage view.</summary>
public sealed record PagefileInfo
{
    /// <summary>Configuration (registry). Null when it could not be read.</summary>
    public PagefileConfig? Config { get; init; }

    /// <summary>Per-file usage counters. Empty when usage could not be read.</summary>
    public IReadOnlyList<PagefileEntry> Usage { get; init; } = Array.Empty<PagefileEntry>();

    /// <summary>Total allocated size across all pagefiles (bytes).</summary>
    public long? TotalAllocatedBytes { get; init; }

    /// <summary>Total current usage across all pagefiles (bytes).</summary>
    public long? TotalCurrentBytes { get; init; }

    /// <summary>Total peak usage across all pagefiles (bytes).</summary>
    public long? TotalPeakBytes { get; init; }

    /// <summary>Total installed physical memory (bytes), for context.</summary>
    public long? PhysicalBytes { get; init; }

    /// <summary>True when pagefile usage counters were readable.</summary>
    public bool UsageAvailable { get; init; }
}