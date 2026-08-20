using PCDiag.Checks.Hardware;
using PCDiag.Core;

namespace PCDiag.Tests.Events;

public class DriverCheckTests
{
    private static DiagnosticContext Ctx() => new(mode: ScanMode.Standard);

    private static Task<DiagnosticResult> Run(FakeEventLogSource source)
        => new DriverCheck(source).ExecuteAsync(Ctx(), CancellationToken.None);

    [Fact]
    public async Task NoDriverEvents_ShouldPass()
    {
        var result = await Run(new FakeEventLogSource());

        Assert.Equal(DiagnosticStatus.Passed, result.Status);
        Assert.Equal(DiagnosticSeverity.Healthy, result.Severity);
        Assert.Contains("No display, driver", result.Summary);
    }

    [Fact]
    public async Task NonDriverEvents_AreIgnored()
    {
        var source = new FakeEventLogSource();
        source.Records.Add(Ev.New("disk", 11));
        source.Records.Add(Ev.New("Microsoft-Windows-WHEA-Logger", 18));
        source.Records.Add(Ev.New("Service Control Manager", 7034));

        var result = await Run(source);

        Assert.Equal(DiagnosticStatus.Passed, result.Status);
        Assert.Equal(DiagnosticSeverity.Healthy, result.Severity);
    }

    [Fact]
    public async Task RepeatedTdr_IsSuspiciousWithPattern()
    {
        var source = new FakeEventLogSource();
        source.Records.Add(Ev.New("Display", 4101, DateTime.UtcNow.AddDays(-4)));
        source.Records.Add(Ev.New("nvlddmkm", 4101, DateTime.UtcNow.AddHours(-3)));

        var result = await Run(source);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Suspicious, result.Severity);
        Assert.Contains(result.Evidence, e => e.Description == "Repeated GPU driver resets");
        Assert.Contains(result.Recommendations, r => r.Text.Contains("display driver") || r.Text.Contains("display-driver"));
        Assert.Contains(result.Evidence, e => e.Value.Contains("NVIDIA display driver"));
    }

    [Fact]
    public async Task SingleStorageControllerError_IsWarning()
    {
        var source = new FakeEventLogSource();
        source.Records.Add(Ev.New("Microsoft-Windows-StorAHCI", 153));

        var result = await Run(source);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Warning, result.Severity);
        Assert.Contains(result.Evidence, e => e.Description == "Storage Controller (storahci/stornvme)");
    }

    [Fact]
    public async Task Bugcheck_IsCritical()
    {
        var source = new FakeEventLogSource();
        source.Records.Add(Ev.New("Microsoft-Windows-WER-SystemErrorReporting", 1001));

        var result = await Run(source);

        Assert.Equal(DiagnosticSeverity.Critical, result.Severity);
        Assert.Contains(result.Evidence, e => e.Description == "System bugcheck");
        Assert.Contains(result.Recommendations, r => r.Text.Contains("Minidump"));
    }

    [Fact]
    public async Task DriverLoadFailure_IsWarningFinding()
    {
        var source = new FakeEventLogSource();
        source.Records.Add(Ev.New("Service Control Manager", 7026));

        var result = await Run(source);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Warning, result.Severity);
        Assert.Contains(result.Evidence, e => e.Description == "Driver Failures");
    }
}