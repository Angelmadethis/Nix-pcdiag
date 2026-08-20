using PCDiag.Core;
using PCDiag.Events;

namespace PCDiag.Tests.Events;

public class EventPatternDetectorTests
{
    private static readonly EventLogOptions Options = EventLogOptions.Default;

    private static IReadOnlyList<EventPattern> Detect(params EventCategorySummary[] summaries)
        => EventPatternDetector.Detect(summaries, Options);

    [Fact]
    public void Empty_ProducesNoPatterns()
    {
        Assert.Empty(Detect());
    }

    [Fact]
    public void SingleWhea_DoesNotFormPattern()
    {
        var summaries = new[] { Ev.Category(EventCategory.Whea, 1, DiagnosticSeverity.Suspicious, (18, DiagnosticSeverity.Suspicious)) };

        var patterns = Detect(summaries);

        Assert.DoesNotContain(patterns, p => p.Name.Contains("Repeated WHEA"));
    }

    [Fact]
    public void TwoWhea_IsSuspiciousRepeatedPattern()
    {
        var summaries = new[] { Ev.Category(EventCategory.Whea, 2, DiagnosticSeverity.Suspicious, (18, DiagnosticSeverity.Suspicious)) };

        var patterns = Detect(summaries);

        var pattern = Assert.Single(patterns);
        Assert.Equal("Repeated WHEA errors", pattern.Name);
        Assert.Equal(DiagnosticSeverity.Suspicious, pattern.Severity);
    }

    [Fact]
    public void SixWhea_IsWarningRepeatedPattern()
    {
        var summaries = new[] { Ev.Category(EventCategory.Whea, 6, DiagnosticSeverity.Suspicious, (18, DiagnosticSeverity.Suspicious)) };

        var pattern = Assert.Single(Detect(summaries));
        Assert.Equal(DiagnosticSeverity.Warning, pattern.Severity);
    }

    [Fact]
    public void FatalWhea_IsCriticalEvenWithSingleEvent()
    {
        var summaries = new[] { Ev.Category(EventCategory.Whea, 1, DiagnosticSeverity.Critical, (1, DiagnosticSeverity.Critical)) };

        var patterns = Detect(summaries);

        Assert.Contains(patterns, p => p.Name == "Fatal WHEA hardware error" && p.Severity == DiagnosticSeverity.Critical);
    }

    [Fact]
    public void DiskPagingError_IsCritical()
    {
        var summaries = new[] { Ev.Category(EventCategory.Disk, 1, DiagnosticSeverity.Critical, (51, DiagnosticSeverity.Critical)) };

        var patterns = Detect(summaries);

        Assert.Contains(patterns, p => p.Name == "Disk paging I/O error" && p.Severity == DiagnosticSeverity.Critical);
    }

    [Fact]
    public void RepeatedDisk_IsSuspiciousAtTwo()
    {
        var summaries = new[] { Ev.Category(EventCategory.Disk, 2, DiagnosticSeverity.Warning, (11, DiagnosticSeverity.Warning)) };

        var pattern = Assert.Single(Detect(summaries));
        Assert.Equal("Repeated disk errors", pattern.Name);
        Assert.Equal(DiagnosticSeverity.Suspicious, pattern.Severity);
    }

    [Fact]
    public void GpuResets_TwoIsSuspicious_FiveIsWarning()
    {
        var suspicious = Detect(Ev.Category(EventCategory.DisplayGpu, 2, DiagnosticSeverity.Warning, (4101, DiagnosticSeverity.Warning)));
        var warning = Detect(Ev.Category(EventCategory.DisplayGpu, 5, DiagnosticSeverity.Warning, (4101, DiagnosticSeverity.Warning)));

        Assert.Contains(suspicious, p => p.Name == "Repeated GPU driver resets" && p.Severity == DiagnosticSeverity.Suspicious);
        Assert.Contains(warning, p => p.Name == "Repeated GPU driver resets" && p.Severity == DiagnosticSeverity.Warning);
    }

    [Theory]
    [InlineData(5, DiagnosticSeverity.Suspicious)]
    [InlineData(15, DiagnosticSeverity.Warning)]
    public void NetworkResets_Thresholds(int count, DiagnosticSeverity expected)
    {
        var patterns = Detect(Ev.Category(EventCategory.NetworkAdapter, count, DiagnosticSeverity.Warning, (27, DiagnosticSeverity.Warning)));

        Assert.Contains(patterns, p => p.Name == "Repeated network adapter resets" && p.Severity == expected);
    }

    [Theory]
    [InlineData(3, DiagnosticSeverity.Suspicious)]
    [InlineData(8, DiagnosticSeverity.Warning)]
    public void UsbResets_Thresholds(int count, DiagnosticSeverity expected)
    {
        var patterns = Detect(Ev.Category(EventCategory.Usb, count, DiagnosticSeverity.Warning, (219, DiagnosticSeverity.Warning)));

        Assert.Contains(patterns, p => p.Name == "Repeated USB resets" && p.Severity == expected);
    }

    [Fact]
    public void Bugcheck_IsCritical()
    {
        var summaries = new[] { Ev.Category(EventCategory.DriverFailure, 1, DiagnosticSeverity.Critical, (1001, DiagnosticSeverity.Critical)) };

        var patterns = Detect(summaries);

        Assert.Contains(patterns, p => p.Name == "System bugcheck" && p.Severity == DiagnosticSeverity.Critical);
    }

    [Fact]
    public void RepeatedDriverFailures_IsSuspicious()
    {
        var summaries = new[] { Ev.Category(EventCategory.DriverFailure, 2, DiagnosticSeverity.Warning, (7026, DiagnosticSeverity.Warning)) };

        var patterns = Detect(summaries);

        Assert.Contains(patterns, p => p.Name == "Repeated driver failures" && p.Severity == DiagnosticSeverity.Suspicious);
    }

    [Fact]
    public void KernelPower41_IsUnexpectedShutdownCritical()
    {
        var summaries = new[] { Ev.Category(EventCategory.KernelPower, 1, DiagnosticSeverity.Critical, (41, DiagnosticSeverity.Critical)) };

        var patterns = Detect(summaries);

        Assert.Contains(patterns, p => p.Name == "Unexpected system shutdown" && p.Severity == DiagnosticSeverity.Critical);
    }

    [Fact]
    public void BelowThreshold_IsNotDetected()
    {
        var summaries = new[] { Ev.Category(EventCategory.ServiceFailure, 2, DiagnosticSeverity.Warning, (7034, DiagnosticSeverity.Warning)) };

        var patterns = Detect(summaries);

        Assert.DoesNotContain(patterns, p => p.Name.Contains("service failures", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InformationalOnly_DoesNotFormPattern()
    {
        var summaries = new[] { Ev.Category(EventCategory.ServiceFailure, 30, DiagnosticSeverity.Info, (7040, DiagnosticSeverity.Info)) };

        var patterns = Detect(summaries);

        Assert.DoesNotContain(patterns, p => p.Name.Contains("service failures", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MixedInfoAndWarning_CountsOnlyConcerningEvents()
    {
        var summaries = new[]
        {
            Ev.Category(EventCategory.ServiceFailure, 57, DiagnosticSeverity.Warning,
                (7040, DiagnosticSeverity.Info), (7031, DiagnosticSeverity.Warning), (7031, DiagnosticSeverity.Warning))
        };

        var patterns = Detect(summaries);

        Assert.DoesNotContain(patterns, p => p.Name.Contains("service failures", StringComparison.OrdinalIgnoreCase));
    }
}