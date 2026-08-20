using PCDiag.Core;

namespace PCDiag.Checks.Network;

/// <summary>
/// Reviews TCP behavior: retransmission indicators, failed connection attempts, TCP
/// configuration, receive-window auto-tuning state, and adapter resets/errors - all
/// interpreted in context (ratios, rates) rather than as bare counts. Read-only: no
/// TCP registry values are read for modification, and none are ever written.
/// </summary>
public sealed class TcpHealthCheck : DiagnosticCheck
{
    private readonly PCDiag.Net.Tcp.TcpOptions _options;
    private readonly PCDiag.Net.Tcp.ITcpStatsSource _statsSource;
    private readonly PCDiag.Net.Tcp.ITcpConfigSource _configSource;
    private readonly PCDiag.Net.Tcp.ITcpAdapterErrorSource _adapterErrorSource;

    public override string CheckId => "NET-TCP-001";
    public override string Name => "TCP Configuration & Statistics";
    public override DiagnosticCategory Category => DiagnosticCategory.Network;
    public override string Description =>
        "Checks TCP retransmissions, connection failures, configuration, auto-tuning, and adapter errors.";

    public TcpHealthCheck(
        PCDiag.Net.Tcp.TcpOptions? options = null,
        PCDiag.Net.Tcp.ITcpStatsSource? statsSource = null,
        PCDiag.Net.Tcp.ITcpConfigSource? configSource = null,
        PCDiag.Net.Tcp.ITcpAdapterErrorSource? adapterErrorSource = null)
    {
        _options = options ?? PCDiag.Net.Tcp.TcpOptions.Default;
        _statsSource = statsSource ?? new PCDiag.Net.Tcp.NetTcpStatsSource();
        _configSource = configSource ?? new PCDiag.Net.Tcp.WmiTcpConfigSource();
        _adapterErrorSource = adapterErrorSource ?? new PCDiag.Net.Tcp.WmiTcpAdapterErrorSource();
    }

    private static readonly IReadOnlyList<string> CheckLimitations = new[]
    {
        "Statistics are cumulative since boot; a single run cannot distinguish a brief burst from a sustained problem.",
        "Connection failures include attempts to dead hosts and blocked ports, which browsers generate routinely.",
        "Retransmission counters come from the TCPv4 perf counters; if they are unavailable the value is reported as such.",
        "The check is read-only; no TCP registry values are changed and no tweaks are recommended as automated fixes."
    };

    protected override Task<DiagnosticResult> RunAsync(DiagnosticContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var stats = _statsSource.GetStats();
        var config = _configSource.GetConfig();

        var active = context.Inventory?.Network.ActiveConnection;
        var adapterErrors = _adapterErrorSource.GetFor(active?.Name, active?.Description);

        var uptime = context.Inventory?.Windows?.Uptime;
        double? adapterErrorRate = null;
        if (adapterErrors is not null && uptime is TimeSpan up && up.TotalSeconds > 0)
            adapterErrorRate = adapterErrors.TotalErrors / up.TotalSeconds;

        var assessment = PCDiag.Net.Tcp.TcpHealthClassifier.Classify(stats, config, adapterErrorRate, _options);
        var (severity, status) = MapVerdict(assessment.Verdict);

        return Task.FromResult(BuildResult(
            severity,
            status,
            BuildSummary(assessment),
            detail: BuildDetail(assessment, stats, config, adapterErrors, uptime),
            evidence: BuildEvidence(stats, config, adapterErrors, adapterErrorRate, uptime, active),
            recommendations: BuildRecommendations(assessment),
            possibleCauses: PossibleCauses(assessment),
            limitations: CheckLimitations,
            confidence: ComputeConfidence(assessment)));
    }

    private static (DiagnosticSeverity Severity, DiagnosticStatus Status) MapVerdict(PCDiag.Net.Tcp.TcpHealthVerdict verdict)
    {
        return verdict switch
        {
            PCDiag.Net.Tcp.TcpHealthVerdict.Warning => (DiagnosticSeverity.Warning, DiagnosticStatus.Finding),
            PCDiag.Net.Tcp.TcpHealthVerdict.Suspicious => (DiagnosticSeverity.Suspicious, DiagnosticStatus.Finding),
            _ => (DiagnosticSeverity.Healthy, DiagnosticStatus.Passed)
        };
    }

    private static string BuildSummary(PCDiag.Net.Tcp.TcpHealthAssessment assessment)
    {
        if (assessment.Verdict == PCDiag.Net.Tcp.TcpHealthVerdict.Healthy)
            return "TCP statistics and configuration look normal.";

        var notes = new List<string>();
        if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.RetransmissionHigh))
            notes.Add("a high segment retransmission rate");
        else if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.RetransmissionElevated))
            notes.Add("an elevated segment retransmission rate");

        if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.ConnectionFailuresHigh))
            notes.Add("a high share of failed connection attempts");
        else if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.ConnectionFailuresElevated))
            notes.Add("a notable share of failed connection attempts");

        if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.AutotuningDisabled))
            notes.Add("Receive Window Auto-Tuning is disabled (non-default)");
        else if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.AutotuningRestricted))
            notes.Add("a non-default Receive Window Auto-Tuning level");

        if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.WindowSizeOverridesAutotuning))
            notes.Add("TCP window-size registry values that disable auto-tuning are set");

        if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.TcpTimedWaitDelayLow))
            notes.Add("TcpTimedWaitDelay is set unusually low");

        if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.MaxUserPortLow))
            notes.Add("MaxUserPort is set below 5000, limiting concurrent connections");

        if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.AdapterErrorsHigh))
            notes.Add("the network adapter is accumulating errors at a high rate");
        else if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.AdapterErrorsElevated))
            notes.Add("the network adapter is accumulating errors");

        return notes.Count == 0
            ? "TCP statistics show some unusual values that are worth investigating."
            : "TCP statistics and configuration show: " + string.Join("; ", notes) + ".";
    }

    private static string BuildDetail(
        PCDiag.Net.Tcp.TcpHealthAssessment assessment,
        PCDiag.Net.Tcp.TcpCumulativeStats stats,
        PCDiag.Net.Tcp.TcpConfiguration config,
        PCDiag.Net.Tcp.TcpAdapterErrorStats? adapterErrors,
        TimeSpan? uptime)
    {
        var parts = new List<string>
        {
            "TCP behavior is judged by ratios and rates so values are meaningful in context. " +
            "Connection failures are compared with how many connections were actually initiated; retransmissions are " +
            "compared with the total segments sent and received. Counters are cumulative since boot."
        };

        if (stats.FailureRatio is double fr)
            parts.Add($"Connection failures: {stats.ConnectionFailures:N0} of {stats.ConnectionsInitiated:N0} initiations ({fr:P0}).");
        else if (stats.ConnectionsInitiated == 0)
            parts.Add("No outbound connections have been initiated since boot, so no failure ratio can be computed.");

        if (stats.RetransmissionRatio is double rr)
            parts.Add($"Segment retransmissions: {stats.SegmentsRetransmitted:N0} of {stats.SegmentsSent + stats.SegmentsReceived:N0} segments ({rr:P0}).");
        else if (stats.SegmentsSent + stats.SegmentsReceived == 0)
            parts.Add("No TCP segments have been sent or received since boot, so the retransmission rate is unavailable.");

        if (adapterErrors is not null)
        {
            parts.Add(uptime is TimeSpan up && up.TotalSeconds > 0
                ? $"Network adapter {adapterErrors.InstanceName}: {adapterErrors.TotalErrors:N0} errors and {adapterErrors.TotalDiscards:N0} discards since boot (avg {adapterErrors.TotalErrors / up.TotalSeconds:F3}/s)."
                : $"Network adapter {adapterErrors.InstanceName}: {adapterErrors.TotalErrors:N0} errors and {adapterErrors.TotalDiscards:N0} discards since boot.");
        }
        else
        {
            parts.Add("Network adapter error counters are not available on this system.");
        }

        parts.Add(
            "Registry values under Tcpip\\Parameters are reported only if they are actually set; unset values mean " +
            "Windows defaults are in effect. This check never writes TCP registry values.");

        return string.Join(" ", parts);
    }

    private IReadOnlyList<DiagnosticEvidence> BuildEvidence(
        PCDiag.Net.Tcp.TcpCumulativeStats stats,
        PCDiag.Net.Tcp.TcpConfiguration config,
        PCDiag.Net.Tcp.TcpAdapterErrorStats? adapterErrors,
        double? adapterErrorRate,
        TimeSpan? uptime,
        PCDiag.Inventory.NetworkAdapterInfo? active)
    {
        var evidence = new List<DiagnosticEvidence>();

        if (active is not null)
        {
            evidence.Add(new DiagnosticEvidence
            {
                Description = "Active Adapter",
                Value = $"{active.Name} ({string.Join(", ", active.IpAddresses)})",
                Source = "SystemInventory.Network"
            });
        }

        evidence.Add(new DiagnosticEvidence
        {
            Description = "Connection Failures",
            Value = $"{stats.ConnectionFailures:N0} of {stats.ConnectionsInitiated:N0} initiations" +
                    (stats.FailureRatio is double fr ? $" ({fr:P0})" : " (ratio unavailable)"),
            Source = "GetTcpIPv4Statistics / perf counters"
        });
        evidence.Add(new DiagnosticEvidence
        {
            Description = "TCP Resets",
            Value = $"{stats.ResetsSent:N0} sent, {stats.ResetsReceived:N0} received",
            Source = "GetTcpIPv4Statistics / Win32_PerfRawData_Tcpip_TCPv4"
        });
        evidence.Add(new DiagnosticEvidence
        {
            Description = "Segments",
            Value = $"{stats.SegmentsSent:N0} sent, {stats.SegmentsReceived:N0} received, {stats.SegmentsRetransmitted:N0} retransmitted" +
                    (stats.RetransmissionRatio is double rr ? $" ({rr:P0} retransmit rate)" : " (retransmit rate unavailable)"),
            Source = "Win32_PerfRawData_Tcpip_TCPv4"
        });

        if (adapterErrors is not null)
        {
            var rateText = adapterErrorRate is double rate ? $"; avg {rate:F3} errors/s since boot" : "";
            evidence.Add(new DiagnosticEvidence
            {
                Description = "Adapter Errors",
                Value = $"{adapterErrors.InstanceName}: {adapterErrors.TotalErrors:N0} errors, {adapterErrors.TotalDiscards:N0} discards since boot{rateText}",
                Source = "Win32_PerfRawData_Tcpip_NetworkInterface"
            });
        }
        else
        {
            evidence.Add(new DiagnosticEvidence
            {
                Description = "Adapter Errors",
                Value = "Not available (perf counters missing or no adapter matched)",
                Source = "Win32_PerfRawData_Tcpip_NetworkInterface"
            });
        }

        evidence.Add(new DiagnosticEvidence
        {
            Description = "Receive Window Auto-Tuning",
            Value = AutotuningText(config.AutotuningLevel),
            ExpectedValue = "Normal (default)",
            Source = "MSFT_NetTCPSetting"
        });
        if (config.AutotuningGroupPolicy != PCDiag.Net.Tcp.TcpAutotuningLevel.Unknown)
        {
            evidence.Add(new DiagnosticEvidence
            {
                Description = "Auto-Tuning Group Policy",
                Value = AutotuningText(config.AutotuningGroupPolicy),
                Source = "MSFT_NetTCPSetting"
            });
        }

        evidence.Add(new DiagnosticEvidence
        {
            Description = "Dynamic Port Range",
            Value = config.DynamicPortStart is int start && config.DynamicPortCount is int count
                ? $"{start}-{start + count - 1} ({count} ports)"
                : "unknown",
            Source = "MSFT_NetTCPSetting"
        });

        evidence.AddRange(new[]
        {
            RegistryEvidence("TcpTimedWaitDelay", config.TcpTimedWaitDelay, "seconds", ">= 30 recommended"),
            RegistryEvidence("TcpNumConnections", config.TcpNumConnections, null, null),
            RegistryEvidence("TcpMaxDataRetransmissions", config.TcpMaxDataRetransmissions, null, null),
            RegistryEvidence("MaxUserPort", config.MaxUserPort, null, ">= 5000 recommended"),
            RegistryEvidence("GlobalMaxTcpWindowSize", config.GlobalMaxTcpWindowSize, "bytes", "not set (default)"),
            RegistryEvidence("TcpWindowSize", config.TcpWindowSize, "bytes", "not set (default)")
        });

        evidence.Add(new DiagnosticEvidence
        {
            Description = "Threshold Reference",
            Value =
                $"retransmissions: suspicious >= {_options.RetransmissionSuspiciousRatio:P0}, warning >= {_options.RetransmissionWarningRatio:P0} of segments; " +
                $"connection failures: suspicious >= {_options.FailureSuspiciousRatio:P0}, warning >= {_options.FailureWarningRatio:P0} of initiations; " +
                $"adapter errors: suspicious >= {_options.AdapterErrorSuspiciousPerSecond:F2}/s, warning >= {_options.AdapterErrorWarningPerSecond:F2}/s; " +
                $"autotuning: Normal expected, Disabled/Restricted/Experimental flagged; MaxUserPort < 5000 and TcpTimedWaitDelay < 30 flagged",
            Source = "documented in SPEC.md Phase 6"
        });

        return evidence;
    }

    private static DiagnosticEvidence RegistryEvidence(string name, int? value, string? unit, string? expected)
    {
        var text = value is int v ? (unit is null ? v.ToString() : $"{v} {unit}") : "not set (Windows default)";
        return new DiagnosticEvidence
        {
            Description = $"TCP Config - {name}",
            Value = text,
            ExpectedValue = expected,
            Source = "Tcpip\\Parameters (registry)"
        };
    }

    private static string AutotuningText(PCDiag.Net.Tcp.TcpAutotuningLevel level)
    {
        return level switch
        {
            PCDiag.Net.Tcp.TcpAutotuningLevel.Normal => "Normal",
            PCDiag.Net.Tcp.TcpAutotuningLevel.Experimental => "Experimental",
            PCDiag.Net.Tcp.TcpAutotuningLevel.Restricted => "Restricted",
            PCDiag.Net.Tcp.TcpAutotuningLevel.HighlyRestricted => "Highly Restricted",
            PCDiag.Net.Tcp.TcpAutotuningLevel.Disabled => "Disabled",
            _ => "Unknown"
        };
    }

    private static IReadOnlyList<DiagnosticRecommendation> BuildRecommendations(PCDiag.Net.Tcp.TcpHealthAssessment assessment)
    {
        if (assessment.Verdict == PCDiag.Net.Tcp.TcpHealthVerdict.Healthy)
            return Array.Empty<DiagnosticRecommendation>();

        var recommendations = new List<DiagnosticRecommendation>();
        if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.AutotuningDisabled)
            || assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.AutotuningRestricted))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "Receive Window Auto-Tuning is at a non-default level. If throughput on high-latency links is impaired, the supported fix is to restore the default (netsh interface tcp set global autotuninglevel=normal). This check does not change the setting.",
                RequiresAdmin = true,
                Priority = 2
            });
        }
        if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.WindowSizeOverridesAutotuning))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "TCP window-size registry values (TcpWindowSize/GlobalMaxTcpWindowSize) are set, which disables auto-tuning. If they were not set intentionally, remove them and let Windows auto-tune.",
                RequiresAdmin = true,
                Priority = 2
            });
        }
        if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.MaxUserPortLow))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "MaxUserPort is set below 5000, which limits how many concurrent outbound connections Windows can open. If it was not set intentionally, restoring the default is recommended.",
                RequiresAdmin = true,
                Priority = 1
            });
        }
        if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.TcpTimedWaitDelayLow))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "TcpTimedWaitDelay is set below 30 seconds. Very low values shorten the TIME_WAIT hold but can cause port-reuse issues. Verify this was set intentionally.",
                RequiresAdmin = true,
                Priority = 2
            });
        }
        if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.RetransmissionHigh)
            || assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.RetransmissionElevated))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "The segment retransmission rate is elevated, which usually points to packet loss on the path. Check Wi-Fi signal, local congestion, and re-run to see whether it is sustained.",
                RequiresAdmin = false,
                Priority = 2
            });
        }
        if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.ConnectionFailuresHigh)
            || assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.ConnectionFailuresElevated))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "A notable share of outbound connection attempts are failing. Some failures to dead hosts or blocked ports are normal; check whether specific applications are affected and re-run the check.",
                RequiresAdmin = false,
                Priority = 2
            });
        }
        if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.AdapterErrorsHigh)
            || assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.AdapterErrorsElevated))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "The network adapter has accumulated a notable number of errors. Check cabling, Wi-Fi signal, and whether the driver is current.",
                RequiresAdmin = false,
                Priority = 2
            });
        }

        return recommendations;
    }

    private static IReadOnlyList<string> PossibleCauses(PCDiag.Net.Tcp.TcpHealthAssessment assessment)
    {
        var causes = new List<string>();
        if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.RetransmissionHigh)
            || assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.RetransmissionElevated)
            || assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.AdapterErrorsHigh)
            || assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.AdapterErrorsElevated))
        {
            causes.Add("Packet loss: poor Wi-Fi signal, congestion, or a faulty cable/port.");
            causes.Add("An outdated or misbehaving network driver.");
        }
        if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.ConnectionFailuresHigh)
            || assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.ConnectionFailuresElevated))
        {
            causes.Add("Connections to dead hosts or ports blocked by a firewall/ISP (partly normal).");
            causes.Add("Intermittent connectivity or a flapping link.");
        }
        if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.AutotuningDisabled)
            || assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.AutotuningRestricted)
            || assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.WindowSizeOverridesAutotuning))
        {
            causes.Add("Non-default TCP tuning (registry tweaks or netsh global settings) that bypass auto-tuning.");
        }
        if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.MaxUserPortLow)
            || assessment.Flags.Contains(PCDiag.Net.Tcp.TcpHealthFlag.TcpTimedWaitDelayLow))
        {
            causes.Add("TCP registry tweaks that were applied and limit concurrency or port reuse.");
        }
        return causes;
    }

    private static double ComputeConfidence(PCDiag.Net.Tcp.TcpHealthAssessment assessment)
    {
        return assessment.Verdict switch
        {
            PCDiag.Net.Tcp.TcpHealthVerdict.Warning => 0.75,
            PCDiag.Net.Tcp.TcpHealthVerdict.Suspicious => 0.6,
            _ => 0.8
        };
    }
}