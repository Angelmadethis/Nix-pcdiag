using Microsoft.Win32;
using PCDiag.Infrastructure;

namespace PCDiag.Memory;

/// <summary>
/// Reads pagefile configuration (read-only registry, never written) and usage
/// (Win32_PageFileUsage). Win32_PageFileSetting is not relied on because it is
/// empty on many systems; the registry PagingFiles value is the authoritative
/// configuration source.
/// </summary>
public sealed class WmiPagefileSource : IPagefileSource
{
    private const long Mb = 1024L * 1024;
    private const string MemoryManagementKey =
        @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management";

    public PagefileInfo GetInfo()
    {
        var config = ReadConfig();
        var usageRows = WmiQuery.Query("SELECT Name, AllocatedBaseSize, CurrentUsage, PeakUsage FROM Win32_PageFileUsage");

        var entries = new List<PagefileEntry>();
        long? totalAllocated = 0;
        long? totalCurrent = 0;
        long? totalPeak = 0;
        bool anyUsage = false;

        foreach (var row in usageRows)
        {
            var allocated = WmiQuery.GetInt64(row, "AllocatedBaseSize") * Mb;
            var current = WmiQuery.GetInt64(row, "CurrentUsage") * Mb;
            var peak = WmiQuery.GetInt64(row, "PeakUsage") * Mb;
            if (allocated is long a) { totalAllocated += a; anyUsage = true; }
            if (current is long c) { totalCurrent += c; anyUsage = true; }
            if (peak is long p) { totalPeak += p; anyUsage = true; }

            entries.Add(new PagefileEntry
            {
                Location = WmiQuery.GetString(row, "Name") ?? "unknown",
                AllocatedBytes = allocated,
                CurrentBytes = current,
                PeakBytes = peak
            });
        }

        var computer = WmiQuery.Query("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem").FirstOrDefault();
        long? physical = computer is null ? null : WmiQuery.GetInt64(computer, "TotalPhysicalMemory");

        return new PagefileInfo
        {
            Config = config,
            Usage = entries,
            TotalAllocatedBytes = anyUsage ? totalAllocated : null,
            TotalCurrentBytes = anyUsage ? totalCurrent : null,
            TotalPeakBytes = anyUsage ? totalPeak : null,
            PhysicalBytes = physical,
            UsageAvailable = anyUsage
        };
    }

    private static PagefileConfig ReadConfig()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(MemoryManagementKey);
            var value = key?.GetValue("PagingFiles");
            var raw = value switch
            {
                string s => new[] { s },
                string[] arr => arr,
                _ => null
            };
            return PagefileConfigParser.Parse(raw);
        }
        catch
        {
            return new PagefileConfig { IsSystemManaged = false, Entries = Array.Empty<string>(), Available = false };
        }
    }
}