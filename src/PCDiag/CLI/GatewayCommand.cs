using PCDiag.Checks.Network;
using PCDiag.Core;
using PCDiag.Infrastructure;
using PCDiag.Inventory;
using PCDiag.Reporting;

namespace PCDiag.CLI;

/// <summary>
/// Implements the <c>pcdiag check gateway</c> command: runs the default gateway
/// reachability check and prints its detailed report. No interaction required.
/// </summary>
public static class GatewayCommand
{
    public static async Task<int> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken = default)
    {
        try
        {
            var inventory = await Task.Run(SystemInventoryCollector.Collect, cancellationToken);
            var context = new DiagnosticContext(
                mode: ScanMode.Standard,
                isAdministrator: SystemInfo.IsRunningAsAdmin(),
                cancellationToken: cancellationToken,
                defaultTimeout: TimeSpan.FromSeconds(45),
                inventory: inventory);

            var check = new GatewayCheck();
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
            Console.Error.WriteLine($"pcdiag check gateway failed: {ex.Message}");
            return 1;
        }
    }
}