namespace PCDiag.Infrastructure;

/// <summary>
/// Helper methods for safely reading Windows system information.
/// All methods are read-only and never modify the system.
/// </summary>
public static class SystemInfo
{
    /// <summary>Get the Windows version string.</summary>
    public static string GetWindowsVersion()
    {
        return Environment.OSVersion.VersionString;
    }

    /// <summary>Get the machine name.</summary>
    public static string GetMachineName()
    {
        return Environment.MachineName;
    }

    /// <summary>Check if the current process has administrator privileges.</summary>
    public static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Get total physical memory in bytes.</summary>
    public static long GetTotalPhysicalMemory()
    {
        try
        {
            // Use WMI via COM interop to avoid System.Management dependency
            var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            foreach (var obj in searcher.Get())
            {
                if (obj["TotalPhysicalMemory"] is ulong value)
                    return (long)value;
            }
        }
        catch { }

        return 0;
    }

    /// <summary>Format bytes to human-readable size.</summary>
    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        int unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:F1} {units[unitIndex]}";
    }
}
