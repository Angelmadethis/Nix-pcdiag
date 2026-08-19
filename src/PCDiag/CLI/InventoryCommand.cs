using PCDiag.Inventory;
using PCDiag.Reporting;

namespace PCDiag.CLI;

/// <summary>
/// Implements the minimal <c>pcdiag info</c> command: collect and print the
/// read-only system inventory, then exit. No interaction required.
/// </summary>
public static class InventoryCommand
{
    public static async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var inventory = await Task.Run(SystemInventoryCollector.Collect, cancellationToken);

            new InventoryRenderer().Print(inventory);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"pcdiag info failed: {ex.Message}");
            return 1;
        }
    }
}