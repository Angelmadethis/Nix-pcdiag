using PCDiag.Memory;

namespace PCDiag.Tests.Memory;

public class MemoryClassifierTests
{
    private static readonly MemoryOptions Options = MemoryOptions.Default;

    [Fact]
    public void ComfortableCommitAndAvailable_ShouldBeHealthy()
    {
        var assessment = MemoryClassifier.Classify(
            Mem.Snapshot(committedGb: 8, commitLimitGb: 20, availableGb: 10, totalGb: 16),
            Options);

        Assert.Equal(MemoryVerdict.Healthy, assessment.Verdict);
        Assert.Empty(assessment.Flags);
    }

    [Theory]
    [InlineData(0.69, MemoryVerdict.Healthy)]
    [InlineData(0.70, MemoryVerdict.Suspicious)]
    [InlineData(0.84, MemoryVerdict.Suspicious)]
    [InlineData(0.85, MemoryVerdict.Warning)]
    [InlineData(0.95, MemoryVerdict.Warning)]
    public void CommitRatioBoundaries_ShouldMapToVerdict(double ratio, MemoryVerdict expected)
    {
        var assessment = MemoryClassifier.Classify(
            Mem.Snapshot(totalGb: 16, availableGb: 10, committedGb: ratio * 20, commitLimitGb: 20),
            Options);

        Assert.Equal(expected, assessment.Verdict);
        Assert.Equal(ratio, assessment.CommitRatio!.Value, 3);
    }

    [Theory]
    [InlineData(0.16, MemoryVerdict.Healthy)]
    [InlineData(0.15, MemoryVerdict.Suspicious)]
    [InlineData(0.06, MemoryVerdict.Suspicious)]
    [InlineData(0.05, MemoryVerdict.Warning)]
    public void AvailableFractionBoundaries_ShouldMapToVerdict(double availableFraction, MemoryVerdict expected)
    {
        var assessment = MemoryClassifier.Classify(
            Mem.Snapshot(totalGb: 16, availableGb: availableFraction * 16, committedGb: 5, commitLimitGb: 20),
            Options);

        Assert.Equal(expected, assessment.Verdict);
        Assert.Equal(availableFraction, assessment.AvailablePercent!.Value, 3);
    }

    [Fact]
    public void LowAvailableAboveFractionButBelowAbsoluteFloor_ShouldFlagAbsoluteLowAvailable()
    {
        var assessment = MemoryClassifier.Classify(
            Mem.Snapshot(totalGb: 20, availableGb: 1.2, committedGb: 5, commitLimitGb: 40),
            Options);

        Assert.Contains(MemoryFlag.AbsoluteLowAvailable, assessment.Flags);
        Assert.Equal(MemoryVerdict.Suspicious, assessment.Verdict);
    }

    [Fact]
    public void HeavyPaging_ShouldFlagButOnlySuspicious()
    {
        var assessment = MemoryClassifier.Classify(
            Mem.Snapshot(committedGb: 8, commitLimitGb: 20, availableGb: 10, pagesPerSec: 250),
            Options);

        Assert.Contains(MemoryFlag.HeavyPaging, assessment.Flags);
        Assert.Equal(MemoryVerdict.Suspicious, assessment.Verdict);
    }

    [Fact]
    public void HeavyPagingWithLowCommit_ShouldNotRaiseVerdictAlone()
    {
        var assessment = MemoryClassifier.Classify(
            Mem.Snapshot(committedGb: 8, commitLimitGb: 20, availableGb: 10, pagesPerSec: 300),
            Options);

        Assert.Equal(MemoryVerdict.Suspicious, assessment.Verdict);
    }

    [Fact]
    public void UnavailableCounters_ShouldBeFlagsNotFabricatedData()
    {
        var assessment = MemoryClassifier.Classify(
            Mem.Snapshot(
                osAvailable: false,
                perfAvailable: false,
                pagefileAvailable: false,
                availableGb: null,
                committedGb: null,
                commitLimitGb: null,
                pagesPerSec: null,
                pagefileCurrentMb: null,
                pagefileAllocatedMb: null),
            Options);

        Assert.Contains(MemoryFlag.OperatingSystemInfoUnavailable, assessment.Flags);
        Assert.Contains(MemoryFlag.PerfCountersUnavailable, assessment.Flags);
        Assert.Contains(MemoryFlag.PagefileUsageUnavailable, assessment.Flags);
        Assert.Null(assessment.CommitRatio);
        Assert.Null(assessment.AvailablePercent);
    }

    [Fact]
    public void CommitHighPlusCriticalAvailable_ShouldBeWarning()
    {
        var assessment = MemoryClassifier.Classify(
            Mem.Snapshot(totalGb: 16, availableGb: 0.5, committedGb: 18, commitLimitGb: 20),
            Options);

        Assert.Contains(MemoryFlag.CommitHigh, assessment.Flags);
        Assert.Contains(MemoryFlag.CriticalAvailable, assessment.Flags);
        Assert.Equal(MemoryVerdict.Warning, assessment.Verdict);
    }
}