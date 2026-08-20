using PCDiag.Checks.Network;
using PCDiag.Core;
using PCDiag.Fixes;
using PCDiag.Infrastructure;

namespace PCDiag.Tests.Fixes;

public class NetworkFixTests
{
    [Fact]
    public void WinsockReset_Risk_RequiresAdmin_AndId()
    {
        var fix = new WinsockResetFix("Packet loss is significant.");

        Assert.Equal(FixRisk.Medium, fix.Risk);
        Assert.True(fix.RequiresAdmin);
        Assert.Equal("winsock-reset", fix.Id);
    }

    [Fact]
    public async Task WinsockReset_Success_ShouldApply()
    {
        var fix = new WinsockResetFix(
            "Packet loss is significant.",
            _ => Task.FromResult(new CommandResult { ExitCode = 0, StandardOutput = "Ok.", Success = true }));

        var result = await fix.ApplyAsync(CancellationToken.None);

        Assert.Equal(FixApplyOutcome.Applied, result.Outcome);
        Assert.Contains("successfully reset", result.Message);
    }

    [Fact]
    public async Task WinsockReset_Failure_ShouldReportError()
    {
        var fix = new WinsockResetFix(
            "Packet loss is significant.",
            _ => Task.FromResult(new CommandResult { ExitCode = 1, StandardError = "Access is denied.", Success = false }));

        var result = await fix.ApplyAsync(CancellationToken.None);

        Assert.Equal(FixApplyOutcome.Failed, result.Outcome);
        Assert.Equal("Access is denied.", result.ErrorDetail);
    }

    [Fact]
    public void TcpIpStackReset_Risk_RequiresAdmin_AndId()
    {
        var fix = new TcpIpStackResetFix("The gateway is unreachable.");

        Assert.Equal(FixRisk.High, fix.Risk);
        Assert.True(fix.RequiresAdmin);
        Assert.Equal("tcp-ip-stack-reset", fix.Id);
    }

    [Fact]
    public async Task TcpIpStackReset_Success_ShouldApply()
    {
        var fix = new TcpIpStackResetFix(
            "The gateway is unreachable.",
            _ => Task.FromResult(new CommandResult { ExitCode = 0, StandardOutput = "Ok.", Success = true }));

        var result = await fix.ApplyAsync(CancellationToken.None);

        Assert.Equal(FixApplyOutcome.Applied, result.Outcome);
        Assert.Contains("successfully reset", result.Message);
    }

    [Fact]
    public async Task TcpIpStackReset_Failure_ShouldReportError()
    {
        var fix = new TcpIpStackResetFix(
            "The gateway is unreachable.",
            _ => Task.FromResult(new CommandResult { ExitCode = 1, StandardError = "Denied.", Success = false }));

        var result = await fix.ApplyAsync(CancellationToken.None);

        Assert.Equal(FixApplyOutcome.Failed, result.Outcome);
        Assert.Equal("Denied.", result.ErrorDetail);
    }

    [Fact]
    public void RestartAdapter_Title_IncludesAdapterName()
    {
        var fix = new RestartNetworkAdapterFix("Gateway unreachable.", "Ethernet");

        Assert.Equal("restart-network-adapter", fix.Id);
        Assert.Contains("Ethernet", fix.Title);
        Assert.True(fix.RequiresAdmin);
        Assert.Equal(FixRisk.Medium, fix.Risk);
    }

    [Fact]
    public async Task RestartAdapter_Success_ShouldApply()
    {
        var fix = new RestartNetworkAdapterFix(
            "Gateway unreachable.",
            "Ethernet",
            _ => Task.FromResult(new CommandResult { ExitCode = 0, StandardOutput = "", Success = true }));

        var result = await fix.ApplyAsync(CancellationToken.None);

        Assert.Equal(FixApplyOutcome.Applied, result.Outcome);
        Assert.Contains("Ethernet", result.Message);
    }

    [Fact]
    public async Task RestartAdapter_Failure_ShouldReportError()
    {
        var fix = new RestartNetworkAdapterFix(
            "Gateway unreachable.",
            "Ethernet",
            _ => Task.FromResult(new CommandResult { ExitCode = 1, StandardError = "No adapter found.", Success = false }));

        var result = await fix.ApplyAsync(CancellationToken.None);

        Assert.Equal(FixApplyOutcome.Failed, result.Outcome);
        Assert.Equal("No adapter found.", result.ErrorDetail);
    }

    [Fact]
    public void DhcpRenew_Risk_RequiresAdmin_AndId()
    {
        var fix = new DhcpRenewFix("The gateway is unreachable.");

        Assert.Equal(FixRisk.Medium, fix.Risk);
        Assert.True(fix.RequiresAdmin);
        Assert.Equal("dhcp-release-renew", fix.Id);
    }

    [Fact]
    public async Task DhcpRenew_Success_ShouldReleaseAndRenew()
    {
        var fix = new DhcpRenewFix(
            "The gateway is unreachable.",
            _ => Task.FromResult(new CommandResult { ExitCode = 0, StandardOutput = "", Success = true }),
            _ => Task.FromResult(new CommandResult { ExitCode = 0, StandardOutput = "", Success = true }));

        var result = await fix.ApplyAsync(CancellationToken.None);

        Assert.Equal(FixApplyOutcome.Applied, result.Outcome);
        Assert.Contains("released and renewed", result.Message);
    }

    [Fact]
    public async Task DhcpRenew_ReleaseFails_ShouldNotRenew()
    {
        var renewed = false;
        var fix = new DhcpRenewFix(
            "The gateway is unreachable.",
            _ => Task.FromResult(new CommandResult { ExitCode = 1, StandardError = "Denied.", Success = false }),
            _ => { renewed = true; return Task.FromResult(new CommandResult { ExitCode = 0, StandardOutput = "", Success = true }); });

        var result = await fix.ApplyAsync(CancellationToken.None);

        Assert.Equal(FixApplyOutcome.Failed, result.Outcome);
        Assert.False(renewed);
        Assert.Equal("Denied.", result.ErrorDetail);
    }

    [Fact]
    public async Task DhcpRenew_RenewFails_ShouldReportError()
    {
        var fix = new DhcpRenewFix(
            "The gateway is unreachable.",
            _ => Task.FromResult(new CommandResult { ExitCode = 0, StandardOutput = "", Success = true }),
            _ => Task.FromResult(new CommandResult { ExitCode = 1, StandardError = "No DHCP server.", Success = false }));

        var result = await fix.ApplyAsync(CancellationToken.None);

        Assert.Equal(FixApplyOutcome.Failed, result.Outcome);
        Assert.Contains("failed to renew", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("No DHCP server.", result.ErrorDetail);
    }

    [Fact]
    public void AutotuningRestore_Risk_RequiresAdmin_AndId()
    {
        var fix = new AutotuningRestoreFix("Auto-tuning is disabled.");

        Assert.Equal(FixRisk.Medium, fix.Risk);
        Assert.True(fix.RequiresAdmin);
        Assert.Equal("tcp-autotuning-restore", fix.Id);
    }

    [Fact]
    public async Task AutotuningRestore_Success_ShouldApply()
    {
        var fix = new AutotuningRestoreFix(
            "Auto-tuning is disabled.",
            _ => Task.FromResult(new CommandResult { ExitCode = 0, StandardOutput = "Ok.", Success = true }));

        var result = await fix.ApplyAsync(CancellationToken.None);

        Assert.Equal(FixApplyOutcome.Applied, result.Outcome);
        Assert.Contains("restored to Normal", result.Message);
    }

    [Fact]
    public async Task AutotuningRestore_Failure_ShouldReportError()
    {
        var fix = new AutotuningRestoreFix(
            "Auto-tuning is disabled.",
            _ => Task.FromResult(new CommandResult { ExitCode = 1, StandardError = "Access is denied.", Success = false }));

        var result = await fix.ApplyAsync(CancellationToken.None);

        Assert.Equal(FixApplyOutcome.Failed, result.Outcome);
        Assert.Equal("Access is denied.", result.ErrorDetail);
    }
}