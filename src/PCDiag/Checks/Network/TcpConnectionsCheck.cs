using PCDiag.Core;

namespace PCDiag.Checks.Network;

/// <summary>
/// Analyzes the TCP connection table (TIME_WAIT, CLOSE_WAIT, established, listen) in
/// context. A high TIME_WAIT count is never labeled bad on its own: it is judged
/// against the size of the dynamic port pool and is only a concern near exhaustion.
/// Read-only; no TCP registry values are changed.
/// </summary>
public sealed class TcpConnectionsCheck : DiagnosticCheck
{
    private readonly PCDiag.Net.Tcp.TcpOptions _options;
    private readonly PCDiag.Net.Tcp.ITcpConnectionSource _connectionSource;
    private readonly PCDiag.Net.Tcp.ITcpConfigSource _configSource;

    public override string CheckId => "NET-CONN-001";
    public override string Name => "TCP Connection States";
    public override DiagnosticCategory Category => DiagnosticCategory.Network;
    public override string Description =>
        "Analyzes TCP connection states (TIME_WAIT, CLOSE_WAIT, established) in context.";

    public TcpConnectionsCheck(
        PCDiag.Net.Tcp.TcpOptions? options = null,
        PCDiag.Net.Tcp.ITcpConnectionSource? connectionSource = null,
        PCDiag.Net.Tcp.ITcpConfigSource? configSource = null)
    {
        _options = options ?? PCDiag.Net.Tcp.TcpOptions.Default;
        _connectionSource = connectionSource ?? new PCDiag.Net.Tcp.WmiTcpConnectionSource();
        _configSource = configSource ?? new PCDiag.Net.Tcp.WmiTcpConfigSource();
    }

    private static readonly IReadOnlyList<string> CheckLimitations = new[]
    {
        "This is a single point-in-time snapshot; growth rates and accumulation trends cannot be measured in one run.",
        "A high TIME_WAIT count is normal for busy clients; it only matters when it approaches the dynamic port limit.",
        "Only IPv4 connections reported by the OS connection table are counted.",
        "The check is read-only; no TCP registry values are changed."
    };

    protected override Task<DiagnosticResult> RunAsync(DiagnosticContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var connections = _connectionSource.GetConnections();
        var config = _configSource.GetConfig();
        var summary = PCDiag.Net.Tcp.TcpStateSummary.Compute(
            connections, config.DynamicPortStart ?? 0, config.DynamicPortCount ?? 0);
        var assessment = PCDiag.Net.Tcp.TcpConnectionsClassifier.Classify(summary, _options, config.DynamicPortCount);
        var (severity, status) = MapHealth(assessment.Health);

        return Task.FromResult(BuildResult(
            severity,
            status,
            BuildSummary(summary, assessment, config),
            detail: BuildDetail(summary, assessment, config),
            evidence: BuildEvidence(summary, config, assessment),
            recommendations: BuildRecommendations(assessment, summary),
            possibleCauses: PossibleCauses(assessment),
            limitations: CheckLimitations,
            confidence: ComputeConfidence(assessment, summary)));
    }

    private static (DiagnosticSeverity Severity, DiagnosticStatus Status) MapHealth(PCDiag.Net.Tcp.TcpConnectionsHealth health)
    {
        return health switch
        {
            PCDiag.Net.Tcp.TcpConnectionsHealth.Warning => (DiagnosticSeverity.Warning, DiagnosticStatus.Finding),
            PCDiag.Net.Tcp.TcpConnectionsHealth.Elevated => (DiagnosticSeverity.Suspicious, DiagnosticStatus.Finding),
            _ => (DiagnosticSeverity.Healthy, DiagnosticStatus.Passed)
        };
    }

    private static string BuildSummary(
        PCDiag.Net.Tcp.TcpStateSummary summary,
        PCDiag.Net.Tcp.TcpConnectionsAssessment assessment,
        PCDiag.Net.Tcp.TcpConfiguration config)
    {
        return assessment.Health switch
        {
            PCDiag.Net.Tcp.TcpConnectionsHealth.Warning =>
                BuildWarningSummary(summary, config),
            PCDiag.Net.Tcp.TcpConnectionsHealth.Elevated =>
                BuildElevatedSummary(summary, config),
            _ =>
                $"TCP connection states look normal: {summary.Established} established, {summary.TimeWait} TIME_WAIT, {summary.CloseWait} CLOSE_WAIT."
        };
    }

    private static string BuildElevatedSummary(PCDiag.Net.Tcp.TcpStateSummary summary, PCDiag.Net.Tcp.TcpConfiguration config)
    {
        var notes = new List<string>();
        if (summary.TimeWait > 0)
            notes.Add($"a large TIME_WAIT accumulation ({summary.TimeWait} sockets)");
        if (summary.CloseWait > 0)
            notes.Add($"CLOSE_WAIT sockets ({summary.CloseWait}) that the owning app has not closed");
        if (summary.Established > 0)
            notes.Add($"a high number of established connections ({summary.Established})");

        return notes.Count == 0
            ? "TCP connection states are somewhat elevated, but no specific concern was identified."
            : "TCP connection states are elevated: " + string.Join(", ", notes) + ".";
    }

    private static string BuildWarningSummary(PCDiag.Net.Tcp.TcpStateSummary summary, PCDiag.Net.Tcp.TcpConfiguration config)
    {
        if (summary.TimeWait > 0 && summary.CloseWait == 0 && summary.Established <= PCDiag.Net.Tcp.TcpOptions.Default.EstablishedWarning)
            return $"TIME_WAIT is approaching the dynamic port limit ({summary.TimeWait} sockets of the dynamic port range).";
        if (summary.CloseWait > 0)
            return $"A large number of CLOSE_WAIT sockets ({summary.CloseWait}) suggests applications are not closing connections.";
        if (summary.Established > 0)
            return $"A very large number of established connections ({summary.Established}) was found.";
        return "TCP connection states show a concerning pattern.";
    }

    private static string BuildDetail(
        PCDiag.Net.Tcp.TcpStateSummary summary,
        PCDiag.Net.Tcp.TcpConnectionsAssessment assessment,
        PCDiag.Net.Tcp.TcpConfiguration config)
    {
        var parts = new List<string>
        {
            "A TCP connection passes through several states. TIME_WAIT holds the local port briefly after a connection " +
            "closes so late packets are not mistaken for a new connection; it is a normal, transient state and only " +
            "matters if the dynamic port pool is nearly exhausted. CLOSE_WAIT means the remote side closed but the " +
            "local application has not yet closed its socket - sockets that stay there accumulate as a leak."
        };

        var ports = config.DynamicPortCount is int c && c > 0 ? c : PCDiag.Net.Tcp.TcpOptions.Default.TimeWaitPortPoolFallback;
        var fraction = ports > 0 ? summary.TimeWait / (double)ports : 0;
        parts.Add(
            $"This snapshot shows {summary.Established} established, {summary.Listen} listening, {summary.TimeWait} TIME_WAIT, " +
            $"{summary.CloseWait} CLOSE_WAIT, {summary.Bound} bound sockets. " +
            $"TIME_WAIT uses {fraction:P0} of the dynamic port pool ({summary.DistinctLocalPorts} distinct local ports in use).");

        if (assessment.Health == PCDiag.Net.Tcp.TcpConnectionsHealth.Warning && summary.CloseWait > 0)
        {
            parts.Add(
                "CLOSE_WAIT sockets are owned by an application that has not issued close() after receiving the peer's " +
                "FIN. A cluster concentrated in one process is a socket leak; left unchecked it exhausts handles and ports.");
        }

        parts.Add(
            "Because this is a single snapshot, whether these counts are growing or transient cannot be determined here; " +
            "re-running the check shows whether the pattern persists.");

        return string.Join(" ", parts);
    }

    private IReadOnlyList<DiagnosticEvidence> BuildEvidence(
        PCDiag.Net.Tcp.TcpStateSummary summary,
        PCDiag.Net.Tcp.TcpConfiguration config,
        PCDiag.Net.Tcp.TcpConnectionsAssessment assessment)
    {
        var evidence = new List<DiagnosticEvidence>
        {
            new()
            {
                Description = "Total Sockets",
                Value = summary.Total.ToString(),
                Source = "MSFT_NetTCPConnection"
            },
            new()
            {
                Description = "Established",
                Value = summary.Established.ToString(),
                Source = "MSFT_NetTCPConnection"
            },
            new()
            {
                Description = "TIME_WAIT",
                Value = summary.TimeWait.ToString(),
                Source = "MSFT_NetTCPConnection"
            },
            new()
            {
                Description = "CLOSE_WAIT",
                Value = summary.CloseWait.ToString(),
                Source = "MSFT_NetTCPConnection"
            },
            new()
            {
                Description = "Listen",
                Value = summary.Listen.ToString(),
                Source = "MSFT_NetTCPConnection"
            },
            new()
            {
                Description = "Bound / Other States",
                Value = $"{summary.Bound} bound, {summary.SynSent} syn-sent, {summary.Other} other",
                Source = "MSFT_NetTCPConnection"
            },
            new()
            {
                Description = "Dynamic Port Range",
                Value = config.DynamicPortStart is int start && config.DynamicPortCount is int count
                    ? $"{start}-{start + count - 1} ({count} ports)"
                    : $"unknown (using {_options.TimeWaitPortPoolFallback} as an estimate)",
                Source = "MSFT_NetTCPSetting"
            },
            new()
            {
                Description = "TIME_WAIT Context",
                Value = config.DynamicPortCount is int c && c > 0
                    ? $"{summary.TimeWait} of {c} dynamic ports ({(c > 0 ? summary.TimeWait / (double)c : 0):P0}); {summary.DistinctLocalPorts} distinct local ports in use"
                    : $"{summary.TimeWait} TIME_WAIT; {summary.DistinctLocalPorts} distinct local ports in use (range unknown)",
                Source = "interpretation"
            }
        };

        if (summary.CloseWaitByProcess.Count > 0)
        {
            var top = summary.CloseWaitByProcess.Take(_options.MaxTopProcesses);
            evidence.Add(new DiagnosticEvidence
            {
                Description = "CLOSE_WAIT by Process",
                Value = string.Join(", ", top.Select(p => $"{ProcessName(p.ProcessId)} (PID {p.ProcessId}): {p.Count}")),
                Source = "MSFT_NetTCPConnection"
            });
        }

        if (summary.EstablishedByProcess.Count > 0)
        {
            var top = summary.EstablishedByProcess.Take(_options.MaxTopProcesses);
            evidence.Add(new DiagnosticEvidence
            {
                Description = "Established by Process",
                Value = string.Join(", ", top.Select(p => $"{ProcessName(p.ProcessId)} (PID {p.ProcessId}): {p.Count}")),
                Source = "MSFT_NetTCPConnection"
            });
        }

        evidence.Add(new DiagnosticEvidence
        {
            Description = "Threshold Reference",
            Value =
                $"TIME_WAIT: healthy < {_options.TimeWaitElevatedPortFraction:P0} of dynamic ports, warning >= {_options.TimeWaitWarningPortFraction:P0}; " +
                $"CLOSE_WAIT: suspicious > {_options.CloseWaitSuspicious}, warning > {_options.CloseWaitWarning}, or one process > {_options.CloseWaitPerProcessSuspicious}; " +
                $"established: suspicious > {_options.EstablishedSuspicious}, warning > {_options.EstablishedWarning}",
            Source = "documented in SPEC.md Phase 6"
        });

        return evidence;
    }

    private static IReadOnlyList<DiagnosticRecommendation> BuildRecommendations(
        PCDiag.Net.Tcp.TcpConnectionsAssessment assessment,
        PCDiag.Net.Tcp.TcpStateSummary summary)
    {
        if (assessment.Health == PCDiag.Net.Tcp.TcpConnectionsHealth.Healthy)
            return Array.Empty<DiagnosticRecommendation>();

        var recommendations = new List<DiagnosticRecommendation>();
        if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpConnectionsFlag.TimeWaitHigh))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "TIME_WAIT is approaching the dynamic port limit. This is usually caused by high connection churn (many short-lived connections); it becomes a problem only if new connections start failing. Re-run the check to see whether it is persistent.",
                RequiresAdmin = false,
                Priority = 2
            });
        }

        if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpConnectionsFlag.CloseWaitCluster)
            || assessment.Flags.Contains(PCDiag.Net.Tcp.TcpConnectionsFlag.CloseWaitSingleProcess))
        {
            var owner = summary.CloseWaitByProcess.Count > 0
                ? $"{ProcessName(summary.CloseWaitByProcess[0].ProcessId)} (PID {summary.CloseWaitByProcess[0].ProcessId})"
                : "the owning application";
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = $"A cluster of CLOSE_WAIT sockets is owned by {owner}. The application is not closing connections after the peer disconnects - restart it or update it to resolve the leak.",
                RequiresAdmin = false,
                Priority = 1
            });
        }

        if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpConnectionsFlag.EstablishedHigh)
            || assessment.Flags.Contains(PCDiag.Net.Tcp.TcpConnectionsFlag.EstablishedElevated))
        {
            var owner = summary.EstablishedByProcess.Count > 0
                ? $"{ProcessName(summary.EstablishedByProcess[0].ProcessId)} (PID {summary.EstablishedByProcess[0].ProcessId})"
                : null;
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = owner is not null
                    ? $"A large number of established connections is owned by {owner}. Review what it is doing; very high counts can indicate a runaway process or peer-sharing activity."
                    : "A large number of established connections was found. Review the processes listed in the evidence to identify what is holding them.",
                RequiresAdmin = false,
                Priority = 2
            });
        }

        return recommendations;
    }

    private static IReadOnlyList<string> PossibleCauses(PCDiag.Net.Tcp.TcpConnectionsAssessment assessment)
    {
        var causes = new List<string>();
        if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpConnectionsFlag.TimeWaitHigh))
        {
            causes.Add("High connection churn: browsers, download managers, and HTTP clients opening many short-lived connections.");
            causes.Add("Applications not reusing connections (no keep-alive).");
        }
        if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpConnectionsFlag.CloseWaitCluster)
            || assessment.Flags.Contains(PCDiag.Net.Tcp.TcpConnectionsFlag.CloseWaitSingleProcess))
        {
            causes.Add("An application failing to close sockets after the peer disconnects (socket leak).");
        }
        if (assessment.Flags.Contains(PCDiag.Net.Tcp.TcpConnectionsFlag.EstablishedHigh)
            || assessment.Flags.Contains(PCDiag.Net.Tcp.TcpConnectionsFlag.EstablishedElevated))
        {
            causes.Add("Peer-to-peer activity, many browser tabs/streams, or a runaway process.");
        }
        return causes;
    }

    private static double ComputeConfidence(PCDiag.Net.Tcp.TcpConnectionsAssessment assessment, PCDiag.Net.Tcp.TcpStateSummary summary)
    {
        return assessment.Health switch
        {
            PCDiag.Net.Tcp.TcpConnectionsHealth.Warning => 0.75,
            PCDiag.Net.Tcp.TcpConnectionsHealth.Elevated => 0.6,
            _ => summary.Total > 0 ? 0.8 : 0.5
        };
    }

    private static string ProcessName(int pid)
    {
        if (pid <= 0)
            return "";
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            return process.ProcessName;
        }
        catch
        {
            return "";
        }
    }
}