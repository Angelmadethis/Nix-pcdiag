using PCDiag.Memory;

namespace PCDiag.Tests.Memory;

public class PagefileClassifierTests
{
    private static readonly MemoryOptions Options = MemoryOptions.Default;

    [Fact]
    public void SystemManagedWithHeadroom_ShouldBeHealthy()
    {
        var assessment = PagefileClassifier.Classify(
            Mem.Pagefile(config: Mem.SystemManaged(), usage: new[] { Mem.Entry(1024, 85, 93) }),
            Options);

        Assert.Equal(PagefileVerdict.Healthy, assessment.Verdict);
        Assert.Contains(PagefileFlag.SystemManaged, assessment.Flags);
    }

    [Fact]
    public void SystemManagedWithHighPeak_ShouldNotBeFlagged()
    {
        var assessment = PagefileClassifier.Classify(
            Mem.Pagefile(config: Mem.SystemManaged(), usage: new[] { Mem.Entry(1024, 900, 950) }),
            Options);

        Assert.Equal(PagefileVerdict.Healthy, assessment.Verdict);
        Assert.DoesNotContain(PagefileFlag.UsageNearAllocated, assessment.Flags);
        Assert.DoesNotContain(PagefileFlag.PeakHigh, assessment.Flags);
    }

    [Fact]
    public void NoPagefile_ShouldBeSuspiciousNeverCritical()
    {
        var assessment = PagefileClassifier.Classify(
            Mem.Pagefile(config: Mem.None(), usage: Array.Empty<PagefileEntry>()),
            Options);

        Assert.Contains(PagefileFlag.NoPagefile, assessment.Flags);
        Assert.Equal(PagefileVerdict.Suspicious, assessment.Verdict);
    }

    [Fact]
    public void FixedSizeCurrentNearAllocated_ShouldBeSuspicious()
    {
        var assessment = PagefileClassifier.Classify(
            Mem.Pagefile(config: Mem.Custom(@"C:\pagefile.sys 1024 2048"), usage: new[] { Mem.Entry(1024, 1000, 1000) }),
            Options);

        Assert.Contains(PagefileFlag.UsageNearAllocated, assessment.Flags);
        Assert.Equal(PagefileVerdict.Suspicious, assessment.Verdict);
    }

    [Fact]
    public void FixedSizePeakNearAllocated_ShouldBeSuspicious()
    {
        var assessment = PagefileClassifier.Classify(
            Mem.Pagefile(config: Mem.Custom(@"C:\pagefile.sys 1024 2048"), usage: new[] { Mem.Entry(1024, 400, 950) }),
            Options);

        Assert.Contains(PagefileFlag.PeakHigh, assessment.Flags);
        Assert.Equal(PagefileVerdict.Suspicious, assessment.Verdict);
    }

    [Fact]
    public void FixedSizeComfortableUsage_ShouldBeHealthy()
    {
        var assessment = PagefileClassifier.Classify(
            Mem.Pagefile(config: Mem.Custom(@"C:\pagefile.sys 1024 2048"), usage: new[] { Mem.Entry(1024, 400, 600) }),
            Options);

        Assert.Equal(PagefileVerdict.Healthy, assessment.Verdict);
        Assert.DoesNotContain(PagefileFlag.UsageNearAllocated, assessment.Flags);
    }

    [Fact]
    public void ConfigUnavailableWithUsage_ShouldBeHealthyButFlagged()
    {
        var assessment = PagefileClassifier.Classify(
            Mem.Pagefile(config: null, usage: new[] { Mem.Entry(1024, 85, 93) }),
            Options);

        Assert.Contains(PagefileFlag.ConfigUnavailable, assessment.Flags);
        Assert.Equal(PagefileVerdict.Healthy, assessment.Verdict);
    }

    [Fact]
    public void UsageUnavailable_ShouldBeFlagged()
    {
        var assessment = PagefileClassifier.Classify(
            Mem.Pagefile(config: Mem.SystemManaged(), usage: null, usageAvailable: false),
            Options);

        Assert.Contains(PagefileFlag.UsageUnavailable, assessment.Flags);
        Assert.Contains(PagefileFlag.SystemManaged, assessment.Flags);
        Assert.Equal(PagefileVerdict.Healthy, assessment.Verdict);
    }
}