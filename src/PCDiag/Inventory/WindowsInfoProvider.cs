using PCDiag.Infrastructure;

namespace PCDiag.Inventory;

/// <summary>
/// Collects Windows-specific information (product name, edition, install date,
/// uptime, boot time) via WMI/CIM and .NET APIs.
/// </summary>
public static class WindowsInfoProvider
{
    public static WindowsInfo Collect()
    {
        var row = WmiQuery.Query("SELECT Caption, InstallDate FROM Win32_OperatingSystem").FirstOrDefault();

        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var bootTime = DateTime.UtcNow - uptime;

        return new WindowsInfo
        {
            ProductName = row is null ? null : WmiQuery.GetString(row, "Caption"),
            Edition = GetEdition(row is null ? null : WmiQuery.GetString(row, "Caption")),
            InstallDate = row is null ? null : WmiQuery.GetDateTime(row, "InstallDate"),
            Uptime = uptime,
            BootTime = bootTime
        };
    }

    /// <summary>
    /// Extract a short edition label (e.g. "Pro", "Home", "Enterprise") from the
    /// Windows caption (e.g. "Microsoft Windows 11 Pro").
    /// </summary>
    public static string? GetEdition(string? caption)
    {
        if (string.IsNullOrWhiteSpace(caption))
            return null;

        var known = new[] { "Home", "Pro", "Enterprise", "Education", "Professional", "Core", "Pro for Workstations" };
        foreach (var edition in known)
        {
            if (caption.Contains(edition, StringComparison.OrdinalIgnoreCase))
                return edition;
        }

        return null;
    }
}