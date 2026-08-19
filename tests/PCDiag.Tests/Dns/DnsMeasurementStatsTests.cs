using PCDiag.Dns;

namespace PCDiag.Tests.Dns;

public class DnsMeasurementStatsTests
{
    [Fact]
    public void Compute_AllSuccess_ShouldReportNoFailures()
    {
        var probes = new[]
        {
            DnsProbes.Success(10),
            DnsProbes.Success(20),
            DnsProbes.Success(30)
        };

        var stats = DnsMeasurementStats.Compute(probes);

        Assert.Equal(3, stats.Attempts);
        Assert.Equal(3, stats.Successes);
        Assert.Equal(0, stats.Failures);
        Assert.Equal(0, stats.Timeouts);
        Assert.Equal(1.0, stats.SuccessRate);
        Assert.Equal(0.0, stats.FailureRate);
    }

    [Fact]
    public void Compute_ShouldCalculateAvgMinMax()
    {
        var probes = new[]
        {
            DnsProbes.Success(10),
            DnsProbes.Success(30),
            DnsProbes.Success(20)
        };

        var stats = DnsMeasurementStats.Compute(probes);

        Assert.Equal(20.0, stats.AvgLatencyMs);
        Assert.Equal(10, stats.MinLatencyMs);
        Assert.Equal(30, stats.MaxLatencyMs);
    }

    [Fact]
    public void Compute_MixedOutcomes_ShouldCountEach()
    {
        var probes = new[]
        {
            DnsProbes.Success(10),
            DnsProbes.Success(10),
            DnsProbes.Failure(10),
            DnsProbes.Timeout(),
            DnsProbes.Timeout()
        };

        var stats = DnsMeasurementStats.Compute(probes);

        Assert.Equal(5, stats.Attempts);
        Assert.Equal(2, stats.Successes);
        Assert.Equal(1, stats.Failures);
        Assert.Equal(2, stats.Timeouts);
        Assert.Equal(2.0 / 5.0, stats.SuccessRate);
        Assert.Equal(3.0 / 5.0, stats.FailureRate);
    }

    [Fact]
    public void Compute_AllTimeouts_ShouldHaveNullLatency()
    {
        var probes = new[]
        {
            DnsProbes.Timeout(),
            DnsProbes.Timeout()
        };

        var stats = DnsMeasurementStats.Compute(probes);

        Assert.Equal(2, stats.Timeouts);
        Assert.Equal(0, stats.Successes);
        Assert.Equal(0, stats.Failures);
        Assert.Null(stats.AvgLatencyMs);
        Assert.Null(stats.MinLatencyMs);
        Assert.Null(stats.MaxLatencyMs);
    }

    [Fact]
    public void Compute_EmptyProbes_ShouldNotThrow()
    {
        var stats = DnsMeasurementStats.Compute(Array.Empty<DnsProbeResult>());

        Assert.Equal(0, stats.Attempts);
        Assert.Equal(0, stats.SuccessRate);
        Assert.Equal(0, stats.FailureRate);
        Assert.Null(stats.AvgLatencyMs);
    }

    [Fact]
    public void Compute_ShouldIncludeFailedResponsesInLatency()
    {
        var probes = new[]
        {
            DnsProbes.Success(10),
            DnsProbes.Failure(50)
        };

        var stats = DnsMeasurementStats.Compute(probes);

        Assert.Equal(30.0, stats.AvgLatencyMs);
        Assert.Equal(10, stats.MinLatencyMs);
        Assert.Equal(50, stats.MaxLatencyMs);
    }
}