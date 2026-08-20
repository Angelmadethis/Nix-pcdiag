using PCDiag.Memory;

namespace PCDiag.Tests.Memory;

internal sealed class FakeMemorySnapshotSource : IMemorySnapshotSource
{
    public MemorySnapshot Snapshot { get; set; } = new()
    {
        OperatingSystemInfoAvailable = true,
        PerfCountersAvailable = true,
        PagefileUsageAvailable = true
    };

    public MemorySnapshot GetSnapshot() => Snapshot;
}

internal sealed class FakePagefileSource : IPagefileSource
{
    public PagefileInfo Info { get; set; } = new();

    public PagefileInfo GetInfo() => Info;
}

internal static class Mem
{
    private const long Mb = 1024L * 1024;
    private const long Gb = 1024L * Mb;

    /// <summary>Snapshot with 16 GB installed and all sources available.</summary>
    public static MemorySnapshot Snapshot(
        double totalGb = 16,
        double? availableGb = 10,
        double? committedGb = 8,
        double? commitLimitGb = 20,
        double? pagesPerSec = 0,
        double? pagefileCurrentMb = 100,
        double? pagefileAllocatedMb = 1024,
        bool osAvailable = true,
        bool perfAvailable = true,
        bool pagefileAvailable = true)
        => new()
        {
            TotalPhysicalBytes = (long)(totalGb * Gb),
            AvailableBytes = availableGb is double a ? (long)(a * Gb) : null,
            CommittedBytes = committedGb is double c ? (long)(c * Gb) : null,
            CommitLimitBytes = commitLimitGb is double l ? (long)(l * Gb) : null,
            PagesPerSecond = (long?)pagesPerSec,
            PagefileCurrentBytes = (long?)(pagefileCurrentMb * Mb),
            PagefileAllocatedBytes = (long?)(pagefileAllocatedMb * Mb),
            PagefilePeakBytes = (long?)(pagefileCurrentMb * Mb),
            OperatingSystemInfoAvailable = osAvailable,
            PerfCountersAvailable = perfAvailable,
            PagefileUsageAvailable = pagefileAvailable
        };

    public static PagefileInfo Pagefile(
        PagefileConfig? config = null,
        IReadOnlyList<PagefileEntry>? usage = null,
        bool usageAvailable = true,
        double? physicalGb = 16)
        => new()
        {
            Config = config,
            Usage = usage ?? Array.Empty<PagefileEntry>(),
            TotalAllocatedBytes = usage?.Sum(e => e.AllocatedBytes ?? 0),
            TotalCurrentBytes = usage?.Sum(e => e.CurrentBytes ?? 0),
            TotalPeakBytes = usage?.Sum(e => e.PeakBytes ?? 0),
            PhysicalBytes = physicalGb is double p ? (long)(p * Gb) : null,
            UsageAvailable = usageAvailable
        };

    public static PagefileEntry Entry(long allocatedMb, double currentMb, double? peakMb = null)
        => new()
        {
            Location = "C:\\pagefile.sys",
            AllocatedBytes = allocatedMb * Mb,
            CurrentBytes = (long)(currentMb * Mb),
            PeakBytes = (long?)((peakMb ?? currentMb) * Mb)
        };

    public static PagefileConfig SystemManaged()
        => new() { IsSystemManaged = true, Entries = new[] { @"?:\pagefile.sys" }, Available = true };

    public static PagefileConfig Custom(params string[] entries)
        => new() { IsSystemManaged = false, Entries = entries, Available = true };

    public static PagefileConfig None()
        => new() { IsSystemManaged = false, Entries = Array.Empty<string>(), Available = true };
}