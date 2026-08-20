using PCDiag.Core;
using PCDiag.Fixes;
using PCDiag.Interactive;
using PCDiag.Inventory;
using PCDiag.Tests.Fixes;
using Spectre.Console.Testing;

namespace PCDiag.Tests;

public class FixFlowTests
{
    private static readonly SystemInventory StubInventory = new()
    {
        System = new OsInfo
        {
            MachineName = "TEST-PC",
            OSVersionString = "Windows 11 Pro",
            Is64Bit = true,
            Architecture = "X64"
        }
    };

    [Fact]
    public async Task ApplyFixFlow_Confirm_ShouldApplyAndShowResolved()
    {
        var check = new FakeFixableCheck(
            "NET-TEST-001",
            FixTestResults.Finding("NET-TEST-001"),
            resultAfterFix: FixTestResults.Healthy("NET-TEST-001"));
        var fix = new FakeFix();
        check.Fixes.Add(fix);

        var console = new TestConsole().Interactive();
        // start scan
        console.Input.PushKey(ConsoleKey.Enter);
        // menu: default = "Fix all problems (1)"
        console.Input.PushKey(ConsoleKey.Enter);
        // proposal: default = "[ Apply ]"
        console.Input.PushKey(ConsoleKey.Enter);
        // rescan (healthy now) -> menu default "View check details"; navigate to Exit
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await InteractiveApp.RunAsync(console, StubInventory, new[] { check });

        Assert.Equal(0, exitCode);
        Assert.Equal(1, fix.ApplyCount);
        Assert.Contains("FIXABLE NET-TEST-001", console.Output);
        Assert.Contains("Problem:", console.Output);
        Assert.Contains("Effect:", console.Output);
        Assert.Contains("FIX APPLIED", console.Output);
        Assert.Contains("Re-running diagnostic", console.Output);
        Assert.Contains("issue resolved", console.Output);
        Assert.Equal(3, check.ExecuteCount);
    }

    [Fact]
    public async Task ApplyFixFlow_PerFindingButton_ShouldApplyThatFinding()
    {
        var check = new FakeFixableCheck(
            "NET-TEST-001",
            FixTestResults.Finding("NET-TEST-001"),
            resultAfterFix: FixTestResults.Healthy("NET-TEST-001"));
        var fix = new FakeFix();
        check.Fixes.Add(fix);

        var console = new TestConsole().Interactive();
        // start scan
        console.Input.PushKey(ConsoleKey.Enter);
        // menu: move from "Fix all problems (1)" down to the "[ FIX ] NET-TEST-001" button
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        // proposal: default = "[ Apply ]"
        console.Input.PushKey(ConsoleKey.Enter);
        // rescan (healthy now) -> menu; navigate to Exit
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await InteractiveApp.RunAsync(console, StubInventory, new[] { check });

        Assert.Equal(0, exitCode);
        Assert.Equal(1, fix.ApplyCount);
        Assert.Contains("[ FIX ] Fixable NET-TEST-001", console.Output);
    }

    [Fact]
    public async Task ApplyFixFlow_Cancel_ShouldNotApply()
    {
        var check = new FakeFixableCheck(
            "NET-TEST-001",
            FixTestResults.Finding("NET-TEST-001"),
            resultAfterFix: FixTestResults.Healthy("NET-TEST-001"));
        var fix = new FakeFix();
        check.Fixes.Add(fix);

        var console = new TestConsole().Interactive();
        // start scan
        console.Input.PushKey(ConsoleKey.Enter);
        // menu: default = "Fix all problems (1)"
        console.Input.PushKey(ConsoleKey.Enter);
        // proposal: move to "[ Cancel ]"
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        // "Press ENTER to continue" after cancel
        console.Input.PushKey(ConsoleKey.Enter);
        // rescan (healthy now) -> menu; navigate to Exit (last of 4 -> 3 downs)
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await InteractiveApp.RunAsync(console, StubInventory, new[] { check });

        Assert.Equal(0, exitCode);
        Assert.Equal(0, fix.ApplyCount);
        Assert.Contains("Fixes cancelled.", console.Output);
        Assert.DoesNotContain("FIX APPLIED", console.Output);
    }

    [Fact]
    public async Task ApplyFixFlow_FailedFix_ShouldShowNotApplied()
    {
        var check = new FakeFixableCheck(
            "NET-TEST-001",
            FixTestResults.Finding("NET-TEST-001"));
        var fix = new FakeFix(outcome: FixApplyOutcome.Failed, message: "The fix failed to apply.", errorDetail: "Access denied");
        check.Fixes.Add(fix);

        var console = new TestConsole().Interactive();
        // start scan
        console.Input.PushKey(ConsoleKey.Enter);
        // menu: default = "Fix all problems (1)"
        console.Input.PushKey(ConsoleKey.Enter);
        // proposal: default = "[ Apply ]"
        console.Input.PushKey(ConsoleKey.Enter);
        // rescan (still finding) -> menu still has fix options; navigate to Exit (6th item -> 5 downs)
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await InteractiveApp.RunAsync(console, StubInventory, new[] { check });

        Assert.Equal(0, exitCode);
        Assert.Equal(1, fix.ApplyCount);
        Assert.Contains("FIX NOT APPLIED", console.Output);
        Assert.Contains("Access denied", console.Output);
    }

    [Fact]
    public async Task NoFixableFindings_ShouldNotOfferApplyFixes()
    {
        var check = new FakeFixableCheck("NET-TEST-001", FixTestResults.Healthy("NET-TEST-001"));

        var console = new TestConsole().Interactive();
        // start scan
        console.Input.PushKey(ConsoleKey.Enter);
        // menu (healthy -> no fix options): navigate to Exit (last of 4 -> 3 downs)
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await InteractiveApp.RunAsync(console, StubInventory, new[] { check });

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("Fix all problems", console.Output);
        Assert.DoesNotContain("[ FIX ]", console.Output);
    }
}