namespace PCDiag.Dns;

/// <summary>
/// Tuning constants for DNS measurements. All thresholds are explicit so the
/// classifier can be reasoned about and tested independently of the network.
/// </summary>
public sealed record DnsOptions
{
    /// <summary>The default options used by the diagnostic check.</summary>
    public static readonly DnsOptions Default = new();

    /// <summary>How many probes to send per resolver (early-abort may stop sooner).</summary>
    public int ProbesPerResolver { get; init; } = 5;

    /// <summary>Per-probe receive timeout.</summary>
    public TimeSpan ProbeTimeout { get; init; } = TimeSpan.FromMilliseconds(1500);

    /// <summary>Maximum number of resolvers to probe (keeps runtime bounded).</summary>
    public int MaxResolvers { get; init; } = 3;

    /// <summary>Consecutive timeouts after which a resolver is aborted as unreachable.</summary>
    public int MaxTimeoutsBeforeAbort { get; init; } = 2;

    /// <summary>Failure+timeout rate at or above which a resolver is unreliable.</summary>
    public double UnreliableFailureRate { get; init; } = 0.4;

    /// <summary>Average latency (ms) at or above which a reliable resolver is slow.</summary>
    public double SlowLatencyMs { get; init; } = 500;

    /// <summary>Safe, IANA-reserved test domains that are stable and non-invasive to query.</summary>
    public IReadOnlyList<string> TestDomains { get; init; } = new[] { "example.com", "example.org" };
}