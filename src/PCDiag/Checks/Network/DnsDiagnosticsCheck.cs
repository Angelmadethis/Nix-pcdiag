using System.Net;
using PCDiag.Core;

namespace PCDiag.Checks.Network;

/// <summary>
/// Measures the responsiveness, latency, reliability, and reachability of the
/// configured DNS resolver(s) using multiple probes to safe test domains.
/// Latency alone never downgrades the verdict beyond "slow" and never triggers a
/// recommendation to change DNS; that requires reliability or reachability evidence.
/// Read-only: DNS settings are never modified.
/// </summary>
public sealed class DnsDiagnosticsCheck : DiagnosticCheck
{
    private readonly PCDiag.Dns.DnsOptions _options;
    private readonly PCDiag.Dns.IDnsTransport _transport;
    private readonly PCDiag.Dns.IDnsServerSource _serverSource;

    public override string CheckId => "NET-DNS-001";
    public override string Name => "DNS Resolution";
    public override DiagnosticCategory Category => DiagnosticCategory.Network;
    public override string Description =>
        "Measures configured DNS resolver responsiveness, latency, reliability, and reachability.";

    public DnsDiagnosticsCheck(
        PCDiag.Dns.DnsOptions? options = null,
        PCDiag.Dns.IDnsTransport? transport = null,
        PCDiag.Dns.IDnsServerSource? serverSource = null)
    {
        _options = options ?? PCDiag.Dns.DnsOptions.Default;
        _transport = transport ?? new PCDiag.Dns.UdpDnsTransport();
        _serverSource = serverSource ?? new PCDiag.Dns.WmiDnsServerSource();
    }

    private sealed record ResolverMeasurement(IPAddress Server, PCDiag.Dns.DnsMeasurementStats Stats, PCDiag.Dns.DnsHealth Health);

    private static readonly IReadOnlyList<string> CheckLimitations = new[]
    {
        "Measurements are point-in-time; a single run may not reflect sustained conditions.",
        "End-to-end latency includes recursion and resolver cache state, not just transport round-trip.",
        "Only A-record queries to the safe test domains example.com and example.org are used.",
        "No HTTPS/DoH/DoT or TCP/53 probing is performed."
    };

    protected override async Task<DiagnosticResult> RunAsync(DiagnosticContext context, CancellationToken cancellationToken)
    {
        var servers = _serverSource.GetServers();
        if (servers.Count == 0)
            return NoConfigurationResult();

        var resolvers = servers.Take(_options.MaxResolvers).ToList();
        var measurements = new List<ResolverMeasurement>(resolvers.Count);

        foreach (var server in resolvers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            measurements.Add(await MeasureAsync(server, cancellationToken).ConfigureAwait(false));
        }

        var overall = PCDiag.Dns.DnsClassifier.ClassifyOverall(measurements.Select(m => m.Health).ToList());
        var (severity, status) = MapHealth(overall);

        var evidence = new List<DiagnosticEvidence>();
        foreach (var m in measurements)
            evidence.Add(ResolverEvidence(m));

        evidence.Add(new DiagnosticEvidence
        {
            Description = "Configured DNS Servers",
            Value = string.Join(", ", resolvers),
            Source = "Win32_NetworkAdapterConfiguration"
        });
        evidence.Add(new DiagnosticEvidence
        {
            Description = "Test Domains",
            Value = string.Join(", ", _options.TestDomains),
            Source = "UDP DNS query (A record)"
        });
        evidence.Add(new DiagnosticEvidence
        {
            Description = "Probe Count per Resolver",
            Value = _options.ProbesPerResolver.ToString(),
            Source = "pcdiag settings"
        });
        evidence.Add(new DiagnosticEvidence
        {
            Description = "Probe Timeout",
            Value = $"{_options.ProbeTimeout.TotalMilliseconds:F0} ms",
            Source = "pcdiag settings"
        });

        var summary = BuildSummary(overall, measurements);
        var detail = BuildDetail(overall, measurements);

        return BuildResult(
            severity,
            status,
            summary,
            detail: detail,
            evidence: evidence,
            recommendations: BuildRecommendations(overall, measurements),
            possibleCauses: PossibleCauses(overall),
            limitations: CheckLimitations,
            confidence: ComputeConfidence(measurements));
    }

    private async Task<ResolverMeasurement> MeasureAsync(IPAddress server, CancellationToken cancellationToken)
    {
        var probes = new List<PCDiag.Dns.DnsProbeResult>(_options.ProbesPerResolver);
        var consecutiveTimeouts = 0;

        for (int i = 0; i < _options.ProbesPerResolver; i++)
        {
            var domain = _options.TestDomains[i % _options.TestDomains.Count];
            var probe = await _transport
                .ProbeAsync(server, domain, _options.ProbeTimeout, cancellationToken)
                .ConfigureAwait(false);

            probes.Add(probe);
            consecutiveTimeouts = probe.Outcome == PCDiag.Dns.DnsProbeOutcome.TimedOut ? consecutiveTimeouts + 1 : 0;

            if (consecutiveTimeouts >= _options.MaxTimeoutsBeforeAbort)
                break;
        }

        var stats = PCDiag.Dns.DnsMeasurementStats.Compute(probes);
        var health = PCDiag.Dns.DnsClassifier.Classify(stats, _options);
        return new ResolverMeasurement(server, stats, health);
    }

    private DiagnosticResult NoConfigurationResult()
    {
        return BuildResult(
            DiagnosticSeverity.Info,
            DiagnosticStatus.Unavailable,
            "No active DNS configuration was found.",
            detail:
                "No network adapter reported configured DNS servers, so the check could not measure any resolver. " +
                "This is reported as unavailable rather than healthy because DNS configuration is missing.",
            evidence: new[]
            {
                new DiagnosticEvidence
                {
                    Description = "Active DNS Servers",
                    Value = "None configured",
                    Source = "Win32_NetworkAdapterConfiguration"
                }
            },
            recommendations: new[]
            {
                new DiagnosticRecommendation
                {
                    Text = "Verify that the active network adapter is set to obtain DNS automatically (DHCP) or has a valid static DNS server configured.",
                    RequiresAdmin = true,
                    Priority = 1
                }
            },
            confidence: 0.9);
    }

    private static DiagnosticEvidence ResolverEvidence(ResolverMeasurement m)
    {
        var value = $"{m.Health} - attempts: {m.Stats.Attempts}, successes: {m.Stats.Successes}, " +
                    $"failures: {m.Stats.Failures}, timeouts: {m.Stats.Timeouts}";
        if (m.Stats.AvgLatencyMs is double avg)
            value += $", avg: {avg:F0} ms, min: {m.Stats.MinLatencyMs:F0} ms, max: {m.Stats.MaxLatencyMs:F0} ms";

        return new DiagnosticEvidence
        {
            Description = $"DNS Server {m.Server}",
            Value = value,
            Source = "UDP probe :53"
        };
    }

    private static string BuildSummary(PCDiag.Dns.DnsHealth overall, IReadOnlyList<ResolverMeasurement> measurements)
    {
        return overall switch
        {
            PCDiag.Dns.DnsHealth.Healthy =>
                $"All {measurements.Count} configured DNS resolver(s) responded reliably.",
            PCDiag.Dns.DnsHealth.Slow =>
                $"DNS resolution works but average latency is elevated ({MaxAvgLatency(measurements):F0} ms).",
            PCDiag.Dns.DnsHealth.Unreliable =>
                "One or more configured DNS resolvers are unreliable or unreachable.",
            PCDiag.Dns.DnsHealth.Unreachable =>
                "No configured DNS resolver responded to any query.",
            _ => "No active DNS configuration was found."
        };
    }

    private static string BuildDetail(PCDiag.Dns.DnsHealth overall, IReadOnlyList<ResolverMeasurement> measurements)
    {
        var parts = new List<string>
        {
            "Each configured resolver was probed multiple times against safe test domains (example.com, example.org). " +
            "A resolver counts a response as success only when it returns a well-formed reply with RCODE 0; non-zero " +
            "RCODE replies and socket errors count as failures, and no reply within the probe timeout counts as a timeout."
        };

        if (overall == PCDiag.Dns.DnsHealth.Slow)
        {
            parts.Add(
                "Latency is elevated but no failures or timeouts were observed. Elevated latency alone is not treated " +
                "as evidence that the DNS configuration is broken; local congestion or VPN overhead are common causes.");
        }
        else if (overall == PCDiag.Dns.DnsHealth.Unreliable)
        {
            parts.Add(
                "A configured resolver failed or timed out on a significant share of probes. This is evidence of an " +
                "unreliable DNS configuration, so a review of resolver reachability is warranted.");
        }
        else if (overall == PCDiag.Dns.DnsHealth.Unreachable)
        {
            parts.Add(
                "No configured resolver answered a single probe. DNS is effectively non-functional through the " +
                "configured servers.");
        }

        return string.Join(" ", parts);
    }

    private static IReadOnlyList<DiagnosticRecommendation> BuildRecommendations(
        PCDiag.Dns.DnsHealth overall,
        IReadOnlyList<ResolverMeasurement> measurements)
    {
        var failed = measurements
            .Where(m => m.Health is PCDiag.Dns.DnsHealth.Unreliable or PCDiag.Dns.DnsHealth.Unreachable)
            .Select(m => m.Server.ToString())
            .ToList();

        return overall switch
        {
            PCDiag.Dns.DnsHealth.Unreachable => new[]
            {
                new DiagnosticRecommendation
                {
                    Text = $"None of the configured DNS resolver(s) ({string.Join(", ", measurements.Select(m => m.Server))}) responded to any query. " +
                           "Verify network connectivity and that outbound UDP/53 is not blocked by a firewall. If the resolver remains unreachable, " +
                           "switching DNS servers is supported by this evidence.",
                    RequiresAdmin = true,
                    Priority = 1
                }
            },
            PCDiag.Dns.DnsHealth.Unreliable => new[]
            {
                new DiagnosticRecommendation
                {
                    Text = $"The resolver(s) {string.Join(", ", failed)} are unreliable: they failed or timed out on a " +
                           "significant share of probes. Verify UDP/53 reachability and the resolver address; if failures " +
                           "persist, consider switching to the ISP-provided or a well-known public resolver (e.g., 1.1.1.1 or 8.8.8.8).",
                    RequiresAdmin = true,
                    Priority = 1
                }
            },
            PCDiag.Dns.DnsHealth.Slow => new[]
            {
                new DiagnosticRecommendation
                {
                    Text = "Average DNS resolution latency is elevated. Latency alone is not sufficient evidence to change DNS servers - " +
                           "check for local network congestion, Wi-Fi signal quality, or VPN overhead first.",
                    RequiresAdmin = false,
                    Priority = 2
                }
            },
            _ => Array.Empty<DiagnosticRecommendation>()
        };
    }

    private static IReadOnlyList<string> PossibleCauses(PCDiag.Dns.DnsHealth overall)
    {
        return overall switch
        {
            PCDiag.Dns.DnsHealth.Unreachable or PCDiag.Dns.DnsHealth.Unreliable => new[]
            {
                "Resolver address is stale, mistyped, or no longer valid.",
                "Outbound UDP/53 blocked by a firewall, router, or ISP.",
                "Network adapter or VPN routing issue.",
                "Resolver service is down or overloaded."
            },
            PCDiag.Dns.DnsHealth.Slow => new[]
            {
                "Local network congestion or poor Wi-Fi signal.",
                "VPN tunneling overhead.",
                "Resolver upstream latency or cold cache."
            },
            _ => Array.Empty<string>()
        };
    }

    private static (DiagnosticSeverity Severity, DiagnosticStatus Status) MapHealth(PCDiag.Dns.DnsHealth health)
    {
        return health switch
        {
            PCDiag.Dns.DnsHealth.Healthy => (DiagnosticSeverity.Healthy, DiagnosticStatus.Passed),
            PCDiag.Dns.DnsHealth.Slow => (DiagnosticSeverity.Suspicious, DiagnosticStatus.Finding),
            PCDiag.Dns.DnsHealth.Unreliable => (DiagnosticSeverity.Warning, DiagnosticStatus.Finding),
            PCDiag.Dns.DnsHealth.Unreachable => (DiagnosticSeverity.Critical, DiagnosticStatus.Finding),
            _ => (DiagnosticSeverity.Info, DiagnosticStatus.Unavailable)
        };
    }

    private static double ComputeConfidence(IReadOnlyList<ResolverMeasurement> measurements)
    {
        if (measurements.Count == 0)
            return 0.9;

        var minAttempts = measurements.Min(m => m.Stats.Attempts);
        return Math.Min(0.95, 0.5 + 0.08 * minAttempts);
    }

    private static double MaxAvgLatency(IReadOnlyList<ResolverMeasurement> measurements)
        => measurements
            .Where(m => m.Stats.AvgLatencyMs is not null)
            .Select(m => m.Stats.AvgLatencyMs!.Value)
            .DefaultIfEmpty(0)
            .Max();
}