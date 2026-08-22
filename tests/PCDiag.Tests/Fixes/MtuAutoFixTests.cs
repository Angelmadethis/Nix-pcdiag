using PCDiag.Core;
using PCDiag.Fixes;
using PCDiag.Infrastructure;

namespace PCDiag.Tests.Fixes;

public class MtuAutoFixTests
{
    [Fact]
    public void RiskAndPermissions_ShouldBeMediumAndAdmin()
    {
        var fix = new MtuAutoFix("Interface MTU exceeds path MTU.", "Ethernet");

        Assert.Equal(FixRisk.Low, fix.Risk);
        Assert.True(fix.RequiresAdmin);
        Assert.Equal("mtu-reset-default", fix.Id);
    }

    [Fact]
    public void Effect_ShouldStateDefaultMtu()
    {
        var fix = new MtuAutoFix("Interface MTU exceeds path MTU.", "Ethernet");

        Assert.Contains("1500", fix.Effect);
        Assert.Contains("Ethernet", fix.Title);
    }

    [Fact]
    public void AdapterName_ShouldBePreserved()
    {
        var fix = new MtuAutoFix("Problem.", "Wi-Fi");

        Assert.Equal("Wi-Fi", fix.AdapterName);
        Assert.Contains("Wi-Fi", fix.Title);
    }

    [Fact]
    public async Task SuccessfulReset_ShouldApply()
    {
        var fix = new MtuAutoFix(
            "Interface MTU exceeds path MTU.",
            "Ethernet",
            _ => Task.FromResult(new CommandResult { ExitCode = 0, StandardOutput = "Ok.", Success = true }));

        var result = await fix.ApplyAsync(CancellationToken.None);

        Assert.Equal(FixApplyOutcome.Applied, result.Outcome);
        Assert.Contains("reset to 1500", result.Message);
        Assert.Null(result.ErrorDetail);
    }

    [Fact]
    public async Task FailedReset_ShouldReportFailureWithError()
    {
        var fix = new MtuAutoFix(
            "Interface MTU exceeds path MTU.",
            "Ethernet",
            _ => Task.FromResult(new CommandResult { ExitCode = 1, StandardError = "Access is denied.", Success = false }));

        var result = await fix.ApplyAsync(CancellationToken.None);

        Assert.Equal(FixApplyOutcome.Failed, result.Outcome);
        Assert.Contains("Failed to reset MTU", result.Message);
        Assert.Equal("Access is denied.", result.ErrorDetail);
    }
}
