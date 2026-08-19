namespace PCDiag.Net;

/// <summary>
/// Tuning constants shared by the path-diagnostic checks (gateway reachability,
/// packet loss, latency). Conservative by default: low probe counts and short
/// timeouts so the checks never flood a network.
/// </summary>
public sealed record NetOptions
{
    /// <summary>The default options used by the diagnostic checks.</summary>
    public static readonly NetOptions Default = new();

    /// <summary>How many probes to send to the default gateway (early-abort may stop sooner).</summary>
    public int GatewayProbes { get; init; } = 4;

    /// <summary>How many probes to send to each internet endpoint (early-abort may stop sooner).</summary>
    public int InternetProbes { get; init; } = 5;

    /// <summary>Per-probe receive timeout for the default gateway.</summary>
    public TimeSpan GatewayProbeTimeout { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Per-probe receive timeout for internet endpoints.</summary>
    public TimeSpan InternetProbeTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Maximum number of internet endpoints to probe (keeps runtime bounded).</summary>
    public int MaxInternetTargets { get; init; } = 2;

    /// <summary>Consecutive timeouts after which a target is aborted as unreachable.</summary>
    public int MaxTimeoutsBeforeAbort { get; init; } = 2;

    /// <summary>Loss rate at or above which a target is treated as suspicious.</summary>
    public double LossSuspiciousRate { get; init; } = 0.05;

    /// <summary>Loss rate at or above which a target is treated as lossy.</summary>
    public double LossWarningRate { get; init; } = 0.20;

    /// <summary>Average latency (ms) at or above which a reliable gateway is slow.</summary>
    public double GatewaySlowLatencyMs { get; init; } = 100;

    /// <summary>Average latency (ms) at or above which a reliable internet endpoint is slow.</summary>
    public double InternetSlowLatencyMs { get; init; } = 300;

    /// <summary>Default internet endpoints used when no targets are configured.</summary>
    public IReadOnlyList<string> DefaultInternetTargets { get; init; } = new[] { "1.1.1.1", "8.8.8.8" };
}