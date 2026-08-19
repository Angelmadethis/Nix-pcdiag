using PCDiag.Checks.Network;
using PCDiag.Core;
using PCDiag.Dns;

namespace PCDiag.Tests.Dns;

public class DnsDiagnosticsCheckTests
{
    private static DnsDiagnosticsCheck CheckWith(
        string[] servers,
        params DnsProbeResult[] results)
        => new(
            transport: new FakeDnsTransport(results),
            serverSource: new FakeDnsServerSource(servers));

    private static async Task<DiagnosticResult> RunAsync(DnsDiagnosticsCheck check)
        => await check.ExecuteAsync(new DiagnosticContext(), CancellationToken.None);

    [Fact]
    public async Task Healthy_ShouldPassWithEvidence()
    {
        var check = CheckWith(new[] { "1.1.1.1" },
            DnsProbes.Success(10), DnsProbes.Success(12), DnsProbes.Success(11),
            DnsProbes.Success(9), DnsProbes.Success(10));

        var result = await RunAsync(check);

        Assert.Equal(DiagnosticStatus.Passed, result.Status);
        Assert.Equal(DiagnosticSeverity.Healthy, result.Severity);

        var evidence = Assert.Single(result.Evidence, e => e.Description.StartsWith("DNS Server 1.1.1.1"));
        Assert.Contains("failures: 0", evidence.Value);
        Assert.Contains("timeouts: 0", evidence.Value);
        Assert.Contains("avg: 10 ms", evidence.Value);
        Assert.Contains("min: 9 ms", evidence.Value);
        Assert.Contains("max: 12 ms", evidence.Value);

        Assert.Empty(result.Recommendations);
    }

    [Fact]
    public async Task Slow_ShouldNotRecommendChangingDns()
    {
        var check = CheckWith(new[] { "1.1.1.1" },
            DnsProbes.Success(700), DnsProbes.Success(750), DnsProbes.Success(690),
            DnsProbes.Success(720), DnsProbes.Success(710));

        var result = await RunAsync(check);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Suspicious, result.Severity);
        Assert.Contains(result.Evidence, e => e.Value.Contains("Slow - attempts: 5, successes: 5"));

        var rec = Assert.Single(result.Recommendations);
        Assert.Contains("not sufficient evidence", rec.Text);
        Assert.DoesNotContain("8.8.8.8", rec.Text);
        Assert.DoesNotContain("switch", rec.Text);
    }

    [Fact]
    public async Task Unreliable_ShouldWarnAndOfferChangeOption()
    {
        var check = CheckWith(new[] { "1.1.1.1" },
            DnsProbes.Success(10), DnsProbes.Success(10), DnsProbes.Failure(10),
            DnsProbes.Timeout(), DnsProbes.Timeout());

        var result = await RunAsync(check);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Warning, result.Severity);

        var evidence = Assert.Single(result.Evidence, e => e.Description.StartsWith("DNS Server 1.1.1.1"));
        Assert.Contains("failures: 1", evidence.Value);
        Assert.Contains("timeouts: 2", evidence.Value);

        var rec = Assert.Single(result.Recommendations);
        Assert.Contains("unreliable", rec.Text);
        Assert.Contains("8.8.8.8", rec.Text);
        Assert.True(rec.RequiresAdmin);
    }

    [Fact]
    public async Task Unreachable_ShouldBeCritical()
    {
        var check = CheckWith(new[] { "1.1.1.1" },
            DnsProbes.Timeout(), DnsProbes.Timeout());

        var result = await RunAsync(check);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Critical, result.Severity);

        var evidence = Assert.Single(result.Evidence, e => e.Description.StartsWith("DNS Server 1.1.1.1"));
        Assert.Contains("Unreachable - attempts: 2, successes: 0, failures: 0, timeouts: 2", evidence.Value);

        var rec = Assert.Single(result.Recommendations);
        Assert.Contains("None of the configured DNS resolver(s)", rec.Text);
        Assert.True(rec.RequiresAdmin);
    }

    [Fact]
    public async Task NoServers_ShouldBeUnavailable()
    {
        var check = CheckWith(Array.Empty<string>());

        var result = await RunAsync(check);

        Assert.Equal(DiagnosticStatus.Unavailable, result.Status);
        Assert.Equal(DiagnosticSeverity.Info, result.Severity);
        Assert.Contains(result.Evidence, e => e.Description == "Active DNS Servers" && e.Value == "None configured");
        Assert.Single(result.Recommendations);
    }

    [Fact]
    public async Task OneDeadOneHealthy_ShouldBeUnreliableOverall()
    {
        var check = CheckWith(new[] { "1.1.1.1", "8.8.8.8" },
            // first server: 5 successes
            DnsProbes.Success(10), DnsProbes.Success(10), DnsProbes.Success(10),
            DnsProbes.Success(10), DnsProbes.Success(10),
            // second server: unreachable (2 timeouts, early abort)
            DnsProbes.Timeout(), DnsProbes.Timeout());

        var result = await RunAsync(check);

        Assert.Equal(DiagnosticSeverity.Warning, result.Severity);
        Assert.Equal(DiagnosticStatus.Finding, result.Status);

        var healthy = Assert.Single(result.Evidence, e => e.Description.StartsWith("DNS Server 1.1.1.1"));
        var dead = Assert.Single(result.Evidence, e => e.Description.StartsWith("DNS Server 8.8.8.8"));
        Assert.Contains("Healthy -", healthy.Value);
        Assert.Contains("Unreachable -", dead.Value);

        var rec = Assert.Single(result.Recommendations);
        Assert.Contains("8.8.8.8", rec.Text);
    }

    [Fact]
    public async Task ShouldExposeSettingsAndDomainsAsEvidence()
    {
        var check = CheckWith(new[] { "1.1.1.1" }, DnsProbes.Success(10));

        var result = await RunAsync(check);

        Assert.Contains(result.Evidence, e => e.Description == "Configured DNS Servers" && e.Value == "1.1.1.1");
        Assert.Contains(result.Evidence, e => e.Description == "Test Domains" && e.Value.Contains("example.com"));
        Assert.Contains(result.Evidence, e => e.Description == "Probe Timeout" && e.Value == "1500 ms");
    }
}