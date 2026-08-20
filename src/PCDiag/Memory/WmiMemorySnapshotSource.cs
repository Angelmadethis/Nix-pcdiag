using PCDiag.Infrastructure;

namespace PCDiag.Memory;

/// <summary>
/// Reads memory state from WMI: Win32_OperatingSystem (installed/free memory),
/// Win32_PerfFormattedData_PerfOS_Memory (commit, available, paging, pools) and
/// Win32_PageFileUsage (pagefile usage). Values are never fabricated; a source that
/// cannot be read is reported as unavailable.
/// </summary>
public sealed class WmiMemorySnapshotSource : IMemorySnapshotSource
{
    private const long Mb = 1024L * 1024;
    private const long Kb = 1024L;

    public MemorySnapshot GetSnapshot()
    {
        var os = WmiQuery.Query("SELECT TotalVisibleMemorySize, FreePhysicalMemory, TotalVirtualMemorySize, FreeVirtualMemory, SizeStoredInPagingFiles FROM Win32_OperatingSystem").FirstOrDefault();
        var perf = WmiQuery.Query("SELECT CommittedBytes, CommitLimit, AvailableMBytes, PagesPerSec, CacheBytes, PoolNonpagedBytes, PoolPagedBytes FROM Win32_PerfFormattedData_PerfOS_Memory").FirstOrDefault();
        var pagefileRows = WmiQuery.Query("SELECT AllocatedBaseSize, CurrentUsage, PeakUsage FROM Win32_PageFileUsage");

        long? osTotal = os is null ? null : WmiQuery.GetInt64(os, "TotalVisibleMemorySize") * Kb;
        long? osFree = os is null ? null : WmiQuery.GetInt64(os, "FreePhysicalMemory") * Kb;

        long? perfCommitted = perf is null ? null : WmiQuery.GetInt64(perf, "CommittedBytes");
        long? perfCommitLimit = perf is null ? null : WmiQuery.GetInt64(perf, "CommitLimit");
        long? perfAvailable = perf is null ? null : WmiQuery.GetInt64(perf, "AvailableMBytes") * Mb;
        long? perfPages = perf is null ? null : WmiQuery.GetInt64(perf, "PagesPerSec");
        long? perfCache = perf is null ? null : WmiQuery.GetInt64(perf, "CacheBytes");
        long? perfNonpaged = perf is null ? null : WmiQuery.GetInt64(perf, "PoolNonpagedBytes");
        long? perfPaged = perf is null ? null : WmiQuery.GetInt64(perf, "PoolPagedBytes");

        long? pfAllocated = 0;
        long? pfCurrent = 0;
        long? pfPeak = 0;
        bool pfAny = false;
        foreach (var row in pagefileRows)
        {
            var allocated = WmiQuery.GetInt64(row, "AllocatedBaseSize") * Mb;
            var current = WmiQuery.GetInt64(row, "CurrentUsage") * Mb;
            var peak = WmiQuery.GetInt64(row, "PeakUsage") * Mb;
            if (allocated is long a) { pfAllocated += a; pfAny = true; }
            if (current is long c) { pfCurrent += c; pfAny = true; }
            if (peak is long p) { pfPeak += p; pfAny = true; }
        }

        return new MemorySnapshot
        {
            TotalPhysicalBytes = osTotal,
            AvailableBytes = perfAvailable ?? osFree,
            CommittedBytes = perfCommitted,
            CommitLimitBytes = perfCommitLimit,
            PagesPerSecond = perfPages,
            CacheBytes = perfCache,
            PoolNonpagedBytes = perfNonpaged,
            PoolPagedBytes = perfPaged,
            PagefileAllocatedBytes = pfAny ? pfAllocated : null,
            PagefileCurrentBytes = pfAny ? pfCurrent : null,
            PagefilePeakBytes = pfAny ? pfPeak : null,
            OperatingSystemInfoAvailable = os is not null && osTotal is not null,
            PerfCountersAvailable = perf is not null && perfCommitted is not null,
            PagefileUsageAvailable = pfAny
        };
    }
}