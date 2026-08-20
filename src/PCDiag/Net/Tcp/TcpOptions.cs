namespace PCDiag.Net.Tcp;

/// <summary>
/// Thresholds for TCP health and connection-state classification. Chosen to be
/// conservative and contextual (ratios and dynamic-port percentages rather than bare
/// counts). See SPEC.md "Phase 6 - TCP Health" for the full rationale.
/// </summary>
public sealed record TcpOptions
{
    public static readonly TcpOptions Default = new();

    /// <summary>Fallback dynamic-port pool size used when the OS range cannot be read (Windows default).</summary>
    public int TimeWaitPortPoolFallback { get; init; } = 16384;

    /// <summary>TIME_WAIT at or above this fraction of the dynamic port pool is a large accumulation.</summary>
    public double TimeWaitElevatedPortFraction { get; init; } = 0.25;

    /// <summary>TIME_WAIT at or above this fraction of the dynamic port pool approaches exhaustion.</summary>
    public double TimeWaitWarningPortFraction { get; init; } = 0.60;

    /// <summary>CLOSE_WAIT above this many sockets is suspicious (may be a leak).</summary>
    public int CloseWaitSuspicious { get; init; } = 10;

    /// <summary>CLOSE_WAIT above this many sockets is treated as a warning.</summary>
    public int CloseWaitWarning { get; init; } = 50;

    /// <summary>A single process owning more CLOSE_WAIT sockets than this is a likely leak.</summary>
    public int CloseWaitPerProcessSuspicious { get; init; } = 25;

    /// <summary>Established connections above this many is suspicious for a desktop PC.</summary>
    public int EstablishedSuspicious { get; init; } = 1000;

    /// <summary>Established connections above this many is a warning (possible runaway app).</summary>
    public int EstablishedWarning { get; init; } = 5000;

    /// <summary>Retransmitted segments at or above this fraction of all segments is suspicious.</summary>
    public double RetransmissionSuspiciousRatio { get; init; } = 0.01;

    /// <summary>Retransmitted segments at or above this fraction of all segments is a warning.</summary>
    public double RetransmissionWarningRatio { get; init; } = 0.05;

    /// <summary>Connection failures at or above this fraction of connections initiated is suspicious.</summary>
    public double FailureSuspiciousRatio { get; init; } = 0.10;

    /// <summary>Connection failures at or above this fraction of connections initiated is a warning.</summary>
    public double FailureWarningRatio { get; init; } = 0.30;

    /// <summary>Adapter error rate (errors/sec since boot) at or above which the adapter is suspicious.</summary>
    public double AdapterErrorSuspiciousPerSecond { get; init; } = 0.01;

    /// <summary>Adapter error rate (errors/sec since boot) at or above which the adapter is a warning.</summary>
    public double AdapterErrorWarningPerSecond { get; init; } = 0.10;

    /// <summary>How many per-process offenders to list in evidence.</summary>
    public int MaxTopProcesses { get; init; } = 5;
}