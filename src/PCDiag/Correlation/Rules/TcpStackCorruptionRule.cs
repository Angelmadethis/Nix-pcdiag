using PCDiag.Core;

namespace PCDiag.Correlation.Rules;

/// <summary>
/// Detects when TCP health is degraded alongside gateway or packet-loss problems.
/// TCP stack issues (auto-tuning disabled, connection anomalies) combined with
/// connectivity problems often indicate a corrupted network stack or failing adapter.
/// </summary>
public sealed class TcpStackCorruptionRule : ICorrelationRule
{
    public IReadOnlyList<DiagnosticCorrelation> Analyze(IReadOnlyList<DiagnosticResult> results)
    {
        var tcp = results.FirstOrDefault(r => r.CheckId == "NET-TCP-001");
        var gateway = results.FirstOrDefault(r => r.CheckId == "NET-GWY-001");
        var packetLoss = results.FirstOrDefault(r => r.CheckId == "NET-LOSS-001");

        if (tcp is null)
            return Array.Empty<DiagnosticCorrelation>();

        if (tcp.Severity < DiagnosticSeverity.Suspicious)
            return Array.Empty<DiagnosticCorrelation>();

        // Need at least one of gateway or packet-loss also as a finding
        var networkFindings = new List<DiagnosticResult>();
        if (gateway is { Severity: >= DiagnosticSeverity.Suspicious })
            networkFindings.Add(gateway);
        if (packetLoss is { Severity: >= DiagnosticSeverity.Suspicious })
            networkFindings.Add(packetLoss);

        if (networkFindings.Count == 0)
            return Array.Empty<DiagnosticCorrelation>();

        // Conflict: if both gateway and packet-loss are healthy, TCP issues are isolated
        if (gateway?.Severity == DiagnosticSeverity.Healthy && packetLoss?.Severity == DiagnosticSeverity.Healthy)
            return Array.Empty<DiagnosticCorrelation>();

        var involved = new List<DiagnosticResult> { tcp };
        involved.AddRange(networkFindings);
        var worst = involved.MaxBy(r => r.Severity)!;
        var minConfidence = involved.Min(r => r.Confidence);
        var confidence = Math.Round(minConfidence * 0.85, 2);

        var evidence = involved
            .SelectMany(r => r.Evidence)
            .GroupBy(e => e.Description)
            .Select(g => g.First())
            .ToList();

        var relatedIds = new List<string> { "NET-TCP-001" };
        if (networkFindings.Any(r => r.CheckId == "NET-GWY-001"))
            relatedIds.Add("NET-GWY-001");
        if (networkFindings.Any(r => r.CheckId == "NET-LOSS-001"))
            relatedIds.Add("NET-LOSS-001");

        return new[]
        {
            new DiagnosticCorrelation
            {
                Id = "CORR-NET-003",
                Title = "TCP Stack Corruption",
                Summary = "TCP health issues combined with connectivity problems suggest a corrupted network stack.",
                Detail =
                    "TCP configuration problems (such as disabled auto-tuning or connection anomalies) " +
                    "are appearing alongside gateway or packet-loss issues. This combination often " +
                    "indicates a corrupted Winsock catalog or TCP/IP stack rather than separate problems. " +
                    "Resetting the network stack typically resolves both the TCP and connectivity issues.",
                Confidence = confidence,
                Severity = worst.Severity,
                RelatedCheckIds = relatedIds,
                ConsolidatedEvidence = evidence,
                Recommendations = new[]
                {
                    new DiagnosticRecommendation
                    {
                        Text = "Reset the TCP/IP stack: run 'netsh int ip reset' followed by 'netsh winsock reset', then reboot.",
                        RequiresAdmin = true,
                        Priority = 1
                    },
                    new DiagnosticRecommendation
                    {
                        Text = "If the problem persists after a stack reset, update or reinstall the network adapter driver.",
                        RequiresAdmin = true,
                        Priority = 2
                    }
                },
                RootCauses = new[]
                {
                    "Winsock catalog corruption.",
                    "TCP/IP stack corruption from a failed update or abrupt shutdown.",
                    "Network adapter driver malfunction affecting both TCP and connectivity."
                }
            }
        };
    }
}
