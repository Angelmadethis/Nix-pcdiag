using PCDiag.Checks.Windows;
using PCDiag.Core;

namespace PCDiag.Tests.Events;

public class EventLogCheckTests
{
    private static DiagnosticContext Ctx() => new(mode: ScanMode.Standard);

    private static Task<PCDiag.Core.DiagnosticResult> Run(FakeEventLogSource source)
        => new EventLogCheck(source).ExecuteAsync(Ctx(), CancellationToken.None);

    [Fact]
    public async Task NoRelevantEvents_ShouldPassHealthy()
    {
        var result = await Run(new FakeEventLogSource());

        Assert.Equal(DiagnosticStatus.Passed, result.Status);
        Assert.Equal(DiagnosticSeverity.Healthy, result.Severity);
        Assert.Contains("No relevant error events", result.Summary);
        Assert.Empty(result.Recommendations);
    }

    [Fact]
    public async Task SingleDiskError_ShouldBeWarningFinding()
    {
        var source = new FakeEventLogSource();
        source.Records.Add(Ev.New("disk", 11));

        var result = await Run(source);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Warning, result.Severity);
        Assert.Contains(result.Evidence, e => e.Description == "Disk Errors");
        Assert.DoesNotContain(result.Evidence, e => e.Description.Contains("Repeated", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RepeatedWhea_ShouldBeSuspiciousWithPattern()
    {
        var source = new FakeEventLogSource();
        source.Records.Add(Ev.New("Microsoft-Windows-WHEA-Logger", 18, DateTime.UtcNow.AddDays(-2)));
        source.Records.Add(Ev.New("Microsoft-Windows-WHEA-Logger", 18, DateTime.UtcNow.AddHours(-1)));

        var result = await Run(source);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Suspicious, result.Severity);
        Assert.Contains(result.Evidence, e => e.Description == "Repeated WHEA errors");
        Assert.Contains("Repeated WHEA errors", result.Summary);
    }

    [Fact]
    public async Task Bugcheck_ShouldBeCriticalWithPattern()
    {
        var source = new FakeEventLogSource();
        source.Records.Add(Ev.New("BugCheck", 1001));

        var result = await Run(source);

        Assert.Equal(DiagnosticSeverity.Critical, result.Severity);
        Assert.Contains(result.Evidence, e => e.Description == "System bugcheck");
        Assert.Contains(result.Recommendations, r => r.Text.Contains("bugcheck"));
    }

    [Fact]
    public async Task KernelPower41_ShouldNotClaimACause()
    {
        var source = new FakeEventLogSource();
        source.Records.Add(Ev.New("Microsoft-Windows-Kernel-Power", 41));

        var result = await Run(source);

        Assert.Equal(DiagnosticSeverity.Critical, result.Severity);
        Assert.Contains(result.Evidence, e => e.Description == "Unexpected system shutdown");
        Assert.Contains("does not identify", result.Detail);
        Assert.Contains("possibilities", string.Join(" ", result.PossibleCauses));
    }

    [Fact]
    public async Task UnavailableChannel_IsReportedNotHidden()
    {
        var source = new FakeEventLogSource();
        source.Statuses.Add(new PCDiag.Events.EventChannelStatus { Channel = "Microsoft-Windows-WHEA-Logger/Operational", IsAvailable = false, Reason = "Access denied" });
        source.Records.Add(Ev.New("disk", 11));

        var result = await Run(source);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Contains(result.Evidence, e => e.Description == "Inspected Channels" && e.Value.Contains("unavailable"));
    }

    [Fact]
    public async Task MultipleCategories_AreAllReported()
    {
        var source = new FakeEventLogSource();
        source.Records.Add(Ev.New("disk", 11));
        source.Records.Add(Ev.New("Ntfs", 55));

        var result = await Run(source);

        Assert.Contains(result.Evidence, e => e.Description == "Disk Errors");
        Assert.Contains(result.Evidence, e => e.Description == "NTFS Filesystem Errors");
    }

    [Fact]
    public async Task OnlyInfoEvents_IsInfoFinding()
    {
        var source = new FakeEventLogSource();
        source.Records.Add(Ev.New("Service Control Manager", 7036));

        var result = await Run(source);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Info, result.Severity);
    }
}