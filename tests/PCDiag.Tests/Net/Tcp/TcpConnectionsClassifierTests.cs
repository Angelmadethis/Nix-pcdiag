using PCDiag.Net.Tcp;

namespace PCDiag.Tests.Net.Tcp;

public class TcpConnectionsClassifierTests
{
    private static readonly TcpOptions Options = TcpOptions.Default;

    private static TcpStateSummary Summary(int timeWait = 0, int closeWait = 0, int established = 0, int perPidCloseWait = 0)
        => new()
        {
            TimeWait = timeWait,
            CloseWait = closeWait,
            Established = established,
            CloseWaitByProcess = perPidCloseWait > 0
                ? new[] { (ProcessId: 1234, Count: perPidCloseWait) }
                : Array.Empty<(int, int)>()
        };

    [Fact]
    public void Classify_TinyCounts_ShouldBeHealthy()
    {
        var result = TcpConnectionsClassifier.Classify(Summary(timeWait: 5, closeWait: 1, established: 20), Options, 16384);

        Assert.Equal(TcpConnectionsHealth.Healthy, result.Health);
        Assert.Empty(result.Flags);
    }

    [Fact]
    public void Classify_HighTimeWaitButSmallPoolShare_ShouldNotFlag()
    {
        var result = TcpConnectionsClassifier.Classify(Summary(timeWait: 200), Options, 16384);

        Assert.Equal(TcpConnectionsHealth.Healthy, result.Health);
        Assert.DoesNotContain(TcpConnectionsFlag.TimeWaitHigh, result.Flags);
        Assert.DoesNotContain(TcpConnectionsFlag.TimeWaitElevated, result.Flags);
    }

    [Fact]
    public void Classify_TimeWaitQuarterOfPool_ShouldBeElevated()
    {
        var result = TcpConnectionsClassifier.Classify(Summary(timeWait: 4096), Options, 16384);

        Assert.Equal(TcpConnectionsHealth.Elevated, result.Health);
        Assert.Contains(TcpConnectionsFlag.TimeWaitElevated, result.Flags);
        Assert.DoesNotContain(TcpConnectionsFlag.TimeWaitHigh, result.Flags);
    }

    [Fact]
    public void Classify_TimeWaitSixtyPercentOfPool_ShouldWarn()
    {
        var result = TcpConnectionsClassifier.Classify(Summary(timeWait: 10000), Options, 16384);

        Assert.Equal(TcpConnectionsHealth.Warning, result.Health);
        Assert.Contains(TcpConnectionsFlag.TimeWaitHigh, result.Flags);
    }

    [Fact]
    public void Classify_SmallPoolFromOs_ShouldUseItInsteadOfFallback()
    {
        var result = TcpConnectionsClassifier.Classify(Summary(timeWait: 200), Options, 250);

        Assert.Equal(TcpConnectionsHealth.Warning, result.Health);
        Assert.Contains(TcpConnectionsFlag.TimeWaitHigh, result.Flags);
    }

    [Fact]
    public void Classify_CloseWaitAboveSuspicious_ShouldBeElevated()
    {
        var result = TcpConnectionsClassifier.Classify(Summary(closeWait: 11), Options, 16384);

        Assert.Equal(TcpConnectionsHealth.Elevated, result.Health);
        Assert.Contains(TcpConnectionsFlag.CloseWaitCluster, result.Flags);
    }

    [Fact]
    public void Classify_CloseWaitAboveWarning_ShouldWarn()
    {
        var result = TcpConnectionsClassifier.Classify(Summary(closeWait: 51), Options, 16384);

        Assert.Equal(TcpConnectionsHealth.Warning, result.Health);
        Assert.Contains(TcpConnectionsFlag.CloseWaitCluster, result.Flags);
    }

    [Fact]
    public void Classify_CloseWaitSmallButConcentrated_ShouldFlagSingleProcess()
    {
        var result = TcpConnectionsClassifier.Classify(Summary(closeWait: 5, perPidCloseWait: 26), Options, 16384);

        Assert.Equal(TcpConnectionsHealth.Elevated, result.Health);
        Assert.Contains(TcpConnectionsFlag.CloseWaitSingleProcess, result.Flags);
    }

    [Fact]
    public void Classify_EstablishedAboveSuspicious_ShouldBeElevated()
    {
        var result = TcpConnectionsClassifier.Classify(Summary(established: 1001), Options, 16384);

        Assert.Equal(TcpConnectionsHealth.Elevated, result.Health);
        Assert.Contains(TcpConnectionsFlag.EstablishedElevated, result.Flags);
    }

    [Fact]
    public void Classify_EstablishedAboveWarning_ShouldWarn()
    {
        var result = TcpConnectionsClassifier.Classify(Summary(established: 5001), Options, 16384);

        Assert.Equal(TcpConnectionsHealth.Warning, result.Health);
        Assert.Contains(TcpConnectionsFlag.EstablishedHigh, result.Flags);
    }

    [Fact]
    public void Classify_MultipleConcerns_ShouldPickWorst()
    {
        var result = TcpConnectionsClassifier.Classify(Summary(timeWait: 10000, closeWait: 51), Options, 16384);

        Assert.Equal(TcpConnectionsHealth.Warning, result.Health);
        Assert.Contains(TcpConnectionsFlag.TimeWaitHigh, result.Flags);
        Assert.Contains(TcpConnectionsFlag.CloseWaitCluster, result.Flags);
    }
}