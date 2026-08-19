using PCDiag.Net;

namespace PCDiag.Tests.Net;

public class GatewayClassifierTests
{
    private static NetOptions Options(double slowMs = 100, double suspicious = 0.05, double warning = 0.20)
        => NetOptions.Default with
        {
            GatewaySlowLatencyMs = slowMs,
            LossSuspiciousRate = suspicious,
            LossWarningRate = warning
        };

    private static PingMeasurementStats Stats(int attempts, int successes, int timeouts = 0, int failures = 0, double? avgMs = 5)
        => new()
        {
            Attempts = attempts,
            Successes = successes,
            Timeouts = timeouts,
            Failures = failures,
            LossRate = attempts > 0 ? (double)(failures + timeouts) / attempts : 0,
            SuccessRate = attempts > 0 ? (double)successes / attempts : 0,
            AvgLatencyMs = avgMs,
            MinLatencyMs = avgMs is double m ? (long)m : null,
            MaxLatencyMs = avgMs is double x ? (long)x : null
        };

    [Fact]
    public void Classify_AllSuccessLowLatency_ShouldBeHealthy()
    {
        Assert.Equal(GatewayHealth.Healthy, GatewayClassifier.Classify(Stats(4, 4), Options()));
    }

    [Fact]
    public void Classify_NoReplies_ShouldBeUnreachable()
    {
        Assert.Equal(GatewayHealth.Unreachable, GatewayClassifier.Classify(Stats(4, 0, timeouts: 4), Options()));
    }

    [Fact]
    public void Classify_NoAttempts_ShouldBeUnreachable()
    {
        Assert.Equal(GatewayHealth.Unreachable, GatewayClassifier.Classify(Stats(0, 0), Options()));
    }

    [Fact]
    public void Classify_AllSuccessHighLatency_ShouldBeSlow()
    {
        Assert.Equal(GatewayHealth.Slow, GatewayClassifier.Classify(Stats(4, 4, avgMs: 150), Options(slowMs: 100)));
    }

    [Fact]
    public void Classify_HighLatencyBelowThreshold_ShouldStayHealthy()
    {
        Assert.Equal(GatewayHealth.Healthy, GatewayClassifier.Classify(Stats(4, 4, avgMs: 90), Options(slowMs: 100)));
    }

    [Fact]
    public void Classify_HeavyLoss_ShouldBeLossy()
    {
        Assert.Equal(GatewayHealth.Lossy, GatewayClassifier.Classify(Stats(4, 2, timeouts: 2), Options()));
    }

    [Fact]
    public void Classify_ModerateLoss_ShouldBeLossy()
    {
        Assert.Equal(GatewayHealth.Lossy, GatewayClassifier.Classify(Stats(20, 18, timeouts: 2), Options()));
    }

    [Fact]
    public void Classify_SparseLossBelowSuspicious_ShouldStayHealthy()
    {
        Assert.Equal(GatewayHealth.Healthy, GatewayClassifier.Classify(Stats(25, 24, timeouts: 1), Options()));
    }
}

public class PacketLossClassifierTests
{
    private static NetOptions Options(double gatewaySlow = 100, double internetSlow = 300)
        => NetOptions.Default with { GatewaySlowLatencyMs = gatewaySlow, InternetSlowLatencyMs = internetSlow };

    private static PingMeasurementStats Stats(int attempts, int successes, int timeouts = 0, int failures = 0, double? avgMs = 20)
        => new()
        {
            Attempts = attempts,
            Successes = successes,
            Timeouts = timeouts,
            Failures = failures,
            LossRate = attempts > 0 ? (double)(failures + timeouts) / attempts : 0,
            SuccessRate = attempts > 0 ? (double)successes / attempts : 0,
            AvgLatencyMs = avgMs,
            MinLatencyMs = avgMs is double m ? (long)m : null,
            MaxLatencyMs = avgMs is double x ? (long)x : null
        };

    [Fact]
    public void ClassifyTarget_AllSuccess_ShouldBeHealthy()
    {
        Assert.Equal(PacketLossHealth.Healthy, PacketLossClassifier.ClassifyTarget(Stats(5, 5), Options(), 300));
    }

    [Fact]
    public void ClassifyTarget_AllTimeouts_ShouldBeUnreachable()
    {
        Assert.Equal(PacketLossHealth.Unreachable, PacketLossClassifier.ClassifyTarget(Stats(5, 0, timeouts: 5), Options(), 300));
    }

    [Fact]
    public void ClassifyTarget_HeavyLoss_ShouldBeLossy()
    {
        Assert.Equal(PacketLossHealth.Lossy, PacketLossClassifier.ClassifyTarget(Stats(5, 3, timeouts: 2), Options(), 300));
    }

    [Fact]
    public void ClassifyTarget_ModerateLoss_ShouldBeElevated()
    {
        Assert.Equal(PacketLossHealth.Elevated, PacketLossClassifier.ClassifyTarget(Stats(20, 18, timeouts: 2), Options(), 300));
    }

    [Fact]
    public void ClassifyTarget_HighLatency_ShouldBeSlow()
    {
        Assert.Equal(PacketLossHealth.Slow, PacketLossClassifier.ClassifyTarget(Stats(5, 5, avgMs: 350), Options(), 300));
    }

    [Fact]
    public void ClassifyTarget_GatewaySlowThreshold_ShouldApply()
    {
        Assert.Equal(PacketLossHealth.Slow, PacketLossClassifier.ClassifyTarget(Stats(4, 4, avgMs: 120), Options(), 100));
        Assert.Equal(PacketLossHealth.Healthy, PacketLossClassifier.ClassifyTarget(Stats(4, 4, avgMs: 120), Options(), 300));
    }

    [Fact]
    public void ClassifyOverall_GatewayUnreachable_ShouldBeUnreachable()
    {
        Assert.Equal(PacketLossHealth.Unreachable, PacketLossClassifier.ClassifyOverall(
            PacketLossHealth.Unreachable, new[] { PacketLossHealth.Healthy }));
    }

    [Fact]
    public void ClassifyOverall_AllInternetUnreachable_ShouldBeInternetUnreachable()
    {
        Assert.Equal(PacketLossHealth.InternetUnreachable, PacketLossClassifier.ClassifyOverall(
            PacketLossHealth.Healthy, new[] { PacketLossHealth.Unreachable, PacketLossHealth.Unreachable }));
    }

    [Fact]
    public void ClassifyOverall_AnyLossy_ShouldBeLossy()
    {
        Assert.Equal(PacketLossHealth.Lossy, PacketLossClassifier.ClassifyOverall(
            PacketLossHealth.Healthy, new[] { PacketLossHealth.Lossy }));
        Assert.Equal(PacketLossHealth.Lossy, PacketLossClassifier.ClassifyOverall(
            PacketLossHealth.Lossy, new[] { PacketLossHealth.Healthy }));
    }

    [Fact]
    public void ClassifyOverall_AnyElevated_ShouldBeElevated()
    {
        Assert.Equal(PacketLossHealth.Elevated, PacketLossClassifier.ClassifyOverall(
            PacketLossHealth.Healthy, new[] { PacketLossHealth.Elevated }));
    }

    [Fact]
    public void ClassifyOverall_AnySlow_ShouldBeSlow()
    {
        Assert.Equal(PacketLossHealth.Slow, PacketLossClassifier.ClassifyOverall(
            PacketLossHealth.Healthy, new[] { PacketLossHealth.Slow }));
    }

    [Fact]
    public void ClassifyOverall_AllHealthy_ShouldBeHealthy()
    {
        Assert.Equal(PacketLossHealth.Healthy, PacketLossClassifier.ClassifyOverall(
            PacketLossHealth.Healthy, new[] { PacketLossHealth.Healthy, PacketLossHealth.Healthy }));
    }
}

public class MtuClassifierTests
{
    private static PathMtuResult Path(int maxPayload, int limit, bool confirmed)
        => new()
        {
            MaxPayloadSucceeded = maxPayload,
            PayloadLimitTested = limit,
            BoundaryConfirmed = confirmed
        };

    [Fact]
    public void Classify_PathEqualsInterface_ShouldBeHealthy()
    {
        var path = Path(1472, 1472, confirmed: true);
        Assert.Equal(MtuVerdict.Healthy, MtuClassifier.Classify(1500, path, null));
    }

    [Fact]
    public void Classify_PathAboveInterface_ShouldBeHealthy()
    {
        var path = Path(9000 - MtuOptions.IcmpIpv4Overhead, 9000 - MtuOptions.IcmpIpv4Overhead, confirmed: true);
        Assert.Equal(MtuVerdict.Healthy, MtuClassifier.Classify(9000, path, null));
    }

    [Fact]
    public void Classify_PathBelowInterfaceConfirmed_ShouldBeConfirmedMismatch()
    {
        var path = Path(1464, 1472, confirmed: true);
        Assert.Equal(MtuVerdict.ConfirmedMismatch, MtuClassifier.Classify(1500, path, null));
    }

    [Fact]
    public void Classify_PathBelowInterfaceUnconfirmed_ShouldBePotentialIssue()
    {
        var path = Path(1464, 1472, confirmed: false);
        Assert.Equal(MtuVerdict.PotentialIssue, MtuClassifier.Classify(1500, path, null));
    }

    [Fact]
    public void Classify_InterfaceUnknown_ShouldBeInterfaceMtuUnknown()
    {
        var path = Path(1472, 1472, confirmed: true);
        Assert.Equal(MtuVerdict.InterfaceMtuUnknown, MtuClassifier.Classify(null, path, null));
    }

    [Fact]
    public void Classify_NoMeasurement_ShouldBeUnmeasurable()
    {
        var path = Path(0, 1472, confirmed: false);
        Assert.Equal(MtuVerdict.Unmeasurable, MtuClassifier.Classify(1500, path, null));
        Assert.Equal(MtuVerdict.Unmeasurable, MtuClassifier.Classify(1500, null, null));
    }

    [Fact]
    public void Classify_PrefersInternetPathOverGateway()
    {
        var gateway = Path(1472, 1472, confirmed: true);
        var internet = Path(1464, 1472, confirmed: true);

        Assert.Equal(MtuVerdict.ConfirmedMismatch, MtuClassifier.Classify(1500, gateway, internet));
    }

    [Fact]
    public void Classify_FallsBackToGatewayWhenInternetUnmeasurable()
    {
        var gateway = Path(1472, 1472, confirmed: true);
        var internet = Path(0, 1472, confirmed: false);

        Assert.Equal(MtuVerdict.Healthy, MtuClassifier.Classify(1500, gateway, internet));
    }

    [Fact]
    public void Classify_JumboInterface_UsesGatewayPathNotCappedInternet()
    {
        // Internet path is capped at 1500 bytes for a 9000-byte interface; only the
        // gateway path spans the full range, so it must drive the verdict.
        var gateway = Path(8972, 8972, confirmed: true);
        var internet = Path(1472, 1472, confirmed: true);

        Assert.Equal(MtuVerdict.Healthy, MtuClassifier.Classify(9000, gateway, internet));
    }

    [Fact]
    public void Classify_JumboInterface_InternetAtCap_IsNotMismatch()
    {
        // Gateway path can't be measured (dead), internet path hits its cap: the
        // comparison beyond the cap is inconclusive, so no mismatch is confirmed.
        var gateway = Path(0, 8972, confirmed: false);
        var internet = Path(1472, 1472, confirmed: true);

        Assert.Equal(MtuVerdict.Healthy, MtuClassifier.Classify(9000, gateway, internet));
    }
}

public class InterfaceMtuLookupTests
{
    [Fact]
    public void FindMtu_ShouldReturnMtuOfMatchingRow()
    {
        var rows = new (string[]? Ips, int? Mtu)[]
        {
            (new[] { "10.0.0.5" }, 9000),
            (new[] { "192.168.1.50" }, 1500)
        };

        Assert.Equal(1500, WmiInterfaceMtuSource.FindMtu(rows, new[] { "192.168.1.50" }));
    }

    [Fact]
    public void FindMtu_NoMatch_ShouldReturnNull()
    {
        var rows = new (string[]? Ips, int? Mtu)[] { (new[] { "10.0.0.5" }, 1500) };

        Assert.Null(WmiInterfaceMtuSource.FindMtu(rows, new[] { "192.168.1.50" }));
    }

    [Fact]
    public void FindMtu_ZeroOrNullMtu_ShouldSkip()
    {
        var rows = new (string[]? Ips, int? Mtu)[]
        {
            (new[] { "192.168.1.50" }, 0),
            (new[] { "192.168.1.50" }, null)
        };

        Assert.Null(WmiInterfaceMtuSource.FindMtu(rows, new[] { "192.168.1.50" }));
    }

    [Fact]
    public void IpsMatch_ShouldBeCaseInsensitiveIntersection()
    {
        Assert.True(WmiInterfaceMtuSource.IpsMatch(new[] { "192.168.1.50" }, new[] { "192.168.1.50" }));
        Assert.True(WmiInterfaceMtuSource.IpsMatch(new[] { "FE80::1", "192.168.1.50" }, new[] { "192.168.1.50" }));
        Assert.False(WmiInterfaceMtuSource.IpsMatch(new[] { "10.0.0.5" }, new[] { "192.168.1.50" }));
        Assert.False(WmiInterfaceMtuSource.IpsMatch(null, new[] { "192.168.1.50" }));
        Assert.False(WmiInterfaceMtuSource.IpsMatch(new[] { "192.168.1.50" }, Array.Empty<string>()));
    }

    [Fact]
    public void FindMtuByInterfaceName_ShouldMatchAliasCaseInsensitively()
    {
        var rows = new (string? Alias, int? Mtu)[]
        {
            ("Ethernet", 1500),
            ("Wi-Fi", 9000)
        };

        Assert.Equal(9000, WmiInterfaceMtuSource.FindMtuByInterfaceName(rows, "wi-fi"));
        Assert.Equal(1500, WmiInterfaceMtuSource.FindMtuByInterfaceName(rows, "Ethernet"));
    }

    [Fact]
    public void FindMtuByInterfaceName_NoMatch_ShouldReturnNull()
    {
        var rows = new (string? Alias, int? Mtu)[] { ("Ethernet", 1500) };

        Assert.Null(WmiInterfaceMtuSource.FindMtuByInterfaceName(rows, "Wi-Fi"));
        Assert.Null(WmiInterfaceMtuSource.FindMtuByInterfaceName(rows, ""));
    }

    [Fact]
    public void FindMtuByInterfaceName_ZeroOrNullMtu_ShouldSkip()
    {
        var rows = new (string? Alias, int? Mtu)[]
        {
            ("Wi-Fi", 0),
            ("Wi-Fi", null)
        };

        Assert.Null(WmiInterfaceMtuSource.FindMtuByInterfaceName(rows, "Wi-Fi"));
    }
}