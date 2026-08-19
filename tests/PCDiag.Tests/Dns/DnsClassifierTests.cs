using PCDiag.Dns;

namespace PCDiag.Tests.Dns;

public class DnsClassifierTests
{
    private static DnsOptions Options(double slowMs = 500, double unreliableRate = 0.4)
        => DnsOptions.Default with { SlowLatencyMs = slowMs, UnreliableFailureRate = unreliableRate };

    private static DnsMeasurementStats Stats(
        int attempts,
        int successes,
        int failures = 0,
        int timeouts = 0,
        double? avgMs = 10)
        => new()
        {
            Attempts = attempts,
            Successes = successes,
            Failures = failures,
            Timeouts = timeouts,
            SuccessRate = attempts > 0 ? (double)successes / attempts : 0,
            FailureRate = attempts > 0 ? (double)(failures + timeouts) / attempts : 0,
            AvgLatencyMs = avgMs,
            MinLatencyMs = avgMs is double min ? (long)min : null,
            MaxLatencyMs = avgMs is double max ? (long)max : null
        };

    [Fact]
    public void Classify_AllSuccessLowLatency_ShouldBeHealthy()
    {
        var stats = Stats(5, 5, avgMs: 20);

        Assert.Equal(DnsHealth.Healthy, DnsClassifier.Classify(stats, Options()));
    }

    [Fact]
    public void Classify_AllSuccessHighLatency_ShouldBeSlow()
    {
        var stats = Stats(5, 5, avgMs: 700);

        Assert.Equal(DnsHealth.Slow, DnsClassifier.Classify(stats, Options()));
    }

    [Fact]
    public void Classify_HighLatencyBelowThreshold_ShouldBeHealthy()
    {
        var stats = Stats(5, 5, avgMs: 490);

        Assert.Equal(DnsHealth.Healthy, DnsClassifier.Classify(stats, Options(slowMs: 500)));
    }

    [Fact]
    public void Classify_SomeFailures_ShouldBeUnreliable()
    {
        var stats = Stats(5, 2, failures: 3, avgMs: 10);

        Assert.Equal(DnsHealth.Unreliable, DnsClassifier.Classify(stats, Options()));
    }

    [Fact]
    public void Classify_SporadicTimeoutBelowThreshold_ShouldStayHealthy()
    {
        var stats = Stats(5, 4, timeouts: 1, avgMs: 20);

        Assert.Equal(DnsHealth.Healthy, DnsClassifier.Classify(stats, Options()));
    }

    [Fact]
    public void Classify_AllTimeouts_ShouldBeUnreachable()
    {
        var stats = Stats(3, 0, timeouts: 3);

        Assert.Equal(DnsHealth.Unreachable, DnsClassifier.Classify(stats, Options()));
    }

    [Fact]
    public void Classify_RespondsButAlwaysFails_ShouldBeUnreliable()
    {
        var stats = Stats(3, 0, failures: 3, avgMs: 5);

        Assert.Equal(DnsHealth.Unreliable, DnsClassifier.Classify(stats, Options()));
    }

    [Fact]
    public void Classify_NoAttempts_ShouldBeUnreachable()
    {
        Assert.Equal(DnsHealth.Unreachable, DnsClassifier.Classify(Stats(0, 0), Options()));
    }

    [Fact]
    public void ClassifyOverall_NoResolvers_ShouldBeNoConfiguration()
    {
        Assert.Equal(DnsHealth.NoConfiguration, DnsClassifier.ClassifyOverall(Array.Empty<DnsHealth>()));
    }

    [Fact]
    public void ClassifyOverall_AllUnreachable_ShouldBeUnreachable()
    {
        var healths = new[] { DnsHealth.Unreachable, DnsHealth.Unreachable };

        Assert.Equal(DnsHealth.Unreachable, DnsClassifier.ClassifyOverall(healths));
    }

    [Fact]
    public void ClassifyOverall_AnyUnreliable_ShouldBeUnreliable()
    {
        var healths = new[] { DnsHealth.Healthy, DnsHealth.Unreliable };

        Assert.Equal(DnsHealth.Unreliable, DnsClassifier.ClassifyOverall(healths));
    }

    [Fact]
    public void ClassifyOverall_PartiallyUnreachable_ShouldBeUnreliable()
    {
        var healths = new[] { DnsHealth.Healthy, DnsHealth.Unreachable };

        Assert.Equal(DnsHealth.Unreliable, DnsClassifier.ClassifyOverall(healths));
    }

    [Fact]
    public void ClassifyOverall_AnySlow_ShouldBeSlow()
    {
        var healths = new[] { DnsHealth.Healthy, DnsHealth.Slow };

        Assert.Equal(DnsHealth.Slow, DnsClassifier.ClassifyOverall(healths));
    }

    [Fact]
    public void ClassifyOverall_AllHealthy_ShouldBeHealthy()
    {
        var healths = new[] { DnsHealth.Healthy, DnsHealth.Healthy };

        Assert.Equal(DnsHealth.Healthy, DnsClassifier.ClassifyOverall(healths));
    }
}