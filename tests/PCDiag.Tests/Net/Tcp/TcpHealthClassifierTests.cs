using PCDiag.Net.Tcp;

namespace PCDiag.Tests.Net.Tcp;

public class TcpHealthClassifierTests
{
    private static readonly TcpOptions Options = TcpOptions.Default;

    private static TcpCumulativeStats Stats(
        long failures = 0, long initiated = 100,
        long retransmitted = 0, long sent = 1000, long received = 900,
        long resetsSent = 0, long resetsReceived = 0)
        => new()
        {
            ConnectionFailures = failures,
            ConnectionsInitiated = initiated,
            ResetsSent = resetsSent,
            ResetsReceived = resetsReceived,
            SegmentsRetransmitted = retransmitted,
            SegmentsSent = sent,
            SegmentsReceived = received
        };

    private static TcpConfiguration Config(
        TcpAutotuningLevel autotuning = TcpAutotuningLevel.Normal,
        TcpAutotuningLevel groupPolicy = TcpAutotuningLevel.Unknown,
        int? timedWaitDelay = null,
        int? maxUserPort = null,
        int? windowSize = null,
        int? globalWindowSize = null)
        => new()
        {
            AutotuningLevel = autotuning,
            AutotuningGroupPolicy = groupPolicy,
            TcpTimedWaitDelay = timedWaitDelay,
            MaxUserPort = maxUserPort,
            TcpWindowSize = windowSize,
            GlobalMaxTcpWindowSize = globalWindowSize,
            DynamicPortStart = 49152,
            DynamicPortCount = 16384
        };

    [Fact]
    public void Classify_HealthySignals_ShouldBeHealthy()
    {
        var result = TcpHealthClassifier.Classify(Stats(), Config(), 0.001, Options);

        Assert.Equal(TcpHealthVerdict.Healthy, result.Verdict);
        Assert.DoesNotContain(TcpHealthFlag.UptimeUnknown, result.Flags);
    }

    [Theory]
    [InlineData(0.009, TcpHealthVerdict.Healthy)]
    [InlineData(0.01, TcpHealthVerdict.Suspicious)]
    [InlineData(0.049, TcpHealthVerdict.Suspicious)]
    [InlineData(0.05, TcpHealthVerdict.Warning)]
    public void Classify_RetransmissionRatioBands_ShouldClassify(double ratio, TcpHealthVerdict expected)
    {
        var result = TcpHealthClassifier.Classify(Stats(retransmitted: (long)(ratio * 1000), sent: 1000, received: 0), Config(), null, Options);

        Assert.Equal(expected, result.Verdict);
    }

    [Fact]
    public void Classify_NoSegments_ShouldFlagUnavailable()
    {
        var result = TcpHealthClassifier.Classify(new TcpCumulativeStats(), Config(), 0.001, Options);

        Assert.Contains(TcpHealthFlag.RetransmissionUnavailable, result.Flags);
        Assert.Equal(TcpHealthVerdict.Healthy, result.Verdict);
    }

    [Theory]
    [InlineData(0.09, TcpHealthVerdict.Healthy)]
    [InlineData(0.10, TcpHealthVerdict.Suspicious)]
    [InlineData(0.29, TcpHealthVerdict.Suspicious)]
    [InlineData(0.30, TcpHealthVerdict.Warning)]
    public void Classify_FailureRatioBands_ShouldClassify(double ratio, TcpHealthVerdict expected)
    {
        var result = TcpHealthClassifier.Classify(Stats(failures: (long)(ratio * 100), initiated: 100), Config(), 0.001, Options);

        Assert.Equal(expected, result.Verdict);
    }

    [Fact]
    public void Classify_NoInitiations_ShouldFlagFailureRatioUnavailable()
    {
        var result = TcpHealthClassifier.Classify(Stats(failures: 0, initiated: 0), Config(), 0.001, Options);

        Assert.Contains(TcpHealthFlag.FailureRatioUnavailable, result.Flags);
    }

    [Theory]
    [InlineData(TcpAutotuningLevel.Normal, TcpHealthVerdict.Healthy)]
    [InlineData(TcpAutotuningLevel.Disabled, TcpHealthVerdict.Suspicious)]
    [InlineData(TcpAutotuningLevel.Restricted, TcpHealthVerdict.Suspicious)]
    [InlineData(TcpAutotuningLevel.Experimental, TcpHealthVerdict.Suspicious)]
    [InlineData(TcpAutotuningLevel.HighlyRestricted, TcpHealthVerdict.Suspicious)]
    public void Classify_AutotuningLevel_ShouldFlagNonDefault(TcpAutotuningLevel level, TcpHealthVerdict expected)
    {
        var result = TcpHealthClassifier.Classify(Stats(), Config(autotuning: level), 0.001, Options);

        Assert.Equal(expected, result.Verdict);
        if (level == TcpAutotuningLevel.Disabled)
            Assert.Contains(TcpHealthFlag.AutotuningDisabled, result.Flags);
        else if (level != TcpAutotuningLevel.Normal)
            Assert.Contains(TcpHealthFlag.AutotuningRestricted, result.Flags);
    }

    [Fact]
    public void Classify_GroupPolicyOverride_ShouldFlagIt()
    {
        var result = TcpHealthClassifier.Classify(Stats(), Config(autotuning: TcpAutotuningLevel.Normal, groupPolicy: TcpAutotuningLevel.Disabled), 0.001, Options);

        Assert.Contains(TcpHealthFlag.AutotuningGroupPolicy, result.Flags);
    }

    [Fact]
    public void Classify_TimedWaitDelayLow_ShouldBeSuspicious()
    {
        var result = TcpHealthClassifier.Classify(Stats(), Config(timedWaitDelay: 25), 0.001, Options);

        Assert.Equal(TcpHealthVerdict.Suspicious, result.Verdict);
        Assert.Contains(TcpHealthFlag.TcpTimedWaitDelayLow, result.Flags);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(null)]
    public void Classify_TimedWaitDelayAtOrDefault_ShouldNotFlag(int? value)
    {
        var result = TcpHealthClassifier.Classify(Stats(), Config(timedWaitDelay: value), 0.001, Options);

        Assert.DoesNotContain(TcpHealthFlag.TcpTimedWaitDelayLow, result.Flags);
    }

    [Fact]
    public void Classify_MaxUserPortLow_ShouldWarn()
    {
        var result = TcpHealthClassifier.Classify(Stats(), Config(maxUserPort: 4000), 0.001, Options);

        Assert.Equal(TcpHealthVerdict.Warning, result.Verdict);
        Assert.Contains(TcpHealthFlag.MaxUserPortLow, result.Flags);
    }

    [Theory]
    [InlineData(5000)]
    [InlineData(65534)]
    [InlineData(null)]
    public void Classify_MaxUserPortAtOrDefault_ShouldNotFlag(int? value)
    {
        var result = TcpHealthClassifier.Classify(Stats(), Config(maxUserPort: value), 0.001, Options);

        Assert.DoesNotContain(TcpHealthFlag.MaxUserPortLow, result.Flags);
    }

    [Theory]
    [InlineData(1, null)]
    [InlineData(null, 1)]
    public void Classify_WindowSizeValuesSet_ShouldFlagOverride(int? windowSize, int? globalWindowSize)
    {
        var result = TcpHealthClassifier.Classify(Stats(), Config(windowSize: windowSize, globalWindowSize: globalWindowSize), 0.001, Options);

        Assert.Equal(TcpHealthVerdict.Suspicious, result.Verdict);
        Assert.Contains(TcpHealthFlag.WindowSizeOverridesAutotuning, result.Flags);
    }

    [Theory]
    [InlineData(0.009, TcpHealthVerdict.Healthy)]
    [InlineData(0.01, TcpHealthVerdict.Suspicious)]
    [InlineData(0.099, TcpHealthVerdict.Suspicious)]
    [InlineData(0.10, TcpHealthVerdict.Warning)]
    public void Classify_AdapterErrorRateBands_ShouldClassify(double rate, TcpHealthVerdict expected)
    {
        var result = TcpHealthClassifier.Classify(Stats(), Config(), rate, Options);

        Assert.Equal(expected, result.Verdict);
    }

    [Fact]
    public void Classify_NoUptime_ShouldFlagUptimeUnknown()
    {
        var result = TcpHealthClassifier.Classify(Stats(), Config(), null, Options);

        Assert.Contains(TcpHealthFlag.UptimeUnknown, result.Flags);
        Assert.Equal(TcpHealthVerdict.Healthy, result.Verdict);
    }

    [Fact]
    public void Classify_MultipleConcerns_ShouldPickWorst()
    {
        var result = TcpHealthClassifier.Classify(
            Stats(retransmitted: 100, sent: 1000),
            Config(maxUserPort: 4000),
            0.001,
            Options);

        Assert.Equal(TcpHealthVerdict.Warning, result.Verdict);
        Assert.Contains(TcpHealthFlag.RetransmissionHigh, result.Flags);
        Assert.Contains(TcpHealthFlag.MaxUserPortLow, result.Flags);
    }
}