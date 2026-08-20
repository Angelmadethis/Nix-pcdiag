namespace PCDiag.Interrupts;

/// <summary>
/// Per-processor counters derived from raw Win32_PerfRawData_PerfOS_Processor deltas
/// over a short window. Values are activity rates and percentages - never per-DPC
/// latency (that requires an admin ETW kernel trace and is explicitly out of scope).
/// </summary>
public sealed record InterruptCoreSample
{
    /// <summary>Perf instance name: a logical processor index ("0", "1", ...) or "_Total".</summary>
    public required string Instance { get; init; }

    /// <summary>Interrupts per second during the sample window.</summary>
    public double? InterruptsPerSecond { get; init; }

    /// <summary>DPCs queued per second during the sample window.</summary>
    public double? DpcsPerSecond { get; init; }

    /// <summary>DPC rate (DPCs queued per interval) reported directly by the counter.</summary>
    public double? DpcRate { get; init; }

    /// <summary>Percent of time in kernel mode (includes interrupt and DPC servicing), 0..100.</summary>
    public double? PrivilegedPercent { get; init; }

    /// <summary>Percent of time the processor was busy, 0..100.</summary>
    public double? ProcessorPercent { get; init; }
}

/// <summary>
/// The full interrupt/DPC activity snapshot for one check run: per-logical-processor
/// samples, the _Total aggregate, the sample window, availability flags, and a
/// non-attributed inventory of loaded drivers and devices for context only.
/// </summary>
public sealed record InterruptSnapshot
{
    public IReadOnlyList<InterruptCoreSample> Cores { get; init; } = Array.Empty<InterruptCoreSample>();

    public InterruptCoreSample? Total { get; init; }

    /// <summary>Duration of the passive sample window in seconds.</summary>
    public double SampleDurationSeconds { get; init; }

    /// <summary>True when at least the _Total activity counters could be read.</summary>
    public bool CountersAvailable { get; init; }

    /// <summary>True when per-processor samples could be read.</summary>
    public bool TopologyAvailable { get; init; }

    /// <summary>Names of currently loaded drivers (State = Running). Context only - never attribution.</summary>
    public IReadOnlyList<string> LoadedDrivers { get; init; } = Array.Empty<string>();

    /// <summary>Descriptions of PnP devices present. Context only - never attribution.</summary>
    public IReadOnlyList<string> Devices { get; init; } = Array.Empty<string>();

    /// <summary>True when the driver/device inventory could be read.</summary>
    public bool InventoryAvailable { get; init; }
}