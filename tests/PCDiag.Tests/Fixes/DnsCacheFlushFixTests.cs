using PCDiag.Checks.Network;
using PCDiag.Core;
using PCDiag.Fixes;
using PCDiag.Infrastructure;

namespace PCDiag.Tests.Fixes;

public class DnsCacheFlushFixTests
{
    [Fact]
    public void RiskAndPermissions_ShouldBeLowAndNoAdmin()
    {
        var fix = new DnsCacheFlushFix("DNS resolution is slow.");

        Assert.Equal(FixRisk.Low, fix.Risk);
        Assert.False(fix.RequiresAdmin);
        Assert.Equal("dns-flush-cache", fix.Id);
    }

    [Fact]
    public void Effect_ShouldStateNoServerSettingsChanged()
    {
        var fix = new DnsCacheFlushFix("DNS resolution is slow.");

        Assert.Contains("No DNS server settings will be changed", fix.Effect);
    }

    [Fact]
    public async Task SuccessfulFlush_ShouldApply()
    {
        var fix = new DnsCacheFlushFix(
            "DNS resolution is slow.",
            _ => Task.FromResult(new CommandResult { ExitCode = 0, StandardOutput = "Flushed the DNS Resolver Cache.", Success = true }));

        var result = await fix.ApplyAsync(CancellationToken.None);

        Assert.Equal(FixApplyOutcome.Applied, result.Outcome);
        Assert.Contains("successfully flushed", result.Message);
        Assert.Null(result.ErrorDetail);
    }

    [Fact]
    public async Task FailedFlush_ShouldReportFailureWithError()
    {
        var fix = new DnsCacheFlushFix(
            "DNS resolution is slow.",
            _ => Task.FromResult(new CommandResult { ExitCode = 1, StandardError = "Access is denied.", Success = false }));

        var result = await fix.ApplyAsync(CancellationToken.None);

        Assert.Equal(FixApplyOutcome.Failed, result.Outcome);
        Assert.Contains("Failed to flush", result.Message);
        Assert.Equal("Access is denied.", result.ErrorDetail);
    }

    [Fact]
    public void GetFixes_HealthyResult_ShouldBeEmpty()
    {
        var check = new DnsDiagnosticsCheck();

        var fixes = check.GetFixes(new DiagnosticResult
        {
            CheckId = check.CheckId,
            Name = check.Name,
            Category = check.Category,
            Severity = DiagnosticSeverity.Healthy,
            Status = DiagnosticStatus.Passed,
            Summary = "All resolvers responded reliably."
        });

        Assert.Empty(fixes);
    }

    [Fact]
    public void GetFixes_SlowFinding_ShouldOfferFlushWithLatencyProblem()
    {
        var check = new DnsDiagnosticsCheck();

        var fixes = check.GetFixes(new DiagnosticResult
        {
            CheckId = check.CheckId,
            Name = check.Name,
            Category = check.Category,
            Severity = DiagnosticSeverity.Suspicious,
            Status = DiagnosticStatus.Finding,
            Summary = "DNS resolution works but average latency is elevated."
        });

        var fix = Assert.Single(fixes);
        Assert.IsType<DnsCacheFlushFix>(fix);
        Assert.Contains("high latency", fix.Problem);
    }

    [Fact]
    public void GetFixes_WarningFinding_ShouldOfferFlushWithUnreliableProblem()
    {
        var check = new DnsDiagnosticsCheck();

        var fixes = check.GetFixes(new DiagnosticResult
        {
            CheckId = check.CheckId,
            Name = check.Name,
            Category = check.Category,
            Severity = DiagnosticSeverity.Warning,
            Status = DiagnosticStatus.Finding,
            Summary = "One or more configured DNS resolvers are unreliable."
        });

        var fix = Assert.Single(fixes);
        Assert.IsType<DnsCacheFlushFix>(fix);
        Assert.Contains("unreliable", fix.Problem);
    }

    [Fact]
    public void GetFixes_Unavailable_ShouldBeEmpty()
    {
        var check = new DnsDiagnosticsCheck();

        var fixes = check.GetFixes(new DiagnosticResult
        {
            CheckId = check.CheckId,
            Name = check.Name,
            Category = check.Category,
            Severity = DiagnosticSeverity.Info,
            Status = DiagnosticStatus.Unavailable,
            Summary = "No active DNS configuration was found."
        });

        Assert.Empty(fixes);
    }
}