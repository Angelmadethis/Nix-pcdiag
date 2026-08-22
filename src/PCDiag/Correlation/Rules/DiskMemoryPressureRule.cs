using PCDiag.Core;

namespace PCDiag.Correlation.Rules;

/// <summary>
/// Detects when both storage and memory diagnostics report problems simultaneously.
/// High memory pressure combined with disk issues (high I/O, low free space, or
/// dirty shutdown bit) often indicates system-wide resource contention where paging
/// activity is overwhelming an already-stressed disk.
/// </summary>
public sealed class DiskMemoryPressureRule : ICorrelationRule
{
    private static readonly string[] RequiredIds = { "PERF-DISK-001", "PERF-MEM-001" };

    public IReadOnlyList<DiagnosticCorrelation> Analyze(IReadOnlyList<DiagnosticResult> results)
    {
        var disk = results.FirstOrDefault(r => r.CheckId == "PERF-DISK-001");
        var memory = results.FirstOrDefault(r => r.CheckId == "PERF-MEM-001");

        if (disk is null || memory is null)
            return Array.Empty<DiagnosticCorrelation>();

        // Both must be warning or critical
        if (disk.Severity < DiagnosticSeverity.Warning || memory.Severity < DiagnosticSeverity.Warning)
            return Array.Empty<DiagnosticCorrelation>();

        // Conflict: if either is healthy, the other is an isolated issue
        if (disk.Severity == DiagnosticSeverity.Healthy || memory.Severity == DiagnosticSeverity.Healthy)
            return Array.Empty<DiagnosticCorrelation>();

        var involved = new[] { disk, memory };
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
                Id = "CORR-SYS-001",
                Title = "System Resource Pressure",
                Summary = "Storage and memory problems together indicate system-wide resource contention.",
                Detail =
                    "Both storage and memory diagnostics report warning-level or critical findings. " +
                    "When memory is low, Windows aggressively pages to disk, which increases disk I/O " +
                    "and can push an already-stressed disk over its limits. Conversely, a slow or full " +
                    "disk can make memory pressure worse because the page file becomes sluggish. " +
                    "Addressing either resource constraint should improve the other.",
                Confidence = confidence,
                Severity = worst.Severity,
                RelatedCheckIds = RequiredIds,
                ConsolidatedEvidence = evidence,
                Recommendations = new[]
                {
                    new DiagnosticRecommendation
                    {
                        Text = "Free up disk space (aim for at least 15–20% free) and close memory-intensive applications to break the pressure cycle.",
                        RequiresAdmin = false,
                        Priority = 1
                    },
                    new DiagnosticRecommendation
                    {
                        Text = "Check the page file size; if it is very small or very large, restore it to 'System managed' in Advanced System Settings.",
                        RequiresAdmin = true,
                        Priority = 2
                    }
                },
                RootCauses = new[]
                {
                    "Low memory causing excessive paging, which stresses the disk.",
                    "Full or slow disk making paging sluggish, worsening memory pressure.",
                    "Both memory and disk near capacity under heavy workload."
                }
            }
        };
    }
}
