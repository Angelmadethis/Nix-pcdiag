using PCDiag.Checks.Windows;
using PCDiag.Core;
using PCDiag.Reporting;

namespace PCDiag.Tests;

public class ReportingTests
{
    private static DiagnosticResult RichResult(string id, DiagnosticSeverity severity, DiagnosticStatus status)
        => new()
        {
            CheckId = id,
            Name = $"Check {id}",
            Category = DiagnosticCategory.Windows,
            Severity = severity,
            Status = status,
            Summary = "Something was detected.",
            Detail = "This is why it matters to the user.",
            Confidence = 0.9,
            Evidence = new[]
            {
                new DiagnosticEvidence
                {
                    Description = "Metric",
                    Value = "42",
                    ExpectedValue = "100",
                    Source = "test"
                }
            },
            PossibleCauses = new[] { "Possible cause one.", "Possible cause two." },
            Recommendations = new[]
            {
                new DiagnosticRecommendation { Text = "Do the thing.", Priority = 1 },
                new DiagnosticRecommendation { Text = "Do the other thing.", Priority = 2, RequiresAdmin = true }
            },
            Limitations = new[] { "This check has a known blind spot." }
        };

    private static string Capture(Action<TerminalRenderer> render)
    {
        var writer = new StringWriter();
        var renderer = new TerminalRenderer(output: writer);
        render(renderer);
        return writer.ToString();
    }

    private static string CapturePlain(Action<TerminalRenderer> render)
    {
        var writer = new StringWriter();
        var renderer = new TerminalRenderer(plain: true, output: writer);
        render(renderer);
        return writer.ToString();
    }

    [Fact]
    public void ScanSummary_RichMode_ShouldUseUnicodeSymbols()
    {
        var summary = new ScanSummary(
            new[] { RichResult("T-001", DiagnosticSeverity.Healthy, DiagnosticStatus.Passed) },
            TimeSpan.Zero);

        var text = Capture(r => r.PrintScanSummary(summary));

        Assert.Contains("✓ HEALTHY", text);
        Assert.DoesNotContain("[HEALTHY]", text);
    }

    [Fact]
    public void ScanSummary_PlainMode_ShouldUseAsciiLabels()
    {
        var summary = new ScanSummary(
            new[] { RichResult("T-001", DiagnosticSeverity.Healthy, DiagnosticStatus.Passed) },
            TimeSpan.Zero);

        var text = CapturePlain(r => r.PrintScanSummary(summary));

        Assert.Contains("[HEALTHY]", text);
        Assert.DoesNotContain("✓", text);
    }

    [Fact]
    public void ScanSummary_CategoryGrouping_ShouldPrintGroupHeader()
    {
        var summary = new ScanSummary(
            new[] { RichResult("T-001", DiagnosticSeverity.Info, DiagnosticStatus.Finding) },
            TimeSpan.Zero);

        var text = Capture(r => r.PrintScanSummary(summary, ResultGrouping.Category));

        Assert.Contains("WINDOWS", text);
    }

    [Fact]
    public void ScanSummary_SeverityGrouping_ShouldPrintSeverityHeader()
    {
        var summary = new ScanSummary(
            new[] { RichResult("T-001", DiagnosticSeverity.Critical, DiagnosticStatus.Finding) },
            TimeSpan.Zero);

        var text = Capture(r => r.PrintScanSummary(summary, ResultGrouping.Severity));

        Assert.Contains("CRITICAL", text);
    }

    [Fact]
    public void ScanSummary_ShouldIncludeRiskScoreAndElapsedTime()
    {
        var summary = new ScanSummary(
            new[] { RichResult("T-001", DiagnosticSeverity.Warning, DiagnosticStatus.Finding) },
            TimeSpan.FromSeconds(2.5));

        var text = Capture(r => r.PrintScanSummary(summary));

        Assert.Contains("Risk Score:", text);
        Assert.Contains("Scan completed in", text);
    }

    [Fact]
    public void DetailedOutput_ShouldIncludeAllSections()
    {
        var result = RichResult("T-001", DiagnosticSeverity.Critical, DiagnosticStatus.Finding);

        var text = Capture(r => r.PrintDetailed(result));

        Assert.Contains("CHECK        Check T-001 (T-001)", text);
        Assert.Contains("STATUS", text);
        Assert.Contains("SEVERITY", text);
        Assert.Contains("CONFIDENCE", text);
        Assert.Contains("WHAT WAS DETECTED", text);
        Assert.Contains("EVIDENCE", text);
        Assert.Contains("WHY IT MATTERS", text);
        Assert.Contains("POSSIBLE CAUSES", text);
        Assert.Contains("RECOMMENDED ACTIONS", text);
        Assert.Contains("LIMITATIONS", text);
        Assert.Contains("Metric: 42", text);
        Assert.Contains("Do the thing.", text);
    }

    [Fact]
    public void DetailedOutput_PlainMode_ShouldUseAsciiOnly()
    {
        var result = RichResult("T-001", DiagnosticSeverity.Critical, DiagnosticStatus.Finding);

        var text = CapturePlain(r => r.PrintDetailed(result));

        Assert.Contains("[CRITICAL]", text);
        Assert.DoesNotContain("✓", text);
        Assert.DoesNotContain("✕", text);
        Assert.DoesNotContain("•", text);
    }

    [Fact]
    public void DetailedOutput_ShouldOmitEmptySections()
    {
        var result = new DiagnosticResult
        {
            CheckId = "T-002",
            Name = "Sparse",
            Category = DiagnosticCategory.Windows,
            Severity = DiagnosticSeverity.Healthy,
            Status = DiagnosticStatus.Passed,
            Summary = "All good.",
            Confidence = 1.0
        };

        var text = Capture(r => r.PrintDetailed(result));

        Assert.DoesNotContain("EVIDENCE", text);
        Assert.DoesNotContain("WHY IT MATTERS", text);
        Assert.DoesNotContain("POSSIBLE CAUSES", text);
        Assert.DoesNotContain("RECOMMENDED ACTIONS", text);
        Assert.DoesNotContain("LIMITATIONS", text);
    }

    [Fact]
    public void DetailedOutput_ShouldShowStructuredErrors()
    {
        var result = new DiagnosticResult
        {
            CheckId = "T-003",
            Name = "Failing",
            Category = DiagnosticCategory.Windows,
            Severity = DiagnosticSeverity.Info,
            Status = DiagnosticStatus.Error,
            Summary = "Failed.",
            Confidence = 1.0,
            Errors = new[]
            {
                new DiagnosticError { Code = "timeout", Message = "The check timed out." }
            }
        };

        var text = Capture(r => r.PrintDetailed(result));

        Assert.Contains("[timeout]", text);
        Assert.Contains("The check timed out.", text);
    }

    [Fact]
    public void PrintProgress_ShouldIncludeCheckIdAndStatus()
    {
        var check = new EnvironmentCheck();
        var result = RichResult("WIN-ENV-001", DiagnosticSeverity.Healthy, DiagnosticStatus.Passed);

        var text = Capture(r => r.PrintProgress(check, result, TimeSpan.FromSeconds(1)));

        Assert.Contains("WIN-ENV-001", text);
        Assert.Contains("PASSED", text);
    }

    [Fact]
    public void VersionOutput_ShouldPrintProgramAndVersion()
    {
        var text = Capture(r => r.PrintVersion("1.0.0"));

        Assert.Contains("pcdiag 1.0.0", text);
    }

    [Fact]
    public void CheckList_ShouldPrintGroupedChecks()
    {
        var checks = new IDiagnosticCheck[] { new EnvironmentCheck() };

        var text = Capture(r => r.PrintCheckList(checks));

        Assert.Contains("AVAILABLE CHECKS", text);
        Assert.Contains("WIN-ENV-001", text);
    }
}