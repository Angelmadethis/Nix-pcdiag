using PCDiag.Core;

namespace PCDiag.Correlation.Rules;

/// <summary>
/// Detects when hardware diagnostics (WHEA errors or driver problems) co-occur
/// with network connectivity issues. WHEA errors pointing at the NIC, or driver
/// problems combined with network failures, strongly suggest a hardware fault
/// in the network adapter itself.
/// </summary>
public sealed class HardwareNetworkLinkRule : ICorrelationRule
{
    public IReadOnlyList<DiagnosticCorrelation> Analyze(IReadOnlyList<DiagnosticResult> results)
    {
        var whea = results.FirstOrDefault(r => r.CheckId == "HW-WHEA-001");
        var driver = results.FirstOrDefault(r => r.CheckId == "HW-DRV-001");
        var gateway = results.FirstOrDefault(r => r.CheckId == "NET-GWY-001");
        var packetLoss = results.FirstOrDefault(r => r.CheckId == "NET-LOSS-001");

        // Need at least one hardware finding and one network finding
        var hwFindings = new List<DiagnosticResult>();
        if (whea is { Severity: >= DiagnosticSeverity.Suspicious })
            hwFindings.Add(whea);
        if (driver is { Severity: >= DiagnosticSeverity.Suspicious })
            hwFindings.Add(driver);

        var netFindings = new List<DiagnosticResult>();
        if (gateway is { Severity: >= DiagnosticSeverity.Suspicious })
            netFindings.Add(gateway);
        if (packetLoss is { Severity: >= DiagnosticSeverity.Suspicious })
            netFindings.Add(packetLoss);

        if (hwFindings.Count == 0 || netFindings.Count == 0)
            return Array.Empty<DiagnosticCorrelation>();

        // Conflict: if hardware is healthy, network issues are not hardware-related
        if (whea?.Severity == DiagnosticSeverity.Healthy && driver?.Severity == DiagnosticSeverity.Healthy)
            return Array.Empty<DiagnosticCorrelation>();

        var involved = new List<DiagnosticResult>();
        involved.AddRange(hwFindings);
        involved.AddRange(netFindings);
        var worst = involved.MaxBy(r => r.Severity)!;
        var minConfidence = involved.Min(r => r.Confidence);
        var confidence = Math.Round(minConfidence * 0.75, 2);

        var evidence = involved
            .SelectMany(r => r.Evidence)
            .GroupBy(e => e.Description)
            .Select(g => g.First())
            .ToList();

        var relatedIds = hwFindings.Select(r => r.CheckId)
            .Concat(netFindings.Select(r => r.CheckId))
            .ToList();

        return new[]
        {
            new DiagnosticCorrelation
            {
                Id = "CORR-HW-NET-001",
                Title = "Hardware-Network Link",
                Summary = "Hardware problems (WHEA errors or driver issues) coincide with network failures — likely a NIC hardware fault.",
                Detail =
                    "Hardware diagnostics report errors or driver problems while network checks also " +
                    "find connectivity issues. When hardware warnings co-occur with network failures, " +
                    "the network adapter hardware itself is often the root cause. WHEA errors pointing " +
                    "at the NIC, or driver crashes combined with unreachable gateways, strongly suggest " +
                    "a physical adapter fault or a critically malfunctioning driver.",
                Confidence = confidence,
                Severity = worst.Severity,
                RelatedCheckIds = relatedIds,
                ConsolidatedEvidence = evidence,
                Recommendations = new[]
                {
                    new DiagnosticRecommendation
                    {
                        Text = "Check Device Manager for the network adapter; look for error codes (Code 10, 43, 31) that indicate hardware failure.",
                        RequiresAdmin = false,
                        Priority = 1
                    },
                    new DiagnosticRecommendation
                    {
                        Text = "Try updating the NIC driver from the manufacturer's website. If the driver is already current, the adapter may need to be replaced.",
                        RequiresAdmin = true,
                        Priority = 2
                    }
                },
                RootCauses = new[]
                {
                    "Network adapter hardware failure.",
                    "Corrupted or incompatible NIC driver causing both WHEA errors and connectivity loss.",
                    "Faulty NIC firmware triggering hardware errors and dropping connections."
                }
            }
        };
    }
}
