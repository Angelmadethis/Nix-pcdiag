using PCDiag.Checks.Hardware;
using PCDiag.Core;

namespace PCDiag.Tests.Events;

public class WheaCheckTests
{
    private static DiagnosticContext Ctx() => new(mode: ScanMode.Standard);

    private static Task<DiagnosticResult> Run(FakeEventLogSource source)
        => new WheaCheck(source).ExecuteAsync(Ctx(), CancellationToken.None);

    [Fact]
    public async Task NoWheaEvents_ShouldPass()
    {
        var result = await Run(new FakeEventLogSource());

        Assert.Equal(DiagnosticStatus.Passed, result.Status);
        Assert.Equal(DiagnosticSeverity.Healthy, result.Severity);
        Assert.Contains("No WHEA hardware error records", result.Summary);
    }

    [Fact]
    public async Task NonWheaEvents_AreIgnored()
    {
        var source = new FakeEventLogSource();
        source.Records.Add(Ev.New("disk", 11));
        source.Records.Add(Ev.New("Ntfs", 55));

        var result = await Run(source);

        Assert.Equal(DiagnosticStatus.Passed, result.Status);
        Assert.Equal(DiagnosticSeverity.Healthy, result.Severity);
    }

    [Fact]
    public async Task TwoCorrectedWhea_IsSuspiciousWithPattern()
    {
        var source = new FakeEventLogSource();
        source.Records.Add(Ev.New("Microsoft-Windows-WHEA-Logger", 18, DateTime.UtcNow.AddDays(-3)));
        source.Records.Add(Ev.New("Microsoft-Windows-WHEA-Logger", 19, DateTime.UtcNow.AddHours(-2)));

        var result = await Run(source);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Suspicious, result.Severity);
        Assert.Contains(result.Evidence, e => e.Description == "Repeated WHEA errors");
        Assert.Contains(result.Recommendations, r => r.Text.Contains("corrected"));
    }

    [Fact]
    public async Task FatalWhea_IsCritical()
    {
        var source = new FakeEventLogSource();
        source.Records.Add(Ev.New("Microsoft-Windows-WHEA-Logger", 1));

        var result = await Run(source);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Critical, result.Severity);
        Assert.Contains(result.Evidence, e => e.Description == "Fatal WHEA hardware error");
        Assert.Contains(result.Recommendations, r => r.Text.Contains("hardware as suspect") || r.Text.Contains("memory"));
    }

    [Fact]
    public async Task SingleCorrectedWhea_IsSuspiciousButNotRepeated()
    {
        var source = new FakeEventLogSource();
        source.Records.Add(Ev.New("Microsoft-Windows-WHEA-Logger", 18));

        var result = await Run(source);

        Assert.Equal(DiagnosticSeverity.Suspicious, result.Severity);
        Assert.DoesNotContain(result.Evidence, e => e.Description == "Repeated WHEA errors");
        Assert.Contains(result.Evidence, e => e.Description == "WHEA Hardware Errors");
    }
}