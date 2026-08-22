using PCDiag.Core;

namespace PCDiag.Correlation.Rules;

/// <summary>
/// Detects when DNS resolution is slow or unreliable while the gateway is also
/// unhealthy. DNS problems that co-occur with gateway issues typically share a
/// common upstream cause (router failure, ISP outage) rather than being independent
/// DNS server problems.
/// </summary>
public sealed class DnsDegradationRule : ICorrelationRule
{
    private static readonly string[] RequiredIds = { "NET-DNS-001", "NET-GWY-001" };

    public IReadOnlyList<DiagnosticCorrelation> Analyze(IReadOnlyList<DiagnosticResult> results)
    {
        var dns = results.FirstOrDefault(r => r.CheckId == "NET-DNS-001");
        var gateway = results.FirstOrDefault(r => r.CheckId == "NET-GWY-001");

        if (dns is null || gateway is null)
            return Array.Empty<DiagnosticCorrelation>();

        // DNS must be suspicious or worse
        if (dns.Severity < DiagnosticSeverity.Suspicious)
            return Array.Empty<DiagnosticCorrelation>();

        // Gateway must be suspicious or worse
        if (gateway.Severity < DiagnosticSeverity.Suspicious)
            return Array.Empty<DiagnosticCorrelation>();

        // Conflict: if gateway is healthy, DNS problems are likely DNS-server-side, not upstream
        if (gateway.Severity == DiagnosticSeverity.Healthy)
            return Array.Empty<DiagnosticCorrelation>();

        var involved = new[] { dns, gateway };
        var worst = involved.MaxBy(r => r.Severity)!;
        var minConfidence = involved.Min(r => r.Confidence);
        var confidence = Math.Round(minConfidence * 0.8, 2);

        var evidence = involved
            .SelectMany(r => r.Evidence)
            .GroupBy(e => e.Description)
            .Select(g => g.First())
            .ToList();

        return new[]
        {
            new DiagnosticCorrelation
            {
                Id = "CORR-NET-002",
                Title = "DNS Degradation with Gateway Issues",
                Summary = "DNS resolution problems coincide with gateway issues — likely the same upstream cause.",
                Detail =
                    "DNS resolution is slow or unreliable while the default gateway is also unhealthy. " +
                    "When both DNS and the gateway show problems, the root cause is usually upstream " +
                    "(router failure, ISP outage, or network congestion) rather than independent DNS " +
                    "server issues. Fixing the gateway connectivity should also resolve the DNS problems.",
                Confidence = confidence,
                Severity = worst.Severity,
                RelatedCheckIds = RequiredIds,
                ConsolidatedEvidence = evidence,
                Recommendations = new[]
                {
                    new DiagnosticRecommendation
                    {
                        Text = "Address the gateway issue first; DNS problems will likely resolve once connectivity is restored.",
                        RequiresAdmin = true,
                        Priority = 1
                    },
                    new DiagnosticRecommendation
                    {
                        Text = "If gateway is healthy but DNS remains slow, try flushing the DNS cache ('ipconfig /flushdns').",
                        RequiresAdmin = false,
                        Priority = 2
                    }
                },
                RootCauses = new[]
                {
                    "Upstream router or modem failure affecting all network traffic.",
                    "ISP outage or degraded connection.",
                    "Local network congestion impacting both DNS and gateway traffic."
                }
            }
        };
    }
}
