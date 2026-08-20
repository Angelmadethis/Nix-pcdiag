namespace PCDiag.Storage;

/// <summary>
/// Thresholds for the storage classifier. Conservative and contextual. See SPEC.md
/// Phase 9 for the full rationale.
/// </summary>
public sealed record StorageOptions
{
    public static readonly StorageOptions Default = new();

    /// <summary>Volume free space below this fraction of capacity is low.</summary>
    public double LowFreeSpaceFraction { get; init; } = 0.15;

    /// <summary>Volume free space below this fraction of capacity is critically low.</summary>
    public double CriticalFreeSpaceFraction { get; init; } = 0.05;

    /// <summary>NVMe wear at or above this percentage of rated lifetime is a concern.</summary>
    public int WearWarningPercent { get; init; } = 90;

    /// <summary>Drive temperature at or above this (Celsius) is flagged.</summary>
    public int TemperatureSuspiciousCelsius { get; init; } = 70;

    /// <summary>Active average read/write latency at or above this (seconds) is slow.</summary>
    public double SlowLatencySeconds { get; init; } = 0.030;

    /// <summary>Active average read/write latency at or above this (seconds) is very slow.</summary>
    public double VerySlowLatencySeconds { get; init; } = 0.100;
}