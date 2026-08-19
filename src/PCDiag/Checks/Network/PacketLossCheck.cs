using System.Net;
using PCDiag.Core;

namespace PCDiag.Checks.Network;

/// <summary>
/// Measures basic packet loss and latency to the default gateway and to a small set
/// of internet endpoints. Conservative probe counts and timeouts. Read-only.
/// </summary>
public sealed class PacketLossCheck : DiagnosticCheck
{
    private readonly PCDiag.Net.NetOptions _options;
    private readonly PCDiag.Net.IPingProbe _probe;
    private readonly IReadOnlyList<string> _targetOverrides;

    public override string CheckId => "NET-LOSS-001";
    public override string Name => "Packet Loss & Latency";
    public override DiagnosticCategory Category => DiagnosticCategory.Network;
    public override string Description =>
        "Measures packet loss and latency to the default gateway and to configurable internet endpoints.";

    public PacketLossCheck(
        PCDiag.Net.NetOptions? options = null,
        PCDiag.Net.IPingProbe? probe = null,
        IReadOnlyList<string>? targetOverrides = null)
    {
        _options = options ?? PCDiag.Net.NetOptions.Default;
        _probe = probe ?? new PCDiag.Net.SystemPingProbe();
        _targetOverrides = targetOverrides ?? Array.Empty<string>();
    }

    private static readonly IReadOnlyList<string> CheckLimitations = new[]
    {
        "Measurements are point-in-time; a single run may not reflect sustained conditions.",
        "Internet endpoints are probed over ICMP echo; some networks block or rate-limit ICMP, which can appear as loss or unreachability.",
        "Latency includes the full path (Wi-Fi, ISP routing, endpoint load), not just the local link.",
        "UDP/TCP connectivity is not tested here; use 'pcdiag check dns' for application-level connectivity."
    };

    private sealed record TargetMeasurement(IPAddress Target, bool IsGateway, PCDiag.Net.PingMeasurementStats Stats, PCDiag.Net.PacketLossHealth Health);

    protected override async Task<DiagnosticResult> RunAsync(DiagnosticContext context, CancellationToken cancellationToken)
    {
        var active = context.Inventory?.Network.ActiveConnection;
        if (active is null)
            return NoActiveConnectionResult();

        var gateway = PCDiag.Net.TargetResolver.FirstGateway(active.GatewayAddresses);
        if (gateway is null)
            return NoGatewayResult();

        var configuredTargets = _targetOverrides.Count > 0 ? _targetOverrides : _options.DefaultInternetTargets;
        var internetTargets = await PCDiag.Net.TargetResolver
            .ResolveAsync(configuredTargets, _options.MaxInternetTargets, cancellationToken)
            .ConfigureAwait(false);

        var measurements = new List<TargetMeasurement>();
        measurements.Add(await MeasureGatewayAsync(gateway, cancellationToken).ConfigureAwait(false));
        foreach (var target in internetTargets)
            measurements.Add(await MeasureInternetAsync(target, cancellationToken).ConfigureAwait(false));

        var internetHealths = measurements.Where(m => !m.IsGateway).Select(m => m.Health).ToList();
        var gatewayHealth = measurements.First(m => m.IsGateway).Health;
        var overall = PCDiag.Net.PacketLossClassifier.ClassifyOverall(gatewayHealth, internetHealths);
        var (severity, status) = MapHealth(overall);

        return BuildResult(
            severity,
            status,
            BuildSummary(overall, measurements),
            detail: BuildDetail(overall, measurements),
            evidence: BuildEvidence(measurements, configuredTargets),
            recommendations: BuildRecommendations(overall, measurements),
            possibleCauses: PossibleCauses(overall),
            limitations: CheckLimitations,
            confidence: ComputeConfidence(overall, measurements));
    }

    private async Task<TargetMeasurement> MeasureGatewayAsync(IPAddress gateway, CancellationToken cancellationToken)
    {
        var probes = await PCDiag.Net.ProbeRunner.ProbeAsync(
            _probe, gateway, _options.GatewayProbes, _options.GatewayProbeTimeout,
            _options.MaxTimeoutsBeforeAbort, payloadBytes: 32, dontFragment: false, cancellationToken).ConfigureAwait(false);

        var stats = PCDiag.Net.PingMeasurementStats.Compute(probes);
        var health = PCDiag.Net.PacketLossClassifier.ClassifyTarget(stats, _options, _options.GatewaySlowLatencyMs);
        return new TargetMeasurement(gateway, IsGateway: true, stats, health);
    }

    private async Task<TargetMeasurement> MeasureInternetAsync(IPAddress target, CancellationToken cancellationToken)
    {
        var probes = await PCDiag.Net.ProbeRunner.ProbeAsync(
            _probe, target, _options.InternetProbes, _options.InternetProbeTimeout,
            _options.MaxTimeoutsBeforeAbort, payloadBytes: 32, dontFragment: false, cancellationToken).ConfigureAwait(false);

        var stats = PCDiag.Net.PingMeasurementStats.Compute(probes);
        var health = PCDiag.Net.PacketLossClassifier.ClassifyTarget(stats, _options, _options.InternetSlowLatencyMs);
        return new TargetMeasurement(target, IsGateway: false, stats, health);
    }

    private static (DiagnosticSeverity Severity, DiagnosticStatus Status) MapHealth(PCDiag.Net.PacketLossHealth health)
    {
        return health switch
        {
            PCDiag.Net.PacketLossHealth.Unreachable => (DiagnosticSeverity.Critical, DiagnosticStatus.Finding),
            PCDiag.Net.PacketLossHealth.InternetUnreachable => (DiagnosticSeverity.Warning, DiagnosticStatus.Finding),
            PCDiag.Net.PacketLossHealth.Lossy => (DiagnosticSeverity.Warning, DiagnosticStatus.Finding),
            PCDiag.Net.PacketLossHealth.Elevated => (DiagnosticSeverity.Suspicious, DiagnosticStatus.Finding),
            PCDiag.Net.PacketLossHealth.Slow => (DiagnosticSeverity.Suspicious, DiagnosticStatus.Finding),
            _ => (DiagnosticSeverity.Healthy, DiagnosticStatus.Passed)
        };
    }

    private DiagnosticResult NoActiveConnectionResult()
    {
        return BuildResult(
            DiagnosticSeverity.Info,
            DiagnosticStatus.Unavailable,
            "No active network connection was found, so packet loss could not be measured.",
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
            "No default gateway is configured, so packet loss to the local network could not be measured.",
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

    private static string BuildSummary(PCDiag.Net.PacketLossHealth overall, IReadOnlyList<TargetMeasurement> measurements)
    {
        var internet = measurements.Where(m => !m.IsGateway).ToList();
        return overall switch
        {
            PCDiag.Net.PacketLossHealth.Unreachable =>
                "The default gateway is unreachable; there is no working path to the internet.",
            PCDiag.Net.PacketLossHealth.InternetUnreachable =>
                "The gateway responds but every tested internet endpoint is unreachable (ICMP echo may be blocked).",
            PCDiag.Net.PacketLossHealth.Lossy =>
                "Packet loss is significant on one or more tested paths.",
            PCDiag.Net.PacketLossHealth.Elevated =>
                "Low but notable packet loss was observed on one or more tested paths.",
            PCDiag.Net.PacketLossHealth.Slow =>
                $"Latency is elevated (internet avg up to {MaxInternetAvg(internet):F0} ms).",
            _ => "No significant packet loss or latency problems detected on the tested paths."
        };
    }

    private static string BuildDetail(PCDiag.Net.PacketLossHealth overall, IReadOnlyList<TargetMeasurement> measurements)
    {
        var parts = new List<string>
        {
            "The default gateway and up to two internet endpoints were probed with a 32-byte ICMP echo payload. " +
            "Per-target attempts, replies, loss, and latency are listed in the evidence."
        };

        parts.Add(overall switch
        {
            PCDiag.Net.PacketLossHealth.Unreachable =>
                "No probe to the gateway received a reply, so nothing can leave the local network.",
            PCDiag.Net.PacketLossHealth.InternetUnreachable =>
                "The gateway is reachable but every internet endpoint timed out. This can mean the ISP connection is down, " +
                "or that ICMP echo is blocked on the path - confirm application-level connectivity with 'pcdiag check dns'.",
            PCDiag.Net.PacketLossHealth.Lossy =>
                "At least one tested path lost 20% or more of its probes, which is consistent with an unstable link or congestion.",
            PCDiag.Net.PacketLossHealth.Elevated =>
                "At least one tested path lost between 5% and 20% of its probes - worth investigating but not severe.",
            PCDiag.Net.PacketLossHealth.Slow =>
                "Latency is elevated even though loss is not; local congestion, Wi-Fi, or VPN overhead are common causes.",
            _ => "Loss is below 5% on every tested path and latency is within normal bounds."
        });

        return string.Join(" ", parts);
    }

    private IReadOnlyList<DiagnosticEvidence> BuildEvidence(
        IReadOnlyList<TargetMeasurement> measurements,
        IReadOnlyList<string> configuredTargets)
    {
        var evidence = new List<DiagnosticEvidence>();
        foreach (var m in measurements)
        {
            var label = m.IsGateway ? $"Gateway {m.Target}" : $"Internet {m.Target}";
            var value = $"{m.Health} - attempts: {m.Stats.Attempts}, replies: {m.Stats.Successes}, loss: {m.Stats.LossRate:P0}";
            if (m.Stats.AvgLatencyMs is double avg)
                value += $", avg: {avg:F0} ms, min: {m.Stats.MinLatencyMs} ms, max: {m.Stats.MaxLatencyMs} ms";

            evidence.Add(new DiagnosticEvidence
            {
                Description = label,
                Value = value,
                Source = m.IsGateway ? "ICMP echo" : "ICMP echo"
            });
        }

        evidence.Add(new DiagnosticEvidence
        {
            Description = "Test Targets",
            Value = configuredTargets.Count > 0 ? string.Join(", ", configuredTargets) : "default endpoints",
            Source = "pcdiag settings"
        });
        evidence.Add(new DiagnosticEvidence
        {
            Description = "Probe Settings",
            Value = $"gateway: {_options.GatewayProbes} x {_options.GatewayProbeTimeout.TotalMilliseconds:F0} ms; " +
                    $"internet: {_options.InternetProbes} x {_options.InternetProbeTimeout.TotalMilliseconds:F0} ms; payload 32 bytes",
            Source = "pcdiag settings"
        });

        return evidence;
    }

    private static IReadOnlyList<DiagnosticRecommendation> BuildRecommendations(
        PCDiag.Net.PacketLossHealth overall,
        IReadOnlyList<TargetMeasurement> measurements)
    {
        var gateway = measurements.FirstOrDefault(m => m.IsGateway);
        return overall switch
        {
            PCDiag.Net.PacketLossHealth.Unreachable => new[]
            {
                new DiagnosticRecommendation
                {
                    Text = $"The default gateway ({gateway?.Target}) did not respond to any probe. Check cabling or Wi-Fi, restart the router, and verify the adapter obtains an IP address automatically (DHCP).",
                    RequiresAdmin = true,
                    Priority = 1
                }
            },
            PCDiag.Net.PacketLossHealth.InternetUnreachable => new[]
            {
                new DiagnosticRecommendation
                {
                    Text = "The gateway is reachable but internet endpoints are not. ICMP echo may be blocked by a firewall or ISP; confirm application-level connectivity with 'pcdiag check dns' (UDP/53).",
                    RequiresAdmin = false,
                    Priority = 1
                }
            },
            PCDiag.Net.PacketLossHealth.Lossy => new[]
            {
                new DiagnosticRecommendation
                {
                    Text = "Significant packet loss was detected. Check Wi-Fi signal, local congestion, and router health; if loss persists on a wired connection, contact the ISP.",
                    RequiresAdmin = false,
                    Priority = 2
                }
            },
            PCDiag.Net.PacketLossHealth.Elevated => new[]
            {
                new DiagnosticRecommendation
                {
                    Text = "Notable packet loss was detected. Check Wi-Fi signal and local congestion; re-run the check to confirm whether it is sustained.",
                    RequiresAdmin = false,
                    Priority = 2
                }
            },
            PCDiag.Net.PacketLossHealth.Slow => new[]
            {
                new DiagnosticRecommendation
                {
                    Text = "Elevated latency was detected. Check for VPN tunneling overhead, local congestion, or Wi-Fi signal issues.",
                    RequiresAdmin = false,
                    Priority = 2
                }
            },
            _ => Array.Empty<DiagnosticRecommendation>()
        };
    }

    private static IReadOnlyList<string> PossibleCauses(PCDiag.Net.PacketLossHealth overall)
    {
        return overall switch
        {
            PCDiag.Net.PacketLossHealth.Unreachable => new[]
            {
                "Router is off, restarting, or failing.",
                "Network cable disconnected or Wi-Fi association lost.",
                "DHCP failed, leaving a stale or missing gateway."
            },
            PCDiag.Net.PacketLossHealth.InternetUnreachable => new[]
            {
                "ISP connection is down or saturated.",
                "ICMP echo blocked by a firewall, router, or ISP.",
                "Internet endpoint is unreachable from this network."
            },
            PCDiag.Net.PacketLossHealth.Lossy or PCDiag.Net.PacketLossHealth.Elevated => new[]
            {
                "Poor Wi-Fi signal or interference.",
                "Local network congestion or an overloaded router.",
                "Faulty cable, port, or network device."
            },
            PCDiag.Net.PacketLossHealth.Slow => new[]
            {
                "VPN tunneling overhead.",
                "Local congestion or Wi-Fi signal issues.",
                "ISP routing or remote endpoint load."
            },
            _ => Array.Empty<string>()
        };
    }

    private static double MaxInternetAvg(IReadOnlyList<TargetMeasurement> internet)
        => internet
            .Where(m => m.Stats.AvgLatencyMs is not null)
            .Select(m => m.Stats.AvgLatencyMs!.Value)
            .DefaultIfEmpty(0)
            .Max();

    private static double ComputeConfidence(PCDiag.Net.PacketLossHealth overall, IReadOnlyList<TargetMeasurement> measurements)
    {
        var minAttempts = measurements.Select(m => m.Stats.Attempts).DefaultIfEmpty(0).Min();
        return overall switch
        {
            PCDiag.Net.PacketLossHealth.Unreachable => 0.85,
            PCDiag.Net.PacketLossHealth.InternetUnreachable => 0.6,
            _ => Math.Min(0.85, 0.45 + 0.08 * minAttempts)
        };
    }
}