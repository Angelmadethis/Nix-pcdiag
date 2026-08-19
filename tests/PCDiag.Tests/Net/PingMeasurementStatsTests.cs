using System.Net.NetworkInformation;
using PCDiag.Net;

namespace PCDiag.Tests.Net;

public class PingMeasurementStatsTests
{
    [Fact]
    public void Compute_AllSuccess_ShouldHaveZeroLossAndLatencyBounds()
    {
        var stats = PingMeasurementStats.Compute(new[]
        {
            PingResults.Success(20), PingResults.Success(30), PingResults.Success(40)
        });

        Assert.Equal(3, stats.Attempts);
        Assert.Equal(3, stats.Successes);
        Assert.Equal(0, stats.Failures);
        Assert.Equal(0, stats.Timeouts);
        Assert.Equal(0, stats.LossRate);
        Assert.Equal(1.0, stats.SuccessRate);
        Assert.Equal(30, stats.AvgLatencyMs);
        Assert.Equal(20, stats.MinLatencyMs);
        Assert.Equal(40, stats.MaxLatencyMs);
    }

    [Fact]
    public void Compute_MixedOutcomes_ShouldCountEachKindAndLoss()
    {
        var stats = PingMeasurementStats.Compute(new[]
        {
            PingResults.Success(10), PingResults.Success(20),
            PingResults.Timeout(), PingResults.Unreachable()
        });

        Assert.Equal(4, stats.Attempts);
        Assert.Equal(2, stats.Successes);
        Assert.Equal(1, stats.Failures);
        Assert.Equal(1, stats.Timeouts);
        Assert.Equal(0.5, stats.LossRate);
        Assert.Equal(0.5, stats.SuccessRate);
        Assert.Equal(15, stats.AvgLatencyMs);
    }

    [Fact]
    public void Compute_AllTimeouts_ShouldHaveNullLatency()
    {
        var stats = PingMeasurementStats.Compute(new[]
        {
            PingResults.Timeout(), PingResults.Timeout()
        });

        Assert.Equal(2, stats.Attempts);
        Assert.Equal(0, stats.Successes);
        Assert.Equal(2, stats.Timeouts);
        Assert.Equal(1.0, stats.LossRate);
        Assert.Null(stats.AvgLatencyMs);
        Assert.Null(stats.MinLatencyMs);
        Assert.Null(stats.MaxLatencyMs);
    }

    [Fact]
    public void Compute_Empty_ShouldBeZeros()
    {
        var stats = PingMeasurementStats.Compute(Array.Empty<PingProbeResult>());

        Assert.Equal(0, stats.Attempts);
        Assert.Equal(0, stats.Successes);
        Assert.Equal(0, stats.LossRate);
        Assert.Null(stats.AvgLatencyMs);
    }
}

public class SystemPingProbeMapTests
{
    [Fact]
    public void Map_Success_ShouldMapToSuccess()
    {
        var result = SystemPingProbe.Map(IPStatus.Success, 12);

        Assert.Equal(PingProbeOutcome.Success, result.Outcome);
        Assert.Equal(12, result.RoundTripMs);
    }

    [Fact]
    public void Map_PacketTooBig_ShouldMapToFragmentationNeeded()
    {
        var result = SystemPingProbe.Map(IPStatus.PacketTooBig, 5);

        Assert.Equal(PingProbeOutcome.FragmentationNeeded, result.Outcome);
        Assert.Equal(5, result.RoundTripMs);
    }

    [Fact]
    public void Map_TimedOut_ShouldMapToTimeoutWithZeroRtt()
    {
        var result = SystemPingProbe.Map(IPStatus.TimedOut, 100);

        Assert.Equal(PingProbeOutcome.TimedOut, result.Outcome);
        Assert.Equal(0, result.RoundTripMs);
    }

    [Fact]
    public void Map_HostUnreachable_ShouldMapToUnreachable()
    {
        var result = SystemPingProbe.Map(IPStatus.DestinationHostUnreachable, 5);

        Assert.Equal(PingProbeOutcome.Unreachable, result.Outcome);
    }

    [Fact]
    public void Map_UnknownStatus_ShouldMapToFailed()
    {
        var result = SystemPingProbe.Map(IPStatus.Unknown, 5);

        Assert.Equal(PingProbeOutcome.Failed, result.Outcome);
    }
}