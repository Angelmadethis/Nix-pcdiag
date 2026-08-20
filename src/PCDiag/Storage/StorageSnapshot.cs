namespace PCDiag.Storage;

/// <summary>A local fixed volume (drive letter) with capacity and free space.</summary>
public sealed record StorageVolume
{
    /// <summary>Device ID, e.g. "C:".</summary>
    public required string DeviceId { get; init; }

    /// <summary>Volume label, if any.</summary>
    public string? VolumeName { get; init; }

    /// <summary>Total capacity in bytes.</summary>
    public long? SizeBytes { get; init; }

    /// <summary>Free space in bytes.</summary>
    public long? FreeBytes { get; init; }

    /// <summary>Filesystem name, e.g. NTFS.</summary>
    public string? FileSystem { get; init; }

    /// <summary>True when the volume dirty bit is set (was not cleanly dismounted).</summary>
    public bool? IsDirty { get; init; }

    /// <summary>Free space as a fraction of capacity (0..1). Null when size is unknown.</summary>
    public double? FreeFraction { get; init; }
}

/// <summary>Health state reported by the Windows storage stack (MSFT_PhysicalDisk.HealthStatus).</summary>
public enum StorageHealthState
{
    Healthy,
    Warning,
    Unhealthy,
    Unknown
}

/// <summary>
/// Health information for one physical disk. The stack health status may be available
/// even when detailed SMART/NVMe reliability counters are not; the availability is
/// explicit so the check never claims a drive is perfect on missing data.
/// </summary>
public sealed record StorageHealth
{
    /// <summary>Health status reported by the storage stack.</summary>
    public StorageHealthState StackState { get; init; } = StorageHealthState.Unknown;

    /// <summary>True when the storage namespace (MSFT_*) could be queried at all.</summary>
    public bool StackQueried { get; init; }

    /// <summary>True when SMART/NVMe reliability counters were available.</summary>
    public bool HasReliabilityCounters { get; init; }

    /// <summary>NVMe wear as a percentage of rated lifetime (0..100+).</summary>
    public int? WearPercent { get; init; }

    /// <summary>Drive temperature in Celsius.</summary>
    public int? TemperatureCelsius { get; init; }

    /// <summary>Uncorrected read errors (total).</summary>
    public long? ReadErrorsUncorrected { get; init; }

    /// <summary>Uncorrected write errors (total).</summary>
    public long? WriteErrorsUncorrected { get; init; }

    /// <summary>Corrected read errors (total) - evidence only.</summary>
    public long? ReadErrorsCorrected { get; init; }

    /// <summary>Corrected write errors (total) - evidence only.</summary>
    public long? WriteErrorsCorrected { get; init; }
}

/// <summary>A physical disk with its identity and health state.</summary>
public sealed record PhysicalDiskInfo
{
    /// <summary>Model string, e.g. KBG40ZNS256G NVMe KIOXIA.</summary>
    public required string Model { get; init; }

    /// <summary>Capacity in bytes.</summary>
    public long? SizeBytes { get; init; }

    /// <summary>Interface, e.g. SCSI (typical for NVMe), SATA, NVMe.</summary>
    public string? InterfaceType { get; init; }

    /// <summary>Media type label: HDD, SSD, SCM, or Unknown.</summary>
    public string? MediaTypeLabel { get; init; }

    /// <summary>Health information for this disk.</summary>
    public StorageHealth Health { get; init; } = new();
}

/// <summary>
/// A passive latency sample for one physical disk instance (e.g. "0 C:" or "_Total").
/// Latency is computed from raw performance-counter deltas over a short window; it is
/// never a load test. When no I/O occurred during the window the sample is idle.
/// </summary>
public sealed record DiskLatencySample
{
    /// <summary>Perf instance name, e.g. "0 C:" or "_Total".</summary>
    public required string Instance { get; init; }

    /// <summary>Average read latency over the window (seconds), when there was read activity.</summary>
    public double? AverageReadSeconds { get; init; }

    /// <summary>Average write latency over the window (seconds), when there was write activity.</summary>
    public double? AverageWriteSeconds { get; init; }

    /// <summary>Reads per second during the window.</summary>
    public double? ReadsPerSecond { get; init; }

    /// <summary>Writes per second during the window.</summary>
    public double? WritesPerSecond { get; init; }

    /// <summary>True when any disk I/O activity was observed during the window.</summary>
    public bool HadIoActivity { get; init; }
}

/// <summary>The full storage snapshot for one check run.</summary>
public sealed record StorageSnapshot
{
    public IReadOnlyList<StorageVolume> Volumes { get; init; } = Array.Empty<StorageVolume>();
    public IReadOnlyList<PhysicalDiskInfo> Disks { get; init; } = Array.Empty<PhysicalDiskInfo>();
    public IReadOnlyList<DiskLatencySample> Latency { get; init; } = Array.Empty<DiskLatencySample>();

    /// <summary>True when volume queries succeeded.</summary>
    public bool VolumesAvailable { get; init; }

    /// <summary>True when physical disk queries succeeded.</summary>
    public bool DisksAvailable { get; init; }

    /// <summary>True when the storage namespace (MSFT_*) was queryable.</summary>
    public bool StorageNamespaceAvailable { get; init; }
}