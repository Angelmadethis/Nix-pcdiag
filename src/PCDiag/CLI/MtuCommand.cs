using PCDiag.Checks.Network;
using PCDiag.Core;
using PCDiag.Infrastructure;
using PCDiag.Inventory;
using PCDiag.Reporting;

namespace PCDiag.CLI;

/// <summary>
/// Implements the <c>pcdiag check mtu</c> command: runs the interface/path MTU
/// check and prints its detailed report. Optional positional arguments override the
/// internet test target(s). No interaction required.
/// </summary>
public static class MtuCommand
{
    public static async Task<int> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken = default)
    {
        try
        {
            var targets = args.Where(a => !a.StartsWith('-')).ToList();

            var inventory = await Task.Run(SystemInventoryCollector.Collect, cancellationToken);
            var context = new DiagnosticContext(
                mode: ScanMode.Standard,
                isAdministrator: SystemInfo.IsRunningAsAdmin(),
                cancellationToken: cancellationToken,
                defaultTimeout: TimeSpan.FromSeconds(45),
                inventory: inventory);

            var check = new MtuDiagnosticsCheck(targetOverrides: targets);
            var result = await check.ExecuteAsync(context, cancellationToken);

            new TerminalRenderer().PrintDetailed(result);
            return result.Status == DiagnosticStatus.Error ? 1 : 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"pcdiag check mtu failed: {ex.Message}");
            return 1;
        }
    }
}