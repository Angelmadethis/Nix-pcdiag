using PCDiag.Checks.Performance;
using PCDiag.Core;
using PCDiag.Memory;

namespace PCDiag.Tests.Memory;

public class PagefileCheckTests
{
    private static DiagnosticContext Ctx() => new(mode: ScanMode.Standard);

    private static Task<PCDiag.Core.DiagnosticResult> Run(FakePagefileSource source)
        => new PagefileCheck(source).ExecuteAsync(Ctx(), CancellationToken.None);

    [Fact]
    public async Task SystemManaged_ShouldPassHealthy()
    {
        source.Info = Mem.Pagefile(config: Mem.SystemManaged(), usage: new[] { Mem.Entry(1024, 85, 93) });
        var result = await Run(source);

        Assert.Equal(DiagnosticStatus.Passed, result.Status);
        Assert.Equal(DiagnosticSeverity.Healthy, result.Severity);
        Assert.Contains(result.Evidence, e => e.Description == "Configuration");
        Assert.Contains("System-managed", result.Evidence[0].Value);
    }

    [Fact]
    public async Task NoPagefile_ShouldBeSuspiciousFinding()
    {
        source.Info = Mem.Pagefile(config: Mem.None(), usage: Array.Empty<PagefileEntry>());
        var result = await Run(source);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Suspicious, result.Severity);
        Assert.Contains("No pagefile is configured", result.Detail);
        Assert.Contains(result.Limitations, l => l.Contains("never recommends disabling"));
    }

    [Fact]
    public async Task FixedSizeNearAllocated_ShouldBeSuspiciousFinding()
    {
        source.Info = Mem.Pagefile(config: Mem.Custom(@"C:\pagefile.sys 1024 2048"), usage: new[] { Mem.Entry(1024, 1000, 1000) });
        var result = await Run(source);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Suspicious, result.Severity);
        Assert.Contains("cannot grow", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfigAndUsageUnavailable_ShouldBeUnavailable()
    {
        source.Info = new PagefileInfo { Config = null, UsageAvailable = false };
        var result = await Run(source);

        Assert.Equal(DiagnosticStatus.Unavailable, result.Status);
    }

    private readonly FakePagefileSource source = new();
}