using PCDiag.Core;
using PCDiag.Interactive;
using Spectre.Console;
using Spectre.Console.Testing;

namespace PCDiag.Tests;

public class ResultsTableTests
{
    private static DiagnosticResult Result(string id, DiagnosticSeverity severity, DiagnosticStatus status)
        => new()
        {
            CheckId = id,
            Name = $"Check {id}",
            Category = DiagnosticCategory.Windows,
            Severity = severity,
            Status = status,
            Summary = "Something was detected.",
            Confidence = 0.9
        };

    private static string Render(Table table)
    {
        var console = new TestConsole();
        console.Write(table);
        return console.Output;
    }

    [Fact]
    public void Build_ShouldCreateRowForEachResult()
    {
        var summary = new ScanSummary(
            new[]
            {
                Result("T-001", DiagnosticSeverity.Healthy, DiagnosticStatus.Passed),
                Result("T-002", DiagnosticSeverity.Critical, DiagnosticStatus.Finding)
            },
            TimeSpan.Zero);

        var table = ResultsTableBuilder.Build(summary);

        Assert.Equal(4, table.Columns.Count);
        Assert.Equal(2, table.Rows.Count);
    }

    [Fact]
    public void Build_ShouldRenderCheckIds()
    {
        var summary = new ScanSummary(
            new[] { Result("T-001", DiagnosticSeverity.Healthy, DiagnosticStatus.Passed) },
            TimeSpan.Zero);

        var text = Render(ResultsTableBuilder.Build(summary));

        Assert.Contains("T-001", text);
        Assert.Contains("SCAN RESULTS", text);
    }

    [Fact]
    public void Build_ShouldRenderStatusAndSeverity()
    {
        var summary = new ScanSummary(
            new[] { Result("T-001", DiagnosticSeverity.Warning, DiagnosticStatus.Finding) },
            TimeSpan.Zero);

        var text = Render(ResultsTableBuilder.Build(summary));

        Assert.Contains("WARNING", text);
        Assert.Contains("FINDING", text);
    }

    [Fact]
    public void Build_EmptySummary_ShouldRenderHeaderOnly()
    {
        var summary = new ScanSummary(Array.Empty<DiagnosticResult>(), TimeSpan.Zero);

        var table = ResultsTableBuilder.Build(summary);

        Assert.Empty(table.Rows);
    }
}