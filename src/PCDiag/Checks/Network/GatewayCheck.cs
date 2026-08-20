using PCDiag.Core;
using PCDiag.Fixes;

namespace PCDiag.Checks.Network;

/// <summary>
/// Measures reachability, packet loss, and latency of the active default gateway.
/// Read-only: no settings are modified.
/// </summary>
public sealed class GatewayCheck : DiagnosticCheck, IFixableCheck
{
    private readonly PCDiag.Net.NetOptions _options;
    private readonly PCDiag.Net.IPingProbe _probe;

    public override string CheckId => "NET-GWY-001";
    public override string Name => "Default Gateway";
    public override DiagnosticCategory Category => DiagnosticCategory.Network;
    public override string Description =>
        "Checks the default gateway for reachability, packet loss, and latency.";

    public GatewayCheck(PCDiag.Net.NetOptions? options = null, PCDiag.Net.IPingProbe? probe = null)
    {
        _options = options ?? PCDiag.Net.NetOptions.Default;
        _probe = probe ?? new PCDiag.Net.SystemPingProbe();
    }

    private static readonly IReadOnlyList<string> CheckLimitations = new[]
    {
        "Measurements are point-in-time; a single run may not reflect sustained conditions.",
        "The gateway is probed over ICMP echo; some devices limit or block ICMP replies, which can appear as loss.",
        "Latency to the first hop includes Wi-Fi and local congestion, not just router processing time."
    };

    protected override async Task<DiagnosticResult> RunAsync(DiagnosticContext context, CancellationToken cancellationToken)
    {
        var active = context.Inventory?.Network.ActiveConnection;
        if (active is null)
            return NoActiveConnectionResult();

        var gateway = PCDiag.Net.TargetResolver.FirstGateway(active.GatewayAddresses);
        if (gateway is null)
            return NoGatewayResult();

        var probes = await PCDiag.Net.ProbeRunner.ProbeAsync(
            _probe,
            gateway,
            _options.GatewayProbes,
            _options.GatewayProbeTimeout,
            _options.MaxTimeoutsBeforeAbort,
            payloadBytes: 32,
            dontFragment: false,
            cancellationToken).ConfigureAwait(false);

        var stats = PCDiag.Net.PingMeasurementStats.Compute(probes);
        var health = PCDiag.Net.GatewayClassifier.Classify(stats, _options);
        var (severity, status) = MapHealth(health);

        return BuildResult(
            severity,
            status,
            BuildSummary(health, gateway, stats),
            detail: BuildDetail(health, stats),
            evidence: BuildEvidence(active, gateway, stats),
            recommendations: BuildRecommendations(health, gateway, stats),
            possibleCauses: PossibleCauses(health),
            limitations: CheckLimitations,
            confidence: ComputeConfidence(health, stats));
    }

    private static (DiagnosticSeverity Severity, DiagnosticStatus Status) MapHealth(PCDiag.Net.GatewayHealth health)
    {
        return health switch
        {
            PCDiag.Net.GatewayHealth.Unreachable => (DiagnosticSeverity.Critical, DiagnosticStatus.Finding),
            PCDiag.Net.GatewayHealth.Lossy => (DiagnosticSeverity.Warning, DiagnosticStatus.Finding),
            PCDiag.Net.GatewayHealth.Slow => (DiagnosticSeverity.Suspicious, DiagnosticStatus.Finding),
            _ => (DiagnosticSeverity.Healthy, DiagnosticStatus.Passed)
        };
    }

    private DiagnosticResult NoActiveConnectionResult()
    {
        return BuildResult(
            DiagnosticSeverity.Info,
            DiagnosticStatus.Unavailable,
            "No active network connection was found, so the default gateway could not be probed.",
            evidence: new[]
            {
                new DiagnosticEvidence
                {
                    Description = "Active Connection",
                    Value = "None",
                    Source = "SystemInventory.Network"
                }
            },
            recommendations: new[]
            {
                new DiagnosticRecommendation
                {
                    Text = "Verify that a network adapter is connected and has a default gateway.",
                    RequiresAdmin = false,
                    Priority = 1
                }
            },
            confidence: 0.9);
    }

    private DiagnosticResult NoGatewayResult()
    {
        return BuildResult(
            DiagnosticSeverity.Info,
            DiagnosticStatus.Unavailable,
            "No default gateway is configured, so gateway reachability could not be measured.",
            detail:
                "A default gateway routes traffic off the local network. Without one, the PC cannot reach the internet. " +
                "This is reported as unavailable rather than healthy because the configuration is missing.",
            evidence: new[]
            {
                new DiagnosticEvidence
                {
                    Description = "Default Gateway",
                    Value = "None configured",
                    Source = "Network adapter default gateway"
                }
            },
            recommendations: new[]
            {
                new DiagnosticRecommendation
                {
                    Text = "Verify that the active network adapter obtains an IP address automatically (DHCP) or has a valid gateway configured.",
                    RequiresAdmin = true,
                    Priority = 1
                }
            },
            confidence: 0.9);
    }

    private static string BuildSummary(PCDiag.Net.GatewayHealth health, System.Net.IPAddress gateway, PCDiag.Net.PingMeasurementStats stats)
    {
        return health switch
        {
            PCDiag.Net.GatewayHealth.Unreachable => $"Default gateway {gateway} is unreachable.",
            PCDiag.Net.GatewayHealth.Lossy => $"Default gateway {gateway} is reachable but losing packets ({stats.LossRate:P0}).",
            PCDiag.Net.GatewayHealth.Slow => $"Default gateway {gateway} is reachable but latency is elevated ({stats.AvgLatencyMs:F0} ms avg).",
            _ => $"Default gateway {gateway} responded reliably with no significant loss."
        };
    }

    private static string BuildDetail(PCDiag.Net.GatewayHealth health, PCDiag.Net.PingMeasurementStats stats)
    {
        var parts = new List<string>
        {
            $"The default gateway was probed {stats.Attempts} time(s) over ICMP echo with a 32-byte payload. " +
            $"Replies: {stats.Successes}, failures: {stats.Failures}, timeouts: {stats.Timeouts}."
        };

        if (stats.AvgLatencyMs is double avg)
            parts.Add($"Latency: avg {avg:F0} ms, min {stats.MinLatencyMs} ms, max {stats.MaxLatencyMs} ms.");

        parts.Add(health switch
        {
            PCDiag.Net.GatewayHealth.Unreachable =>
                "No probe received a reply. Without a reachable gateway nothing can leave the local network.",
            PCDiag.Net.GatewayHealth.Lossy =>
                "A significant share of probes were lost. This indicates an unstable local link or gateway.",
            PCDiag.Net.GatewayHealth.Slow =>
                "Latency to the first hop is elevated even though no significant loss was observed.",
            _ => "Latency and loss are within expected bounds for a local first hop."
        });

        return string.Join(" ", parts);
    }

    private IReadOnlyList<DiagnosticEvidence> BuildEvidence(
        PCDiag.Inventory.NetworkAdapterInfo active,
        System.Net.IPAddress gateway,
        PCDiag.Net.PingMeasurementStats stats)
    {
        var evidence = new List<DiagnosticEvidence>
        {
            new()
            {
                Description = "Active Adapter",
                Value = $"{active.Name} ({string.Join(", ", active.IpAddresses)})",
                Source = "NetworkInterface"
            },
            new()
            {
                Description = "Default Gateway",
                Value = gateway.ToString(),
                Source = "Network adapter default gateway"
            },
            new()
            {
                Description = "Probes Sent",
                Value = stats.Attempts.ToString(),
                Source = "ICMP echo"
            },
            new()
            {
                Description = "Packet Loss",
                Value = $"{stats.LossRate:P0} ({stats.Failures + stats.Timeouts} of {stats.Attempts})",
                Source = "ICMP echo"
            }
        };

        if (stats.AvgLatencyMs is double avg)
        {
            evidence.Add(new DiagnosticEvidence
            {
                Description = "Latency",
                Value = $"avg {avg:F0} ms, min {stats.MinLatencyMs} ms, max {stats.MaxLatencyMs} ms",
                Source = "ICMP echo"
            });
        }

        evidence.Add(new DiagnosticEvidence
        {
            Description = "Probe Settings",
            Value = $"payload 32 bytes, timeout {_options.GatewayProbeTimeout.TotalMilliseconds:F0} ms",
            Source = "pcdiag settings"
        });

        return evidence;
    }

    private static IReadOnlyList<DiagnosticRecommendation> BuildRecommendations(
        PCDiag.Net.GatewayHealth health,
        System.Net.IPAddress gateway,
        PCDiag.Net.PingMeasurementStats stats)
    {
        return health switch
        {
            PCDiag.Net.GatewayHealth.Unreachable => new[]
            {
                new DiagnosticRecommendation
                {
                    Text = $"The default gateway ({gateway}) did not respond to any ICMP echo. Check cabling or Wi-Fi, restart the router, and verify the adapter obtains an IP address automatically (DHCP).",
                    RequiresAdmin = true,
                    Priority = 1
                }
            },
            PCDiag.Net.GatewayHealth.Lossy => new[]
            {
                new DiagnosticRecommendation
                {
                    Text = $"The default gateway is reachable but {stats.LossRate:P0} of probes were lost. Check Wi-Fi signal, interference, or local network congestion.",
                    RequiresAdmin = false,
                    Priority = 2
                }
            },
            PCDiag.Net.GatewayHealth.Slow => new[]
            {
                new DiagnosticRecommendation
                {
                    Text = $"Gateway latency is elevated (avg {stats.AvgLatencyMs:F0} ms). Check local congestion, Wi-Fi signal, or VPN overhead.",
                    RequiresAdmin = false,
                    Priority = 2
                }
            },
            _ => Array.Empty<DiagnosticRecommendation>()
        };
    }

    private static IReadOnlyList<string> PossibleCauses(PCDiag.Net.GatewayHealth health)
    {
        return health switch
        {
            PCDiag.Net.GatewayHealth.Unreachable => new[]
            {
                "Router is powered off, restarting, or failing.",
                "Network cable disconnected or Wi-Fi association lost.",
                "DHCP failed, leaving a stale or missing gateway.",
                "Local firewall or security software blocking ICMP."
            },
            PCDiag.Net.GatewayHealth.Lossy => new[]
            {
                "Poor Wi-Fi signal or interference.",
                "Local network congestion or an overloaded router.",
                "Faulty cable or port."
            },
            PCDiag.Net.GatewayHealth.Slow => new[]
            {
                "Local network congestion.",
                "Wi-Fi signal issues or VPN tunneling overhead."
            },
            _ => Array.Empty<string>()
        };
    }

    private static double ComputeConfidence(PCDiag.Net.GatewayHealth health, PCDiag.Net.PingMeasurementStats stats)
    {
        return health switch
        {
            PCDiag.Net.GatewayHealth.Unreachable => 0.85,
            PCDiag.Net.GatewayHealth.Lossy => Math.Min(0.85, 0.4 + 0.1 * stats.Attempts),
            PCDiag.Net.GatewayHealth.Slow => 0.7,
            _ => 0.8
        };
    }

    /// <summary>
    /// Fixes offered for a gateway finding. Unreachable or lossy gateways can benefit
    /// from renewing the DHCP lease and restarting the adapter; a reset of the Winsock
    /// catalog covers stale/corrupted stack state. Healthy results offer no fixes.
    /// </summary>
    public IReadOnlyList<DiagnosticFix> GetFixes(DiagnosticResult result)
    {
        if (result.Status != DiagnosticStatus.Finding || result.Severity < DiagnosticSeverity.Suspicious)
            return Array.Empty<DiagnosticFix>();

        var fixes = new List<DiagnosticFix>();
        var unreachable = result.Severity >= DiagnosticSeverity.Warning;
        var adapter = PCDiag.Fixes.NetworkFixHelpers.GetActiveAdapterName(result);
        var problem = unreachable
            ? "The default gateway is unreachable or losing packets."
            : "Latency to the default gateway is elevated.";

        if (adapter is not null)
            fixes.Add(new RestartNetworkAdapterFix(problem, adapter));
        fixes.Add(new DhcpRenewFix(problem));

        if (unreachable)
            fixes.Add(new WinsockResetFix(problem));

        return fixes;
    }
}