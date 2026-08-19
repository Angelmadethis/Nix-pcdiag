using PCDiag.Checks.Network;
using PCDiag.Checks.Windows;
using PCDiag.Core;

namespace PCDiag.Infrastructure;

/// <summary>
/// Central registry of all available diagnostic checks.
/// New checks are registered here.
/// </summary>
public static class CheckRegistry
{
    /// <summary>
    /// Create and return all registered diagnostic checks.
    /// </summary>
    public static IReadOnlyList<IDiagnosticCheck> GetAllChecks()
    {
        return new List<IDiagnosticCheck>
        {
            new EnvironmentCheck(),
            new DnsDiagnosticsCheck(),
            new MtuDiagnosticsCheck(),
            new GatewayCheck(),
            new PacketLossCheck()
        };
    }

    /// <summary>
    /// Get a check by its ID or name (case-insensitive).
    /// </summary>
    public static IDiagnosticCheck? GetCheck(string nameOrId)
    {
        return GetAllChecks().FirstOrDefault(c =>
            string.Equals(c.CheckId, nameOrId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(c.Name, nameOrId, StringComparison.OrdinalIgnoreCase));
    }
}