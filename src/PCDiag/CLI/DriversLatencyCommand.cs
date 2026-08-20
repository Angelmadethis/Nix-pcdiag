using PCDiag.Checks.Hardware;
using PCDiag.Core;
using PCDiag.Reporting;

namespace PCDiag.CLI;

/// <summary>
/// Implements the <c>pcdiag check drivers-latency</c> command: samples interrupt and
/// DPC activity from performance counters and prints the detailed report. Read-only;
/// no interaction required.
/// </summary>
public static class DriversLatencyCommand
{
    public static async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var context = new DiagnosticContext(
                mode: ScanMode.Standard,
                isAdministrator: Infrastructure.SystemInfo.IsRunningAsAdmin(),
                cancellationToken: cancellationToken,
                defaultTimeout: TimeSpan.FromSeconds(45));

            var check = new DriverLatencyCheck();
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
            Console.Error.WriteLine($"pcdiag check drivers-latency failed: {ex.Message}");
            return 1;
        }
    }
}