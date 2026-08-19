using PCDiag.Core;

namespace PCDiag.Tests;

public class ScoringTests
{
    private static DiagnosticResult Result(string id, DiagnosticSeverity severity, double confidence = 1.0,
        DiagnosticStatus status = DiagnosticStatus.Finding)
        => new()
        {
            CheckId = id,
            Name = id,
            Category = DiagnosticCategory.Windows,
            Severity = severity,
            Status = status,
            Summary = "Test",
            Confidence = confidence
        };

    [Fact]
    public void RiskScore_NoResults_ShouldBeZero()
    {
        Assert.Equal(0, ScanSummary.CalculateRiskScore(Array.Empty<DiagnosticResult>()));
    }

    [Fact]
    public void RiskScore_HealthyResults_ShouldNotIncreaseRisk()
    {
        var results = Enumerable.Range(0, 50)
            .Select(i => Result($"T-{i}", DiagnosticSeverity.Healthy, status: DiagnosticStatus.Passed))
            .ToArray();

        Assert.Equal(0, ScanSummary.CalculateRiskScore(results));
    }

    [Fact]
    public void RiskScore_SingleCritical_ShouldBeHigh()
    {
        var score = ScanSummary.CalculateRiskScore(new[] { Result("T-001", DiagnosticSeverity.Critical) });

        Assert.True(score >= 80, $"Expected score >= 80, got {score}");
    }

    [Fact]
    public void RiskScore_Critical_ShouldNotBeHiddenByManyHealthyResults()
    {
        var results = new List<DiagnosticResult> { Result("T-CRIT", DiagnosticSeverity.Critical) };
        results.AddRange(Enumerable.Range(0, 100)
            .Select(i => Result($"H-{i}", DiagnosticSeverity.Healthy, status: DiagnosticStatus.Passed)));

        var score = ScanSummary.CalculateRiskScore(results);

        Assert.True(score >= 80, $"A single critical must not be diluted by healthy results, got {score}");
    }

    [Fact]
    public void RiskScore_SingleWarning_ShouldBeModerate()
    {
        var score = ScanSummary.CalculateRiskScore(new[] { Result("T-001", DiagnosticSeverity.Warning) });

        Assert.InRange(score, 40, 70);
    }

    [Fact]
    public void RiskScore_SingleInfo_ShouldBeLow()
    {
        var score = ScanSummary.CalculateRiskScore(new[] { Result("T-001", DiagnosticSeverity.Info) });

        Assert.InRange(score, 1, 30);
    }

    [Fact]
    public void RiskScore_Confidence_ShouldLowerContribution()
    {
        var confident = ScanSummary.CalculateRiskScore(new[] { Result("T-001", DiagnosticSeverity.Critical, confidence: 1.0) });
        var uncertain = ScanSummary.CalculateRiskScore(new[] { Result("T-001", DiagnosticSeverity.Critical, confidence: 0.5) });

        Assert.True(uncertain < confident, $"Expected {uncertain} < {confident}");
    }

    [Fact]
    public void RiskScore_ShouldExcludeErrorsSkippedUnavailablePermissionDenied()
    {
        var results = new[]
        {
            Result("E-001", DiagnosticSeverity.Critical, status: DiagnosticStatus.Error),
            Result("S-001", DiagnosticSeverity.Critical, status: DiagnosticStatus.Skipped),
            Result("U-001", DiagnosticSeverity.Critical, status: DiagnosticStatus.Unavailable),
            Result("P-001", DiagnosticSeverity.Critical, status: DiagnosticStatus.PermissionDenied),
            Result("H-001", DiagnosticSeverity.Healthy, status: DiagnosticStatus.Passed)
        };

        Assert.Equal(0, ScanSummary.CalculateRiskScore(results));
    }

    [Fact]
    public void RiskScore_MultipleCriticalFindings_ShouldApproachMaximum()
    {
        var results = Enumerable.Range(0, 10)
            .Select(i => Result($"C-{i}", DiagnosticSeverity.Critical))
            .ToArray();

        var score = ScanSummary.CalculateRiskScore(results);

        Assert.Equal(100, score);
    }

    [Fact]
    public void RiskScore_ClusterOfWarnings_ShouldScoreAboveSingleWarning()
    {
        var single = ScanSummary.CalculateRiskScore(new[] { Result("T-001", DiagnosticSeverity.Warning) });
        var cluster = ScanSummary.CalculateRiskScore(new[]
        {
            Result("T-001", DiagnosticSeverity.Warning),
            Result("T-002", DiagnosticSeverity.Warning),
            Result("T-003", DiagnosticSeverity.Warning)
        });

        Assert.True(cluster > single, $"Expected cluster {cluster} > single {single}");
    }
}