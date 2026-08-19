using PCDiag.Core;

namespace PCDiag.Tests;

public class ScanSummaryTests
{
    private static DiagnosticResult Result(string id, DiagnosticSeverity severity, DiagnosticStatus status)
        => new()
        {
            CheckId = id,
            Name = id,
            Category = DiagnosticCategory.Windows,
            Severity = severity,
            Status = status,
            Summary = "Test",
            Confidence = 1.0
        };

    [Fact]
    public void RiskScore_NoResults_ShouldBeZero()
    {
        var summary = new ScanSummary(Array.Empty<DiagnosticResult>(), TimeSpan.Zero);

        Assert.Equal(0, summary.RiskScore);
        Assert.Equal(0, summary.Total);
        Assert.Equal(DiagnosticSeverity.Healthy, summary.MaxSeverity);
    }

    [Fact]
    public void RiskScore_AllHealthy_ShouldBeZero()
    {
        var results = new[]
        {
            Result("T-001", DiagnosticSeverity.Healthy, DiagnosticStatus.Passed),
            Result("T-002", DiagnosticSeverity.Healthy, DiagnosticStatus.Passed)
        };

        var summary = new ScanSummary(results, TimeSpan.Zero);

        Assert.Equal(0, summary.RiskScore);
    }

    [Fact]
    public void RiskScore_WithCritical_ShouldBeHigh()
    {
        var results = new[]
        {
            Result("T-001", DiagnosticSeverity.Critical, DiagnosticStatus.Finding)
        };

        var summary = new ScanSummary(results, TimeSpan.Zero);

        Assert.True(summary.RiskScore > 50, $"Expected score > 50, got {summary.RiskScore}");
    }

    [Fact]
    public void RiskScore_ShouldIgnoreErrorsAndSkipped()
    {
        var results = new[]
        {
            Result("T-001", DiagnosticSeverity.Healthy, DiagnosticStatus.Passed),
            Result("T-002", DiagnosticSeverity.Critical, DiagnosticStatus.Error),
            Result("T-003", DiagnosticSeverity.Critical, DiagnosticStatus.Skipped),
            Result("T-004", DiagnosticSeverity.Critical, DiagnosticStatus.Unavailable),
            Result("T-005", DiagnosticSeverity.Critical, DiagnosticStatus.PermissionDenied)
        };

        var summary = new ScanSummary(results, TimeSpan.Zero);

        Assert.Equal(0, summary.RiskScore);
    }

    [Fact]
    public void Counts_ShouldReflectStatuses()
    {
        var results = new[]
        {
            Result("T-001", DiagnosticSeverity.Healthy, DiagnosticStatus.Passed),
            Result("T-002", DiagnosticSeverity.Warning, DiagnosticStatus.Finding),
            Result("T-003", DiagnosticSeverity.Info, DiagnosticStatus.Error),
            Result("T-004", DiagnosticSeverity.Info, DiagnosticStatus.Skipped),
            Result("T-005", DiagnosticSeverity.Info, DiagnosticStatus.Unavailable),
            Result("T-006", DiagnosticSeverity.Info, DiagnosticStatus.PermissionDenied)
        };

        var summary = new ScanSummary(results, TimeSpan.FromSeconds(3));

        Assert.Equal(6, summary.Total);
        Assert.Equal(1, summary.Passed);
        Assert.Equal(1, summary.Finding);
        Assert.Equal(1, summary.Error);
        Assert.Equal(1, summary.Skipped);
        Assert.Equal(1, summary.Unavailable);
        Assert.Equal(1, summary.PermissionDenied);
        Assert.Equal(TimeSpan.FromSeconds(3), summary.Duration);
    }

    [Fact]
    public void MaxSeverity_ShouldBeWorstCountableResult()
    {
        var results = new[]
        {
            Result("T-001", DiagnosticSeverity.Healthy, DiagnosticStatus.Passed),
            Result("T-002", DiagnosticSeverity.Critical, DiagnosticStatus.Finding),
            Result("T-003", DiagnosticSeverity.Critical, DiagnosticStatus.Error)
        };

        var summary = new ScanSummary(results, TimeSpan.Zero);

        Assert.Equal(DiagnosticSeverity.Critical, summary.MaxSeverity);
    }
}