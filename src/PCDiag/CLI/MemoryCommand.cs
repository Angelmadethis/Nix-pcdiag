using PCDiag.Checks.Performance;
using PCDiag.Core;
using PCDiag.Reporting;

namespace PCDiag.CLI;

/// <summary>
/// Implements the <c>pcdiag check memory</c> command: runs the memory pressure check
/// and prints its detailed report. Read-only; no interaction required.
/// </summary>
public static class MemoryCommand
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

            var check = new MemoryCheck();
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
            Console.Error.WriteLine($"pcdiag check memory failed: {ex.Message}");
            return 1;
        }
    }
}