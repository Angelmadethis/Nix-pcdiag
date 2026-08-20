using PCDiag.Interrupts;

namespace PCDiag.Tests.Interrupts;

public class InterruptClassifierTests
{
    private static readonly InterruptOptions Options = InterruptOptions.Default;

    [Fact]
    public void LowActivity_ShouldBeHealthy()
    {
        var snapshot = Int.Snapshot(
            Int.Total(interrupts: 1_000, dpcs: 200, privileged: 5, cpu: 10),
            Int.Core("0", 500, 100, 4, 8),
            Int.Core("1", 500, 100, 6, 12));

        var assessment = InterruptClassifier.Classify(snapshot, Options);

        Assert.Equal(InterruptVerdict.Healthy, assessment.Verdict);
        Assert.Empty(assessment.Flags);
        Assert.Equal(0.9, InterruptClassifier.ComputeConfidence(assessment));
    }

    [Theory]
    [InlineData(12_000, InterruptVerdict.Suspicious, InterruptFlag.ElevatedInterruptRate, 0.5)]
    [InlineData(30_000, InterruptVerdict.Warning, InterruptFlag.HighInterruptRate, 0.55)]
    public void ElevatedInterruptRate_ShouldFlag(double rate, InterruptVerdict expectedVerdict, InterruptFlag flag, double expectedConfidence)
    {
        var assessment = InterruptClassifier.Classify(
            Int.Snapshot(Int.Total(interrupts: rate, dpcs: 200, privileged: 5, cpu: 20)),
            Options);

        Assert.Equal(expectedVerdict, assessment.Verdict);
        Assert.Contains(flag, assessment.Flags);
        Assert.Equal(expectedConfidence, InterruptClassifier.ComputeConfidence(assessment), 2);
    }

    [Theory]
    [InlineData(2_500, InterruptVerdict.Suspicious, InterruptFlag.ElevatedDpcRate)]
    [InlineData(9_000, InterruptVerdict.Warning, InterruptFlag.HighDpcRate)]
    public void ElevatedDpcRate_ShouldFlag(double rate, InterruptVerdict expectedVerdict, InterruptFlag flag)
    {
        var assessment = InterruptClassifier.Classify(
            Int.Snapshot(Int.Total(interrupts: 1_000, dpcs: rate, privileged: 5, cpu: 20)),
            Options);

        Assert.Equal(expectedVerdict, assessment.Verdict);
        Assert.Contains(flag, assessment.Flags);
    }

    [Theory]
    [InlineData(25, InterruptVerdict.Suspicious, InterruptFlag.HighPrivilegedTime)]
    [InlineData(45, InterruptVerdict.Warning, InterruptFlag.VeryHighPrivilegedTime)]
    public void HighPrivilegedTime_ShouldFlag(double privileged, InterruptVerdict expectedVerdict, InterruptFlag flag)
    {
        var assessment = InterruptClassifier.Classify(
            Int.Snapshot(Int.Total(interrupts: 1_000, dpcs: 200, privileged: privileged, cpu: 50)),
            Options);

        Assert.Equal(expectedVerdict, assessment.Verdict);
        Assert.Contains(flag, assessment.Flags);
    }

    [Fact]
    public void ConcentratedInterruptLoad_ShouldFlagSuspicious()
    {
        var snapshot = Int.Snapshot(
            Int.Total(interrupts: 12_000, dpcs: 200, privileged: 5, cpu: 30),
            Int.Core("0", 1_000),
            Int.Core("1", 1_000),
            Int.Core("2", 30_000));

        var assessment = InterruptClassifier.Classify(snapshot, Options);

        Assert.Equal(InterruptVerdict.Suspicious, assessment.Verdict);
        Assert.Contains(InterruptFlag.ConcentratedInterruptLoad, assessment.Flags);
        Assert.Contains(InterruptFlag.ElevatedInterruptRate, assessment.Flags);
    }

    [Fact]
    public void MultipleSignals_ShouldRaiseConfidence()
    {
        var snapshot = Int.Snapshot(
            Int.Total(interrupts: 30_000, dpcs: 9_000, privileged: 50, cpu: 60),
            Int.Core("0", 1_000, 100, 5, 5),
            Int.Core("1", 1_000, 100, 5, 5),
            Int.Core("2", 30_000, 300, 8, 8));

        var assessment = InterruptClassifier.Classify(snapshot, Options);

        Assert.Equal(InterruptVerdict.Warning, assessment.Verdict);
        Assert.Equal(0.79, InterruptClassifier.ComputeConfidence(assessment), 2);
    }

    [Fact]
    public void UnavailableCounters_ShouldFlagAndReportLowConfidence()
    {
        var assessment = InterruptClassifier.Classify(
            new InterruptSnapshot { Total = null, Cores = Array.Empty<InterruptCoreSample>() },
            Options);

        Assert.Equal(InterruptVerdict.Healthy, assessment.Verdict);
        Assert.Contains(InterruptFlag.CountersUnavailable, assessment.Flags);
        Assert.Equal(0.4, InterruptClassifier.ComputeConfidence(assessment));
    }

    [Fact]
    public void PartialCounters_ShouldBeConsideredAvailable()
    {
        var snapshot = Int.Snapshot(Int.Total(interrupts: 500, dpcs: null, privileged: null, cpu: null));

        var assessment = InterruptClassifier.Classify(snapshot, Options);

        Assert.DoesNotContain(InterruptFlag.CountersUnavailable, assessment.Flags);
        Assert.Equal(InterruptVerdict.Healthy, assessment.Verdict);
    }

    [Fact]
    public void NoTopology_ShouldFlagTopologyUnavailable()
    {
        var assessment = InterruptClassifier.Classify(
            Int.Snapshot(Int.Total(interrupts: 1_000, dpcs: 200)),
            Options);

        Assert.Contains(InterruptFlag.TopologyUnavailable, assessment.Flags);
        Assert.Equal(InterruptVerdict.Healthy, assessment.Verdict);
    }
}