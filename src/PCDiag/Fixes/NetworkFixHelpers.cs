using PCDiag.Core;

namespace PCDiag.Fixes;

/// <summary>
/// Shared helpers for building fixes from diagnostic results. Kept internal so the
/// checks can compose fixes without duplicating evidence parsing.
/// </summary>
internal static class NetworkFixHelpers
{
    /// <summary>
    /// Reads the "Active Adapter" evidence row (formatted as "Name (ip1, ip2)") and
    /// returns just the adapter name, or null when the evidence is missing.
    /// </summary>
    public static string? GetActiveAdapterName(DiagnosticResult result)
    {
        var row = result.Evidence.FirstOrDefault(e => e.Description == "Active Adapter");
        if (row is null)
            return null;

        var separator = row.Value.IndexOf(" (", StringComparison.Ordinal);
        return separator > 0 ? row.Value[..separator] : row.Value;
    }

    /// <summary>
    /// Reads the "Receive Window Auto-Tuning" evidence row and returns true when the
    /// value is a non-default level (i.e. a fix to restore Normal is relevant).
    /// </summary>
    public static bool IsAutotuningNonDefault(DiagnosticResult result)
    {
        var row = result.Evidence.FirstOrDefault(e => e.Description == "Receive Window Auto-Tuning");
        return row is not null
               && row.Value != "Normal"
               && row.Value != "Unknown";
    }
}