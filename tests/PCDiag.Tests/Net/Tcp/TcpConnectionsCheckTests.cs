using PCDiag.Checks.Network;
using PCDiag.Core;
using PCDiag.Net.Tcp;
using PCDiag.Tests.Net.Tcp;

namespace PCDiag.Tests.Net.Tcp;

public class TcpConnectionsCheckTests
{
    private static readonly DiagnosticContext Context = new(mode: ScanMode.Standard, inventory: TcpInventory.WithActiveAdapter());

    private static TcpConnectionsCheck Check(FakeTcpConnectionSource source, FakeTcpConfigSource? config = null)
        => new(connectionSource: source, configSource: config ?? new FakeTcpConfigSource());

    [Fact]
    public async Task Healthy_TypicalCounts_ShouldPass()
    {
        var source = new FakeTcpConnectionSource();
        source.Connections.AddRange(new[]
        {
            TcpConn.Listen(80, 4),
            TcpConn.Listen(443, 4),
            TcpConn.Established(50000, 1000),
            TcpConn.Established(50001, 1000),
            TcpConn.TimeWait(50002),
            TcpConn.TimeWait(50003)
        });

        var result = await Check(source).ExecuteAsync(Context, CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Passed, result.Status);
        Assert.Equal(DiagnosticSeverity.Healthy, result.Severity);
        Assert.Empty(result.Recommendations);
        Assert.Contains("look normal", result.Summary);
    }

    [Fact]
    public async Task HighTimeWait_ShouldWarnButContextualize()
    {
        var source = new FakeTcpConnectionSource();
        source.Connections.AddRange(Enumerable.Range(0, 10000).Select(i => TcpConn.TimeWait(49152 + i)));

        var result = await Check(source).ExecuteAsync(Context, CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Warning, result.Severity);
        Assert.Contains("dynamic port", result.Summary);
        Assert.DoesNotContain("leak", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Evidence, e => e.Description == "TIME_WAIT Context" && e.Value.Contains("61%"));
        Assert.Single(result.Recommendations);
        Assert.Contains("Re-run the check", result.Recommendations[0].Text);
    }

    [Fact]
    public async Task CloseWaitCluster_ShouldBeSuspicious()
    {
        var source = new FakeTcpConnectionSource();
        source.Connections.AddRange(Enumerable.Range(0, 30).Select(i => TcpConn.CloseWait(49152 + i, 3000)));

        var result = await Check(source).ExecuteAsync(Context, CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Suspicious, result.Severity);
        Assert.Contains(result.Evidence, e => e.Description == "CLOSE_WAIT by Process");
        Assert.Single(result.Recommendations);
        Assert.Contains("not closing connections", result.Recommendations[0].Text);
    }

    [Fact]
    public async Task NoConnections_ShouldPass()
    {
        var result = await Check(new FakeTcpConnectionSource()).ExecuteAsync(Context, CancellationToken.None);

        Assert.Equal(DiagnosticStatus.Passed, result.Status);
        Assert.Contains("look normal", result.Summary);
    }

    [Fact]
    public async Task PortRange_ShouldComeFromConfigSource()
    {
        var source = new FakeTcpConnectionSource();
        source.Connections.AddRange(Enumerable.Range(0, 4096).Select(i => TcpConn.TimeWait(49152 + i)));

        var config = new FakeTcpConfigSource();
        var result = await Check(source, config).ExecuteAsync(Context, CancellationToken.None);

        Assert.Contains(result.Evidence, e => e.Description == "Dynamic Port Range" && e.Value.Contains("49152-65535"));
        Assert.Equal(DiagnosticStatus.Finding, result.Status);
    }
}