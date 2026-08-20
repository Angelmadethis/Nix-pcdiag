using PCDiag.Checks.Network;
using PCDiag.Core;
using PCDiag.Infrastructure;
using PCDiag.Inventory;
using PCDiag.Reporting;

namespace PCDiag.CLI;

/// <summary>
/// Implements the <c>pcdiag check connections</c> command: analyzes TCP connection
/// states (TIME_WAIT, CLOSE_WAIT, established) and prints the detailed report.
/// Read-only; no interaction required.
/// </summary>
public static class ConnectionsCommand
{
    public static async Task<int> RunAsync(CancellationToken cancellationToken = default)
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

            var check = new TcpConnectionsCheck();
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
            Console.Error.WriteLine($"pcdiag check connections failed: {ex.Message}");
            return 1;
        }
    }
}