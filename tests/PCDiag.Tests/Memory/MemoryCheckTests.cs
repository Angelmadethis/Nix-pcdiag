using PCDiag.Checks.Performance;
using PCDiag.Core;

namespace PCDiag.Tests.Memory;

public class MemoryCheckTests
{
    private static DiagnosticContext Ctx() => new(mode: ScanMode.Standard);

    private static Task<PCDiag.Core.DiagnosticResult> Run(FakeMemorySnapshotSource source)
        => new MemoryCheck(source).ExecuteAsync(Ctx(), CancellationToken.None);

    [Fact]
    public async Task ComfortableSnapshot_ShouldPassHealthy()
    {
        source.Snapshot = Mem.Snapshot(committedGb: 8, commitLimitGb: 20, availableGb: 10, totalGb: 16);
        var result = await Run(source);

        Assert.Equal(DiagnosticStatus.Passed, result.Status);
        Assert.Equal(DiagnosticSeverity.Healthy, result.Severity);
        Assert.Empty(result.Recommendations);
        Assert.Contains(result.Evidence, e => e.Description == "Installed RAM");
        Assert.Contains(result.Evidence, e => e.Description == "Committed Memory");
    }

    [Fact]
    public async Task HighCommit_ShouldBeWarningFinding()
    {
        source.Snapshot = Mem.Snapshot(totalGb: 16, availableGb: 10, committedGb: 18, commitLimitGb: 20);
        var result = await Run(source);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Warning, result.Severity);
        Assert.NotEmpty(result.Recommendations);
        Assert.Contains("high", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ElevatedCommit_ShouldBeSuspiciousFinding()
    {
        source.Snapshot = Mem.Snapshot(totalGb: 16, availableGb: 10, committedGb: 14, commitLimitGb: 20);
        var result = await Run(source);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Suspicious, result.Severity);
        Assert.NotEmpty(result.Recommendations);
    }

    [Fact]
    public async Task AllSourcesUnavailable_ShouldBeUnavailable()
    {
        source.Snapshot = Mem.Snapshot(osAvailable: false, perfAvailable: false, pagefileAvailable: false);
        var result = await Run(source);

        Assert.Equal(DiagnosticStatus.Unavailable, result.Status);
        Assert.Equal(DiagnosticSeverity.Info, result.Severity);
    }

    [Fact]
    public async Task EvidenceListsUnavailableSources()
    {
        source.Snapshot = Mem.Snapshot(committedGb: 8, commitLimitGb: 20, availableGb: 10, perfAvailable: false);
        var result = await Run(source);

        Assert.Contains(result.Evidence, e => e.Description == "Data Availability");
    }

    private readonly FakeMemorySnapshotSource source = new();
}