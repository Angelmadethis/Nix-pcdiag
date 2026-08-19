using System.Net;
using PCDiag.Checks.Network;
using PCDiag.Core;
using PCDiag.Net;

namespace PCDiag.Tests.Net;

public class PacketLossCheckTests
{
    private static DiagnosticContext ContextWithGateway(string? gateway = "192.168.1.1")
        => new(mode: ScanMode.Standard, inventory: NetInventory.WithGateway(gateway));

    private static DiagnosticContext ContextNoConnection()
        => new(mode: ScanMode.Standard, inventory: NetInventory.WithNoActiveConnection());

    private static PacketLossCheck CheckWith(
        Func<IPAddress, int, bool, TimeSpan, CancellationToken, Task<PingProbeResult>> behavior,
        IReadOnlyList<string>? overrides = null)
        => new(probe: new FakePingProbe(behavior), targetOverrides: overrides);

    private static Func<IPAddress, int, bool, TimeSpan, CancellationToken, Task<PingProbeResult>> Sequence(params PingProbeResult[] results)
    {
        var index = 0;
        return (_, _, _, _, _) =>
        {
            var result = results[Math.Min(index, results.Length - 1)];
            index++;
            return Task.FromResult(result);
        };
    }

    [Fact]
    public async Task Healthy_ShouldPass()
    {
        var behavior = PathSimulator.GatewayThen(
            NetInventory.GatewayIp,
            PathSimulator.AlwaysSuccess(10),
            PathSimulator.AlwaysSuccess(20));
        var check = CheckWith(behavior);

        var result = await check.ExecuteAsync(ContextWithGateway(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Passed, result.Status);
        Assert.Equal(DiagnosticSeverity.Healthy, result.Severity);
        Assert.Empty(result.Recommendations);
    }

    [Fact]
    public async Task GatewayUnreachable_ShouldBeCritical()
    {
        var check = CheckWith(PathSimulator.Dead());
        var result = await check.ExecuteAsync(ContextWithGateway(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Critical, result.Severity);
        Assert.Contains("gateway is unreachable", result.Summary);
    }

    [Fact]
    public async Task InternetUnreachableWhileGatewayOk_ShouldWarn()
    {
        var behavior = PathSimulator.GatewayThen(
            NetInventory.GatewayIp,
            PathSimulator.AlwaysSuccess(5),
            PathSimulator.Dead());
        var check = CheckWith(behavior);

        var result = await check.ExecuteAsync(ContextWithGateway(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Warning, result.Severity);
        Assert.Contains("internet endpoint is unreachable", result.Summary);
        Assert.Contains(result.Recommendations, r => r.Text.Contains("check dns"));
    }

    [Fact]
    public async Task InternetLossy_ShouldWarn()
    {
        var behavior = PathSimulator.GatewayThen(
            NetInventory.GatewayIp,
            PathSimulator.AlwaysSuccess(5),
            Sequence(PingResults.Success(20), PingResults.Success(20), PingResults.Timeout(), PingResults.Timeout()));
        var check = CheckWith(behavior);

        var result = await check.ExecuteAsync(ContextWithGateway(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Warning, result.Severity);
        Assert.Contains("loss is significant", result.Summary);
    }

    [Fact]
    public async Task InternetSlowLatency_ShouldBeSuspicious()
    {
        var behavior = PathSimulator.GatewayThen(
            NetInventory.GatewayIp,
            PathSimulator.AlwaysSuccess(10),
            PathSimulator.AlwaysSuccess(350));
        var check = CheckWith(behavior);

        var result = await check.ExecuteAsync(ContextWithGateway(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Suspicious, result.Severity);
        Assert.Contains("Latency is elevated", result.Summary);
    }

    [Fact]
    public async Task GatewaySlow_ShouldBeSuspicious()
    {
        var behavior = PathSimulator.GatewayThen(
            NetInventory.GatewayIp,
            PathSimulator.AlwaysSuccess(150),
            PathSimulator.AlwaysSuccess(20));
        var check = CheckWith(behavior);

        var result = await check.ExecuteAsync(ContextWithGateway(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Suspicious, result.Severity);
    }

    [Fact]
    public async Task NoGateway_ShouldBeUnavailable()
    {
        var check = CheckWith(PathSimulator.AlwaysSuccess(5));
        var result = await check.ExecuteAsync(ContextWithGateway(gateway: null), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Unavailable, result.Status);
        Assert.Equal(DiagnosticSeverity.Info, result.Severity);
    }

    [Fact]
    public async Task NoActiveConnection_ShouldBeUnavailable()
    {
        var check = CheckWith(PathSimulator.AlwaysSuccess(5));
        var result = await check.ExecuteAsync(ContextNoConnection(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Unavailable, result.Status);
        Assert.Equal(DiagnosticSeverity.Info, result.Severity);
    }

    [Fact]
    public async Task ConfigurableTargets_AreProbedInsteadOfDefaults()
    {
        var probe = new FakePingProbe(PathSimulator.AlwaysSuccess(5));
        var check = new PacketLossCheck(probe: probe, targetOverrides: new[] { "9.9.9.9" });

        var result = await check.ExecuteAsync(ContextWithGateway(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Passed, result.Status);
        Assert.Contains(IPAddress.Parse("9.9.9.9"), probe.CalledTargets);
        Assert.DoesNotContain(IPAddress.Parse("1.1.1.1"), probe.CalledTargets);
        Assert.Contains(result.Evidence, e => e.Description == "Test Targets" && e.Value.Contains("9.9.9.9"));
    }

    [Fact]
    public async Task Cancellation_IsRespected()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var check = CheckWith(PathSimulator.AlwaysSuccess(5));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => check.ExecuteAsync(ContextWithGateway(), cts.Token));
    }
}