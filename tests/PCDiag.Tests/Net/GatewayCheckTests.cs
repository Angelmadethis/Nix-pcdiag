using System.Net;
using PCDiag.Checks.Network;
using PCDiag.Core;
using PCDiag.Net;

namespace PCDiag.Tests.Net;

public class GatewayCheckTests
{
    private static DiagnosticContext ContextWithGateway(string? gateway = "192.168.1.1")
        => new(mode: ScanMode.Standard, inventory: NetInventory.WithGateway(gateway));

    private static DiagnosticContext ContextNoConnection()
        => new(mode: ScanMode.Standard, inventory: NetInventory.WithNoActiveConnection());

    private static GatewayCheck CheckWith(Func<IPAddress, int, bool, TimeSpan, CancellationToken, Task<PingProbeResult>> behavior)
        => new(probe: new FakePingProbe(behavior));

    [Fact]
    public async Task Healthy_ShouldPass()
    {
        var check = CheckWith(PathSimulator.AlwaysSuccess(5));
        var result = await check.ExecuteAsync(ContextWithGateway(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Passed, result.Status);
        Assert.Equal(DiagnosticSeverity.Healthy, result.Severity);
        Assert.Empty(result.Recommendations);

        Assert.Contains(result.Evidence, e => e.Description == "Packet Loss" && e.Value.Contains("0%"));
    }

    [Fact]
    public async Task Unreachable_ShouldBeCritical()
    {
        var check = CheckWith(PathSimulator.Dead());
        var result = await check.ExecuteAsync(ContextWithGateway(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Critical, result.Severity);
        Assert.Contains("unreachable", result.Summary);

        var rec = Assert.Single(result.Recommendations);
        Assert.Contains("router", rec.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lossy_ShouldWarn()
    {
        var counter = 0;
        var check = CheckWith((_, _, _, _, _) =>
        {
            counter++;
            return Task.FromResult(counter % 2 == 0 ? PingResults.Timeout() : PingResults.Success(5));
        });

        var result = await check.ExecuteAsync(ContextWithGateway(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Warning, result.Severity);
        Assert.Contains(result.Evidence, e => e.Description == "Packet Loss" && e.Value.Contains("50%"));
        Assert.NotEmpty(result.Recommendations);
    }

    [Fact]
    public async Task Slow_ShouldBeSuspicious()
    {
        var check = CheckWith(PathSimulator.AlwaysSuccess(150));
        var result = await check.ExecuteAsync(ContextWithGateway(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Suspicious, result.Severity);
        Assert.Contains("latency", result.Summary, StringComparison.OrdinalIgnoreCase);
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
}