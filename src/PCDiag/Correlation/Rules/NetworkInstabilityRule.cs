using PCDiag.Core;

namespace PCDiag.Correlation.Rules;

/// <summary>
/// Detects when gateway, packet-loss, and TCP health checks all report findings
/// simultaneously. Three independent network checks finding problems at the same
/// time almost always points to a single underlying cause: adapter driver issue,
/// failing hardware, or upstream network failure.
/// </summary>
public sealed class NetworkInstabilityRule : ICorrelationRule
{
    private static readonly string[] RequiredIds = { "NET-GWY-001", "NET-LOSS-001", "NET-TCP-001" };

    public IReadOnlyList<DiagnosticCorrelation> Analyze(IReadOnlyList<DiagnosticResult> results)
    {
        var gateway = results.FirstOrDefault(r => r.CheckId == "NET-GWY-001");
        var packetLoss = results.FirstOrDefault(r => r.CheckId == "NET-LOSS-001");
        var tcp = results.FirstOrDefault(r => r.CheckId == "NET-TCP-001");

        // All three must be findings
        if (gateway is null || packetLoss is null || tcp is null)
            return Array.Empty<DiagnosticCorrelation>();

        // Conflict check: if gateway is healthy, the pattern does not apply
        // (healthy gateway + packet loss = ISP or internet-side issue, not local instability)
        if (gateway.Severity == DiagnosticSeverity.Healthy)
            return Array.Empty<DiagnosticCorrelation>();

        var involved = new[] { gateway, packetLoss, tcp };
        var worst = involved.MaxBy(r => r.Severity)!;
        var minConfidence = involved.Min(r => r.Confidence);
        var confidence = Math.Round(minConfidence * 0.85, 2);

        var evidence = involved
            .SelectMany(r => r.Evidence)
            .GroupBy(e => e.Description)
            .Select(g => g.First())
            .ToList();

        return new[]
        {
            new DiagnosticCorrelation
            {
                Id = "CORR-NET-001",
                Title = "Network Instability",
                Summary = "Gateway, packet-loss, and TCP health checks all report problems — likely a single underlying cause.",
                Detail =
                    "Three independent network diagnostics are finding problems simultaneously. " +
                    "When the default gateway is unreachable or lossy, packet loss is elevated, and TCP " +
                    "health is degraded, the most likely explanation is a single root cause affecting the " +
                    "network adapter or its driver, rather than three separate issues. Common causes include " +
                    "a failing NIC, a corrupted Winsock/TCP stack, or an upstream router failure.",
                Confidence = confidence,
                Severity = worst.Severity,
                RelatedCheckIds = RequiredIds,
                ConsolidatedEvidence = evidence,
                Recommendations = new[]
                {
                    new DiagnosticRecommendation
                    {
                        Text = "Restart the network adapter and renew the DHCP lease as a first step; if the problem persists, check the NIC driver in Device Manager.",
                        RequiresAdmin = true,
                        Priority = 1
                    },
                    new DiagnosticRecommendation
                    {
                        Text = "If restarting the adapter does not help, run 'netsh winsock reset' and 'netsh int ip reset' to rebuild the network stack, then reboot.",
                        RequiresAdmin = true,
                        Priority = 2
                    }
                },
                RootCauses = new[]
                {
                    "Network adapter driver is failing or corrupted.",
                    "Winsock/TCP stack is corrupted.",
                    "Upstream router or switch is malfunctioning.",
                    "Physical connection fault (cable, port, or Wi-Fi radio)."
                }
            }
        };
    }
}
