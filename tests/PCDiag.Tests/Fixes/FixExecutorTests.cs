using PCDiag.Core;
using PCDiag.Fixes;

namespace PCDiag.Tests.Fixes;

public class FixExecutorTests
{
    private static DiagnosticContext Ctx(bool isAdmin = false) => new(isAdministrator: isAdmin);

    private static async Task<FixExecutionResult> Run(
        IDiagnosticCheck check,
        DiagnosticFix fix,
        DiagnosticResult original,
        bool isAdmin = false)
        => await new FixExecutor().ExecuteAsync(check, fix, Ctx(isAdmin), original, CancellationToken.None);

    [Fact]
    public async Task SuccessfulApplyAndHealthyRecheck_ShouldBeResolved()
    {
        var check = new FakeFixableCheck("NET-TEST-001",
            FixTestResults.Finding("NET-TEST-001"),
            resultAfterFix: FixTestResults.Healthy("NET-TEST-001"));
        var fix = new FakeFix();
        var original = FixTestResults.Finding("NET-TEST-001");

        await check.ExecuteAsync(Ctx(), CancellationToken.None);
        var outcome = await Run(check, fix, original);

        Assert.True(outcome.Applied);
        Assert.True(outcome.Resolved);
        Assert.NotNull(outcome.RecheckResult);
        Assert.Equal(DiagnosticStatus.Passed, outcome.RecheckResult.Status);
        Assert.Equal(1, fix.ApplyCount);
        Assert.Equal(2, check.ExecuteCount);
    }

    [Fact]
    public async Task FailedApply_ShouldReportFailureWithDetail()
    {
        var check = new FakeFixableCheck("NET-TEST-001", FixTestResults.Finding("NET-TEST-001"));
        var fix = new FakeFix(
            outcome: FixApplyOutcome.Failed,
            message: "The fake fix failed.",
            errorDetail: "Access denied");

        var outcome = await Run(check, fix, FixTestResults.Finding("NET-TEST-001"));

        Assert.False(outcome.Applied);
        Assert.False(outcome.Resolved);
        Assert.Equal("The fake fix failed.", outcome.Message);
        Assert.Equal("Access denied", outcome.ErrorDetail);
        Assert.Null(outcome.RecheckResult);
    }

    [Fact]
    public async Task AppliedButIssuePersists_ShouldReportNotResolved()
    {
        var check = new FakeFixableCheck("NET-TEST-001", FixTestResults.Finding("NET-TEST-001"));
        var fix = new FakeFix();

        var outcome = await Run(check, fix, FixTestResults.Finding("NET-TEST-001"));

        Assert.True(outcome.Applied);
        Assert.False(outcome.Resolved);
        Assert.NotNull(outcome.RecheckResult);
        Assert.Contains("still detects the issue", outcome.Message);
    }

    [Fact]
    public async Task TargetedVerifyTrue_ShouldResolveWithoutRecheck()
    {
        var check = new FakeFixableCheck("NET-TEST-001", FixTestResults.Finding("NET-TEST-001"));
        var fix = new FakeFix(verify: true);

        var outcome = await Run(check, fix, FixTestResults.Finding("NET-TEST-001"));

        Assert.True(outcome.Applied);
        Assert.True(outcome.Resolved);
        Assert.Null(outcome.RecheckResult);
        Assert.Equal(0, check.ExecuteCount);
    }

    [Fact]
    public async Task TargetedVerifyFalse_ShouldNotResolve()
    {
        var check = new FakeFixableCheck("NET-TEST-001", FixTestResults.Finding("NET-TEST-001"));
        var fix = new FakeFix(verify: false);

        var outcome = await Run(check, fix, FixTestResults.Finding("NET-TEST-001"));

        Assert.True(outcome.Applied);
        Assert.False(outcome.Resolved);
        Assert.Contains("issue persists", outcome.Message);
    }

    [Fact]
    public async Task AdminRequiredButNotElevated_ShouldNotAttemptFix()
    {
        var check = new FakeFixableCheck("NET-TEST-001", FixTestResults.Finding("NET-TEST-001"));
        var fix = new FakeFix(requiresAdmin: true);

        var outcome = await Run(check, fix, FixTestResults.Finding("NET-TEST-001"), isAdmin: false);

        Assert.False(outcome.Applied);
        Assert.False(outcome.Resolved);
        Assert.Contains("administrator", outcome.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, fix.ApplyCount);
    }

    [Fact]
    public async Task AdminRequiredAndElevated_ShouldApply()
    {
        var check = new FakeFixableCheck("NET-TEST-001",
            FixTestResults.Finding("NET-TEST-001"),
            resultAfterFix: FixTestResults.Healthy("NET-TEST-001"));
        var fix = new FakeFix(requiresAdmin: true);

        await check.ExecuteAsync(Ctx(isAdmin: true), CancellationToken.None);
        var outcome = await Run(check, fix, FixTestResults.Finding("NET-TEST-001"), isAdmin: true);

        Assert.True(outcome.Applied);
        Assert.True(outcome.Resolved);
        Assert.Equal(1, fix.ApplyCount);
    }

    [Fact]
    public async Task NotApplicableApply_ShouldNotBeApplied()
    {
        var check = new FakeFixableCheck("NET-TEST-001", FixTestResults.Finding("NET-TEST-001"));
        var fix = new FakeFix(outcome: FixApplyOutcome.NotApplicable, message: "Nothing to apply.");

        var outcome = await Run(check, fix, FixTestResults.Finding("NET-TEST-001"));

        Assert.False(outcome.Applied);
        Assert.Equal("Nothing to apply.", outcome.Message);
    }
}