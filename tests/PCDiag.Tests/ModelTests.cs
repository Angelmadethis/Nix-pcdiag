using PCDiag.Core;

namespace PCDiag.Tests;

public class ModelTests
{
    [Fact]
    public void DiagnosticResult_ShouldHaveRequiredPropertiesAndDefaults()
    {
        var result = new DiagnosticResult
        {
            CheckId = "TEST-001",
            Name = "Test Check",
            Category = DiagnosticCategory.Network,
            Severity = DiagnosticSeverity.Healthy,
            Status = DiagnosticStatus.Passed,
            Summary = "All good"
        };

        Assert.Equal("TEST-001", result.CheckId);
        Assert.Equal("Test Check", result.Name);
        Assert.Equal(DiagnosticCategory.Network, result.Category);
        Assert.Equal(DiagnosticSeverity.Healthy, result.Severity);
        Assert.Equal(DiagnosticStatus.Passed, result.Status);
        Assert.Equal("All good", result.Summary);
        Assert.Empty(result.Evidence);
        Assert.Empty(result.Recommendations);
        Assert.Empty(result.Errors);
        Assert.Equal(TimeSpan.Zero, result.Duration);
    }

    [Fact]
    public void DiagnosticResult_ShouldSupportEvidence()
    {
        var result = new DiagnosticResult
        {
            CheckId = "TEST-002",
            Name = "Test with Evidence",
            Category = DiagnosticCategory.Network,
            Severity = DiagnosticSeverity.Info,
            Status = DiagnosticStatus.Finding,
            Summary = "Found something",
            Evidence = new[]
            {
                new DiagnosticEvidence
                {
                    Description = "Test metric",
                    Value = "42",
                    ExpectedValue = "100",
                    Source = "mock"
                }
            }
        };

        var evidence = Assert.Single(result.Evidence);
        Assert.Equal("42", evidence.Value);
        Assert.Equal("100", evidence.ExpectedValue);
        Assert.Equal("mock", evidence.Source);
    }

    [Fact]
    public void DiagnosticResult_ShouldSupportStructuredErrors()
    {
        var result = new DiagnosticResult
        {
            CheckId = "TEST-003",
            Name = "Test with Error",
            Category = DiagnosticCategory.Windows,
            Severity = DiagnosticSeverity.Info,
            Status = DiagnosticStatus.Error,
            Summary = "Failed",
            Errors = new[]
            {
                new DiagnosticError { Code = "timeout", Message = "The check timed out." }
            }
        };

        var error = Assert.Single(result.Errors);
        Assert.Equal("timeout", error.Code);
    }

    [Fact]
    public void Severity_ShouldHaveCorrectOrdering()
    {
        Assert.True(DiagnosticSeverity.Healthy < DiagnosticSeverity.Info);
        Assert.True(DiagnosticSeverity.Info < DiagnosticSeverity.Suspicious);
        Assert.True(DiagnosticSeverity.Suspicious < DiagnosticSeverity.Warning);
        Assert.True(DiagnosticSeverity.Warning < DiagnosticSeverity.Critical);
    }

    [Fact]
    public void Status_ShouldIncludeUnavailableAndPermissionDenied()
    {
        Assert.Equal(6, Enum.GetNames<DiagnosticStatus>().Length);
        Assert.Contains(DiagnosticStatus.Unavailable, Enum.GetValues<DiagnosticStatus>());
        Assert.Contains(DiagnosticStatus.PermissionDenied, Enum.GetValues<DiagnosticStatus>());
    }

    [Fact]
    public void DiagnosticContext_ShouldUseDefaults()
    {
        var context = new DiagnosticContext();

        Assert.Equal(ScanMode.Standard, context.Mode);
        Assert.False(context.IsAdministrator);
        Assert.Equal(TimeSpan.FromSeconds(30), context.DefaultTimeout);
    }
}