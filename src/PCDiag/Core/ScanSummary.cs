namespace PCDiag.Core;

/// <summary>
/// Aggregated result of a full scan.
/// </summary>
public sealed class ScanSummary
{
    /// <summary>All individual check results, in execution order.</summary>
    public IReadOnlyList<DiagnosticResult> Results { get; }

    /// <summary>Total wall-clock time of the scan.</summary>
    public TimeSpan Duration { get; }

    /// <summary>Total number of checks that were evaluated.</summary>
    public int Total { get; }

    public int Passed { get; }
    public int Finding { get; }
    public int Error { get; }
    public int Skipped { get; }
    public int Unavailable { get; }
    public int PermissionDenied { get; }

    /// <summary>Risk score from 0 (no risk) to 100 (maximum risk).</summary>
    public int RiskScore { get; }

    /// <summary>The worst severity found among countable results.</summary>
    public DiagnosticSeverity MaxSeverity { get; }

    public ScanSummary(IReadOnlyList<DiagnosticResult> results, TimeSpan duration)
    {
        Results = results;
        Duration = duration;
        Total = results.Count;
        Passed = results.Count(r => r.Status == DiagnosticStatus.Passed);
        Finding = results.Count(r => r.Status == DiagnosticStatus.Finding);
        Error = results.Count(r => r.Status == DiagnosticStatus.Error);
        Skipped = results.Count(r => r.Status == DiagnosticStatus.Skipped);
        Unavailable = results.Count(r => r.Status == DiagnosticStatus.Unavailable);
        PermissionDenied = results.Count(r => r.Status == DiagnosticStatus.PermissionDenied);
        RiskScore = CalculateRiskScore(results);
        MaxSeverity = results
            .Where(IsCountable)
            .Select(r => r.Severity)
            .DefaultIfEmpty(DiagnosticSeverity.Healthy)
            .Max();
    }

    private static bool IsCountable(DiagnosticResult result)
        => result.Status != DiagnosticStatus.Error
           && result.Status != DiagnosticStatus.Skipped
           && result.Status != DiagnosticStatus.Unavailable
           && result.Status != DiagnosticStatus.PermissionDenied;

    /// <summary>
    /// PCDiag risk score (0-100). The model is intentionally max-dominant:
    /// the worst finding drives the score, and healthy checks contribute nothing.
    ///
    /// <para>
    /// For every countable result (not Error/Skipped/Unavailable/PermissionDenied):
    /// </para>
    /// <list type="bullet">
    /// <item>Healthy → weight 0 (never increases risk)</item>
    /// <item>Info → weight 15</item>
    /// <item>Suspicious → weight 35</item>
    /// <item>Warning → weight 60</item>
    /// <item>Critical → weight 85</item>
    /// </list>
    /// <para>
    /// Each finding contributes <c>weight × confidence</c> (confidence clamped to 0-1),
    /// so a less-certain diagnosis carries proportionally less risk.
    /// The score is the worst single contribution, plus a capped bonus of 15% of the
    /// remaining contributions, so additional findings still register without letting
    /// a single catastrophic result be diluted by dozens of healthy ones.
    /// </para>
    /// </summary>
    public static int CalculateRiskScore(IReadOnlyList<DiagnosticResult> results)
    {
        const int weightHealthy = 0;
        const int weightInfo = 15;
        const int weightSuspicious = 35;
        const int weightWarning = 60;
        const int weightCritical = 85;

        int maxContribution = 0;
        int sumContributions = 0;

        foreach (var result in results)
        {
            if (!IsCountable(result))
                continue;

            int weight = result.Severity switch
            {
                DiagnosticSeverity.Critical => weightCritical,
                DiagnosticSeverity.Warning => weightWarning,
                DiagnosticSeverity.Suspicious => weightSuspicious,
                DiagnosticSeverity.Info => weightInfo,
                _ => weightHealthy
            };

            double confidence = Math.Clamp(result.Confidence, 0.0, 1.0);
            int contribution = (int)Math.Round(weight * confidence, MidpointRounding.AwayFromZero);

            maxContribution = Math.Max(maxContribution, contribution);
            sumContributions += contribution;
        }

        if (maxContribution == 0)
            return 0;

        int bonus = (int)((sumContributions - maxContribution) * 0.15);
        return Math.Min(100, maxContribution + bonus);
    }
}