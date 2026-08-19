using System.Net;
using PCDiag.Checks.Network;
using PCDiag.Core;
using PCDiag.Net;

namespace PCDiag.Tests.Net;

public class MtuDiagnosticsCheckTests
{
    private static DiagnosticContext ContextWithGateway(string? gateway = "192.168.1.1")
        => new(mode: ScanMode.Standard, inventory: NetInventory.WithGateway(gateway));

    private static DiagnosticContext ContextNoConnection()
        => new(mode: ScanMode.Standard, inventory: NetInventory.WithNoActiveConnection());

    private static MtuDiagnosticsCheck CheckWith(
        int? interfaceMtu,
        Func<IPAddress, int, bool, TimeSpan, CancellationToken, Task<PingProbeResult>> behavior,
        IReadOnlyList<string>? overrides = null)
        => new(
            probe: new FakePingProbe(behavior),
            mtuSource: new FakeMtuSource(interfaceMtu),
            targetOverrides: overrides);

    [Fact]
    public async Task Healthy_Standard1500_ShouldPass()
    {
        var check = CheckWith(1500, PathSimulator.Cooperative(1500));
        var result = await check.ExecuteAsync(ContextWithGateway(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Passed, result.Status);
        Assert.Equal(DiagnosticSeverity.Healthy, result.Severity);
        Assert.Contains("consistent", result.Summary);
        Assert.Empty(result.Recommendations);

        Assert.Contains(result.Evidence, e => e.Description.StartsWith("Path MTU to") && e.Value.Contains("1500 bytes"));
    }

    [Fact]
    public async Task Healthy_LegitimatePppoe1492_ShouldNotFlagDifferentTechnology()
    {
        var check = CheckWith(1492, PathSimulator.Cooperative(1492));
        var result = await check.ExecuteAsync(ContextWithGateway(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Passed, result.Status);
        Assert.Equal(DiagnosticSeverity.Healthy, result.Severity);
        Assert.Empty(result.Recommendations);
    }

    [Fact]
    public async Task Healthy_Jumbo9000_ShouldNotFlag()
    {
        var check = CheckWith(9000, PathSimulator.Cooperative(9000));
        var result = await check.ExecuteAsync(ContextWithGateway(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Passed, result.Status);
        Assert.Equal(DiagnosticSeverity.Healthy, result.Severity);
    }

    [Fact]
    public async Task ConfirmedMismatch_ShouldWarnWithPotentialPhrasing()
    {
        var check = CheckWith(1500, PathSimulator.Cooperative(1492));
        var result = await check.ExecuteAsync(ContextWithGateway(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Warning, result.Severity);
        Assert.Contains("Potential MTU/path issue", result.Summary);
        Assert.Contains("1492", result.Summary);
        Assert.Contains("1500", result.Summary);

        Assert.Contains(result.Evidence, e => e.Description.StartsWith("Path MTU to") && e.Value.Contains("1492 bytes"));

        Assert.NotEmpty(result.Recommendations);
        Assert.Contains(result.Recommendations, r => r.Text.Contains("interface MTU"));
    }

    [Fact]
    public async Task BlackHole_ShouldWarnAboutDroppedPackets()
    {
        var check = CheckWith(1500, PathSimulator.BlackHole(1400));
        var result = await check.ExecuteAsync(ContextWithGateway(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Warning, result.Severity);
        Assert.Contains("Potential MTU/path issue", result.Summary);
        Assert.Contains("black hole", result.Summary);

        var indicator = Assert.Single(result.Evidence, e => e.Description == "Fragmentation Indicator");
        Assert.Contains("silently dropped", indicator.Value);

        Assert.Contains(result.Recommendations, r => r.Text.Contains("black hole"));
    }

    [Fact]
    public async Task InterfaceMtuUnknown_ShouldBeUnavailable()
    {
        var check = CheckWith(null, PathSimulator.Cooperative(1500));
        var result = await check.ExecuteAsync(ContextWithGateway(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Unavailable, result.Status);
        Assert.Equal(DiagnosticSeverity.Info, result.Severity);
        Assert.Contains("could not be determined", result.Summary);
        Assert.Contains(result.Evidence, e => e.Description == "Interface MTU" && e.Value == "Unknown");
    }

    [Fact]
    public async Task NoGateway_ShouldBeUnavailable()
    {
        var check = CheckWith(1500, PathSimulator.Cooperative(1500));
        var result = await check.ExecuteAsync(ContextWithGateway(gateway: null), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Unavailable, result.Status);
        Assert.Equal(DiagnosticSeverity.Info, result.Severity);
        Assert.Contains("No default gateway", result.Summary);
    }

    [Fact]
    public async Task NoActiveConnection_ShouldBeUnavailable()
    {
        var check = CheckWith(1500, PathSimulator.Cooperative(1500));
        var result = await check.ExecuteAsync(ContextNoConnection(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Unavailable, result.Status);
        Assert.Equal(DiagnosticSeverity.Info, result.Severity);
    }

    [Fact]
    public async Task DeadTarget_ShouldBeUnmeasurableNotHealthy()
    {
        var check = CheckWith(1500, PathSimulator.Dead());
        var result = await check.ExecuteAsync(ContextWithGateway(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Unavailable, result.Status);
        Assert.Equal(DiagnosticSeverity.Info, result.Severity);
        Assert.Contains("could not be measured", result.Summary);
    }

    [Fact]
    public async Task InternetDead_FallsBackToGatewayMeasurement()
    {
        var behavior = PathSimulator.GatewayThen(
            NetInventory.GatewayIp,
            PathSimulator.Cooperative(1500),
            PathSimulator.Dead());
        var check = CheckWith(1500, behavior);

        var result = await check.ExecuteAsync(ContextWithGateway(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Passed, result.Status);
        Assert.Equal(DiagnosticSeverity.Healthy, result.Severity);
    }

    [Fact]
    public async Task ConfigurableTarget_IsUsedForInternetPath()
    {
        var probe = new FakePingProbe(PathSimulator.Cooperative(1500));
        var check = new MtuDiagnosticsCheck(
            probe: probe,
            mtuSource: new FakeMtuSource(1500),
            targetOverrides: new[] { "9.9.9.9" });

        var result = await check.ExecuteAsync(ContextWithGateway(), CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Passed, result.Status);
        Assert.Contains(IPAddress.Parse("9.9.9.9"), probe.CalledTargets);
        Assert.Contains(result.Evidence, e => e.Description == "Test Targets" && e.Value.Contains("9.9.9.9"));
    }
}