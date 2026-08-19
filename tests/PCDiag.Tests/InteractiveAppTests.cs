using PCDiag.Checks.Windows;
using PCDiag.Core;
using PCDiag.Infrastructure;
using PCDiag.Interactive;
using PCDiag.Inventory;
using Spectre.Console;
using Spectre.Console.Testing;

namespace PCDiag.Tests;

public class InteractiveAppTests
{
    private static readonly IReadOnlyList<IDiagnosticCheck> NoNetworkChecks = new IDiagnosticCheck[]
    {
        new EnvironmentCheck()
    };

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
    public async Task RunAsync_EnterKey_ShouldScanAndShowResults()
    {
        var console = new TestConsole().Interactive();
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await InteractiveApp.RunAsync(console, StubInventory, NoNetworkChecks);

        Assert.Equal(0, exitCode);
        Assert.Contains("Windows PC Diagnostic Tool", console.Output);
        Assert.Contains("SCAN RESULTS", console.Output);
        Assert.Contains("WIN-ENV-001", console.Output);
        Assert.Contains("Risk Score", console.Output);
    }

    [Fact]
    public async Task RunAsync_EscapeKey_ShouldExitWithoutScanning()
    {
        var console = new TestConsole().Interactive();
        console.Input.PushKey(ConsoleKey.Escape);

        var exitCode = await InteractiveApp.RunAsync(console, StubInventory, NoNetworkChecks);

        Assert.Equal(0, exitCode);
        Assert.Contains("Windows PC Diagnostic Tool", console.Output);
        Assert.DoesNotContain("SCAN RESULTS", console.Output);
    }

    [Fact]
    public async Task RunAsync_SystemInfo_ShouldPrintInventory()
    {
        var console = new TestConsole().Interactive();
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await InteractiveApp.RunAsync(console, StubInventory, NoNetworkChecks);

        Assert.Equal(0, exitCode);
        Assert.Contains("PCDIAG SYSTEM INFORMATION", console.Output);
        Assert.Contains("TEST-PC", console.Output);
        Assert.Contains("Windows 11 Pro", console.Output);
    }
}