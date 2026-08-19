namespace PCDiag.Core;

/// <summary>
/// The environment in which diagnostic checks run.
/// Passed to every check so it can adapt to the scan mode and its constraints.
/// </summary>
public sealed class DiagnosticContext
{
    /// <summary>The depth of the current scan.</summary>
    public ScanMode Mode { get; }

    /// <summary>Whether the current process is running with administrator privileges.</summary>
    public bool IsAdministrator { get; }

    /// <summary>The cancellation token for the current scan.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>Default per-check timeout applied by the scanner.</summary>
    public TimeSpan DefaultTimeout { get; }

    /// <summary>
    /// Read-only system inventory gathered before the scan, when available.
    /// Null when no inventory was collected (e.g. standalone check runs).
    /// </summary>
    public PCDiag.Inventory.SystemInventory? Inventory { get; }

    public DiagnosticContext(
        ScanMode mode = ScanMode.Standard,
        bool isAdministrator = false,
        CancellationToken cancellationToken = default,
        TimeSpan? defaultTimeout = null,
        PCDiag.Inventory.SystemInventory? inventory = null)
    {
        Mode = mode;
        IsAdministrator = isAdministrator;
        CancellationToken = cancellationToken;
        DefaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(30);
        Inventory = inventory;
    }
}