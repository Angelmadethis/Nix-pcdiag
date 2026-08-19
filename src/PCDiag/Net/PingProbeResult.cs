namespace PCDiag.Net;

/// <summary>The outcome of a single ICMP echo probe.</summary>
public enum PingProbeOutcome
{
    /// <summary>A reply was received.</summary>
    Success,

    /// <summary>The target (or an intermediate router) reported the packet was too large.</summary>
    FragmentationNeeded,

    /// <summary>The target (or an intermediate router) reported the destination unreachable.</summary>
    Unreachable,

    /// <summary>No reply was received within the probe timeout.</summary>
    TimedOut,

    /// <summary>A probe failed for another reason (socket error, etc.).</summary>
    Failed
}

/// <summary>The result of a single ICMP echo probe.</summary>
public sealed record PingProbeResult
{
    public PingProbeOutcome Outcome { get; init; }

    /// <summary>Round-trip time in milliseconds for received replies; 0 for timeouts.</summary>
    public long RoundTripMs { get; init; }

    /// <summary>The raw ICMP status reported by the platform, when available.</summary>
    public string? IcmpStatus { get; init; }

    /// <summary>Human-readable reason for failed probes, when available.</summary>
    public string? Error { get; init; }
}