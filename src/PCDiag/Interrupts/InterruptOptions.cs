namespace PCDiag.Interrupts;

/// <summary>
/// Heuristic thresholds for the interrupt/DPC activity classifier. Absolute rates vary
/// widely by machine and workload, so these are conservative reference values, not
/// measurements of true DPC latency. See SPEC.md Phase 11 for the rationale.
/// </summary>
public sealed record InterruptOptions
{
    public static readonly InterruptOptions Default = new();

    /// <summary>_Total interrupts/sec at or above this is elevated interrupt activity.</summary>
    public double InterruptsPerSecondSuspicious { get; init; } = 10_000;

    /// <summary>_Total interrupts/sec at or above this is high interrupt activity.</summary>
    public double InterruptsPerSecondWarning { get; init; } = 25_000;

    /// <summary>_Total DPCs queued/sec at or above this is elevated DPC activity.</summary>
    public double DpcsPerSecondSuspicious { get; init; } = 2_000;

    /// <summary>_Total DPCs queued/sec at or above this is high DPC activity.</summary>
    public double DpcsPerSecondWarning { get; init; } = 8_000;

    /// <summary>_Total percent privileged time (%) at or above this is kernel-time heavy.</summary>
    public double PrivilegedTimeSuspicious { get; init; } = 20;

    /// <summary>_Total percent privileged time (%) at or above this is very kernel-time heavy.</summary>
    public double PrivilegedTimeWarning { get; init; } = 40;

    /// <summary>
    /// A core's interrupts/sec is flagged as a concentrated load when it exceeds this
    /// multiple of the median core rate and is also above the concentration floor.
    /// </summary>
    public double ConcentrationFactor { get; init; } = 3.0;

    /// <summary>Absolute floor (interrupts/sec) before a concentrated core is flagged.</summary>
    public double ConcentrationFloorPerSecond { get; init; } = 5_000;
}