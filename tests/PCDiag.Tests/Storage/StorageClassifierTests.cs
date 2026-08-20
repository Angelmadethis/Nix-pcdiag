using PCDiag.Storage;

namespace PCDiag.Tests.Storage;

public class StorageClassifierTests
{
    private static readonly StorageOptions Options = StorageOptions.Default;

    [Fact]
    public void HealthyEverything_ShouldBeHealthy()
    {
        var assessment = StorageClassifier.Classify(
            Sto.Snapshot(
                volumes: new[] { Sto.Volume("C:", 0.30) },
                disks: new[] { Sto.Disk(Sto.HealthyHealth()) }),
            Options);

        Assert.Equal(StorageVerdict.Healthy, assessment.Verdict);
        Assert.Empty(assessment.Flags);
    }

    [Theory]
    [InlineData(0.16, StorageVerdict.Healthy)]
    [InlineData(0.15, StorageVerdict.Healthy)]
    [InlineData(0.14, StorageVerdict.Suspicious)]
    [InlineData(0.05, StorageVerdict.Suspicious)]
    [InlineData(0.049, StorageVerdict.Warning)]
    public void FreeSpaceBoundaries_ShouldMapToVerdict(double freeFraction, StorageVerdict expected)
    {
        var assessment = StorageClassifier.Classify(
            Sto.Snapshot(volumes: new[] { Sto.Volume("C:", freeFraction) }),
            Options);

        Assert.Equal(expected, assessment.Verdict);
    }

    [Fact]
    public void DirtyVolume_ShouldBeWarning()
    {
        var assessment = StorageClassifier.Classify(
            Sto.Snapshot(volumes: new[] { Sto.Volume("C:", 0.30, dirty: true) }),
            Options);

        Assert.Contains(StorageFlag.DirtyVolume, assessment.Flags);
        Assert.Equal(StorageVerdict.Warning, assessment.Verdict);
    }

    [Fact]
    public void StackHealthWarning_ShouldBeSuspicious()
    {
        var assessment = StorageClassifier.Classify(
            Sto.Snapshot(disks: new[]
            {
                Sto.Disk(new StorageHealth { StackQueried = true, StackState = StorageHealthState.Warning, HasReliabilityCounters = false })
            }),
            Options);

        Assert.Contains(StorageFlag.StackHealthWarning, assessment.Flags);
        Assert.Equal(StorageVerdict.Suspicious, assessment.Verdict);
    }

    [Fact]
    public void StackHealthUnhealthy_ShouldBeWarning()
    {
        var assessment = StorageClassifier.Classify(
            Sto.Snapshot(disks: new[]
            {
                Sto.Disk(new StorageHealth { StackQueried = true, StackState = StorageHealthState.Unhealthy, HasReliabilityCounters = false })
            }),
            Options);

        Assert.Contains(StorageFlag.StackHealthUnhealthy, assessment.Flags);
        Assert.Equal(StorageVerdict.Warning, assessment.Verdict);
    }

    [Fact]
    public void UnknownHealthWithoutReliability_ShouldBeFlaggedButNotClaimPerfect()
    {
        var assessment = StorageClassifier.Classify(
            Sto.Snapshot(disks: new[] { Sto.Disk(Sto.UnknownNoReliability()) }),
            Options);

        Assert.Contains(StorageFlag.StackHealthUnknown, assessment.Flags);
        Assert.Contains(StorageFlag.ReliabilityUnavailable, assessment.Flags);
        Assert.Equal(StorageVerdict.Healthy, assessment.Verdict);
    }

    [Fact]
    public void WearNearLimit_ShouldBeWarning()
    {
        var assessment = StorageClassifier.Classify(
            Sto.Snapshot(disks: new[]
            {
                Sto.Disk(new StorageHealth
                {
                    StackQueried = true,
                    StackState = StorageHealthState.Healthy,
                    HasReliabilityCounters = true,
                    WearPercent = 95,
                    TemperatureCelsius = 40
                })
            }),
            Options);

        Assert.Contains(StorageFlag.WearHigh, assessment.Flags);
        Assert.Equal(StorageVerdict.Warning, assessment.Verdict);
    }

    [Fact]
    public void HotDrive_ShouldBeSuspicious()
    {
        var assessment = StorageClassifier.Classify(
            Sto.Snapshot(disks: new[]
            {
                Sto.Disk(new StorageHealth
                {
                    StackQueried = true,
                    StackState = StorageHealthState.Healthy,
                    HasReliabilityCounters = true,
                    WearPercent = 10,
                    TemperatureCelsius = 75
                })
            }),
            Options);

        Assert.Contains(StorageFlag.TemperatureHigh, assessment.Flags);
        Assert.Equal(StorageVerdict.Suspicious, assessment.Verdict);
    }

    [Fact]
    public void UncorrectedErrors_ShouldBeWarning()
    {
        var assessment = StorageClassifier.Classify(
            Sto.Snapshot(disks: new[]
            {
                Sto.Disk(new StorageHealth
                {
                    StackQueried = true,
                    StackState = StorageHealthState.Healthy,
                    HasReliabilityCounters = true,
                    WearPercent = 10,
                    TemperatureCelsius = 40,
                    ReadErrorsUncorrected = 3
                })
            }),
            Options);

        Assert.Contains(StorageFlag.UncorrectedErrors, assessment.Flags);
        Assert.Equal(StorageVerdict.Warning, assessment.Verdict);
    }

    [Fact]
    public void IdleLatency_ShouldBeFlaggedButNotJudged()
    {
        var assessment = StorageClassifier.Classify(
            Sto.Snapshot(latency: new[] { Sto.Latency("0 C:") }),
            Options);

        Assert.Contains(StorageFlag.LatencyIdle, assessment.Flags);
        Assert.Equal(StorageVerdict.Healthy, assessment.Verdict);
    }

    [Theory]
    [InlineData(0.029, StorageVerdict.Healthy)]
    [InlineData(0.030, StorageVerdict.Suspicious)]
    [InlineData(0.099, StorageVerdict.Suspicious)]
    [InlineData(0.100, StorageVerdict.Warning)]
    public void ActiveLatencyBoundaries_ShouldMapToVerdict(double seconds, StorageVerdict expected)
    {
        var assessment = StorageClassifier.Classify(
            Sto.Snapshot(latency: new[] { Sto.Latency("0 C:", readSeconds: seconds, active: true) }),
            Options);

        Assert.Equal(expected, assessment.Verdict);
    }

    [Fact]
    public void UnavailableSources_ShouldBeFlags()
    {
        var assessment = StorageClassifier.Classify(
            Sto.Snapshot(volumesAvailable: false, disksAvailable: false, namespaceAvailable: false),
            Options);

        Assert.Contains(StorageFlag.VolumesUnavailable, assessment.Flags);
        Assert.Contains(StorageFlag.DisksUnavailable, assessment.Flags);
    }

    [Fact]
    public void CombinedLowSpaceAndUnhealthy_ShouldBeWarning()
    {
        var assessment = StorageClassifier.Classify(
            Sto.Snapshot(
                volumes: new[] { Sto.Volume("C:", 0.04) },
                disks: new[]
                {
                    Sto.Disk(new StorageHealth { StackQueried = true, StackState = StorageHealthState.Unhealthy, HasReliabilityCounters = false })
                }),
            Options);

        Assert.Contains(StorageFlag.CriticalFreeSpace, assessment.Flags);
        Assert.Contains(StorageFlag.StackHealthUnhealthy, assessment.Flags);
        Assert.Equal(StorageVerdict.Warning, assessment.Verdict);
    }
}