namespace PCDiag.Net;

/// <summary>
/// Tuning constants for path MTU measurement. The check never assumes a specific
/// MTU is correct; it measures the largest Don't-Fragment packet that passes and
/// compares it against the interface MTU.
/// </summary>
public sealed record MtuOptions
{
    /// <summary>The default options used by the diagnostic check.</summary>
    public static readonly MtuOptions Default = new();

    /// <summary>Smallest payload size probed during the binary search.</summary>
    public int SearchMinPayload { get; init; } = 68;

    /// <summary>Largest payload probed when the interface MTU is unknown (1472 = 1500 - 28).</summary>
    public int DefaultMaxPayload { get; init; } = 1472;

    /// <summary>Per-probe receive timeout during the MTU search.</summary>
    public TimeSpan ProbeTimeout { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>How many extra probes confirm the found boundary.</summary>
    public int ConfirmationProbes { get; init; } = 2;

    /// <summary>Maximum number of internet endpoints probed for path MTU.</summary>
    public int MaxMtuTargets { get; init; } = 1;

    /// <summary>Default internet endpoint used when no target is configured.</summary>
    public IReadOnlyList<string> DefaultInternetTargets { get; init; } = new[] { "1.1.1.1" };

    /// <summary>Fixed IPv4 + ICMP header overhead subtracted from an MTU to get the payload size.</summary>
    public const int IcmpIpv4Overhead = 28;
}