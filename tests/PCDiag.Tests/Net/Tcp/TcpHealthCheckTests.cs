using PCDiag.Checks.Network;
using PCDiag.Core;
using PCDiag.Net.Tcp;

namespace PCDiag.Tests.Net.Tcp;

public class TcpHealthCheckTests
{
    private static TcpCumulativeStats HealthyStats(long sent = 1000, long received = 900)
        => new()
        {
            ConnectionFailures = 5,
            ConnectionsInitiated = 100,
            ResetsSent = 1,
            ResetsReceived = 2,
            SegmentsRetransmitted = 3,
            SegmentsSent = sent,
            SegmentsReceived = received
        };

    private static TcpConfiguration NormalConfig()
        => new()
        {
            AutotuningLevel = TcpAutotuningLevel.Normal,
            DynamicPortStart = 49152,
            DynamicPortCount = 16384
        };

    private static TcpHealthCheck Check(
        FakeTcpStatsSource? stats = null,
        FakeTcpConfigSource? config = null,
        FakeAdapterErrorSource? adapter = null)
        => new(statsSource: stats ?? new FakeTcpStatsSource { Stats = HealthyStats() },
               configSource: config ?? new FakeTcpConfigSource { Config = NormalConfig() },
               adapterErrorSource: adapter ?? new FakeAdapterErrorSource());

    [Fact]
    public async Task Healthy_Signals_ShouldPass()
    {
        var adapter = new FakeAdapterErrorSource { Result = new TcpAdapterErrorStats("Wi-Fi", 2, 1, 0, 0) };
        var context = new DiagnosticContext(mode: ScanMode.Standard, inventory: TcpInventory.WithActiveAdapter(TimeSpan.FromHours(48)));

        var result = await Check(adapter: adapter).ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Passed, result.Status);
        Assert.Equal(DiagnosticSeverity.Healthy, result.Severity);
        Assert.Empty(result.Recommendations);
        Assert.Contains("normal", result.Summary);
        Assert.Contains(result.Evidence, e => e.Description == "Receive Window Auto-Tuning" && e.Value == "Normal");
    }

    [Fact]
    public async Task HighRetransmission_ShouldWarn()
    {
        var stats = new FakeTcpStatsSource
        {
            Stats = new TcpCumulativeStats
            {
                ConnectionFailures = 5,
                ConnectionsInitiated = 100,
                SegmentsRetransmitted = 100,
                SegmentsSent = 1000,
                SegmentsReceived = 0
            }
        };

        var result = await Check(stats: stats).ExecuteAsync(HealthyContext(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Warning, result.Severity);
        Assert.Contains(result.Evidence, e => e.Description == "Segments" && e.Value.Contains("(10% retransmit rate)"));
        Assert.Contains(result.Recommendations, r => r.Text.Contains("retransmission"));
    }

    [Fact]
    public async Task AutotuningDisabled_ShouldBeSuspicious()
    {
        var config = new FakeTcpConfigSource
        {
            Config = NormalConfig() with { AutotuningLevel = TcpAutotuningLevel.Disabled }
        };

        var result = await Check(config: config).ExecuteAsync(HealthyContext(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Suspicious, result.Severity);
        Assert.Contains(result.Evidence, e => e.Description == "Receive Window Auto-Tuning" && e.Value == "Disabled");
        Assert.Contains(result.Recommendations, r => r.Text.Contains("autotuninglevel=normal"));
        Assert.Contains("does not change the setting", result.Recommendations[0].Text);
    }

    [Fact]
    public async Task RegistryTweaks_ShouldAppearInEvidence()
    {
        var config = new FakeTcpConfigSource
        {
            Config = NormalConfig() with { MaxUserPort = 4000, TcpTimedWaitDelay = 25 }
        };

        var result = await Check(config: config).ExecuteAsync(HealthyContext(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Contains(result.Evidence, e => e.Description == "TCP Config - MaxUserPort" && e.Value == "4000");
        Assert.Contains(result.Evidence, e => e.Description == "TCP Config - TcpTimedWaitDelay" && e.Value == "25 seconds");
    }

    [Fact]
    public async Task UnsetRegistryValues_ShouldReportWindowsDefault()
    {
        var result = await Check().ExecuteAsync(HealthyContext(), CancellationToken.None);

        Assert.Contains(result.Evidence, e => e.Description == "TCP Config - TcpTimedWaitDelay" && e.Value.Contains("Windows default"));
        Assert.Contains(result.Evidence, e => e.Description == "TCP Config - MaxUserPort" && e.Value.Contains("Windows default"));
    }

    [Fact]
    public async Task AdapterMatch_ShouldUseActiveAdaptersNameAndDescription()
    {
        var adapter = new FakeAdapterErrorSource { Result = new TcpAdapterErrorStats("Intel[R] Wi-Fi 6 AX201 160MHz", 0, 0, 0, 0) };
        var context = new DiagnosticContext(mode: ScanMode.Standard, inventory: TcpInventory.WithActiveAdapter(TimeSpan.FromHours(1)));

        await Check(adapter: adapter).ExecuteAsync(context, CancellationToken.None);

        Assert.Equal("Wi-Fi", adapter.CalledName);
        Assert.Equal("Intel(R) Wi-Fi 6 AX201 160MHz", adapter.CalledDescription);
    }

    [Fact]
    public async Task AdapterErrorsWithUptime_ShouldComputeRate()
    {
        var adapter = new FakeAdapterErrorSource { Result = new TcpAdapterErrorStats("Wi-Fi", 7200, 0, 0, 0) };
        var context = new DiagnosticContext(mode: ScanMode.Standard, inventory: TcpInventory.WithActiveAdapter(TimeSpan.FromHours(2)));

        var result = await Check(adapter: adapter).ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Warning, result.Severity);
        Assert.Contains(result.Evidence, e => e.Description == "Adapter Errors" && e.Value.Contains("1.000 errors/s"));
    }

    [Fact]
    public async Task NoUptime_ShouldStillPassButFlagUnavailable()
    {
        var adapter = new FakeAdapterErrorSource { Result = new TcpAdapterErrorStats("Wi-Fi", 5, 0, 0, 0) };
        var context = new DiagnosticContext(mode: ScanMode.Standard, inventory: TcpInventory.WithActiveAdapter(null));

        var result = await Check(adapter: adapter).ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Passed, result.Status);
        Assert.Contains(result.Evidence, e => e.Description == "Adapter Errors" && e.Value.Contains("since boot") && !e.Value.Contains("avg"));
    }

    [Fact]
    public async Task NoAdapterCounters_ShouldReportNotAvailable()
    {
        var result = await Check(adapter: new FakeAdapterErrorSource()).ExecuteAsync(HealthyContext(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Passed, result.Status);
        Assert.Contains(result.Evidence, e => e.Description == "Adapter Errors" && e.Value.Contains("Not available"));
    }

    private static DiagnosticContext HealthyContext()
        => new(mode: ScanMode.Standard, inventory: TcpInventory.WithActiveAdapter(TimeSpan.FromHours(48)));
}