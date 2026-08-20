using System.Diagnostics;
using PCDiag.Infrastructure;

namespace PCDiag.Storage;

/// <summary>
/// Reads storage state from WMI: volumes (Win32_LogicalDisk), physical disks
/// (Win32_DiskDrive), storage-stack health (MSFT_PhysicalDisk) and SMART/NVMe
/// reliability counters (MSFT_StorageReliabilityCounter, when present), plus a short
/// passive latency sample from Win32_PerfRawData_PerfDisk_PhysicalDisk. Latency is
/// derived from raw-counter deltas over a brief window - never a load test, and the
/// drive is never benchmarked.
/// </summary>
public sealed class WmiStorageInfoSource : IStorageInfoSource
{
    private const string StorageNamespace = @"root\microsoft\windows\storage";
    private readonly TimeSpan _sampleInterval;

    public WmiStorageInfoSource(TimeSpan? sampleInterval = null)
        => _sampleInterval = sampleInterval ?? TimeSpan.FromMilliseconds(700);

    public async Task<StorageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        var first = QueryPerfDisk();
        await Task.Delay(_sampleInterval, cancellationToken).ConfigureAwait(false);
        var second = QueryPerfDisk();
        timer.Stop();
        cancellationToken.ThrowIfCancellationRequested();

        var volumes = ReadVolumes();
        var disks = ReadDisks();

        return new StorageSnapshot
        {
            Volumes = volumes.Rows,
            VolumesAvailable = volumes.Available,
            Disks = disks,
            DisksAvailable = disks.Count > 0,
            StorageNamespaceAvailable = StorageNamespaceQueried,
            Latency = BuildLatency(first, second, timer.Elapsed.TotalSeconds)
        };
    }

    private bool StorageNamespaceQueried { get; set; }

    private static (IReadOnlyList<StorageVolume> Rows, bool Available) ReadVolumes()
    {
        var rows = WmiQuery.Query("SELECT DeviceID, VolumeName, Size, FreeSpace, FileSystem, VolumeDirty, DriveType FROM Win32_LogicalDisk");
        var volumes = new List<StorageVolume>();

        foreach (var row in rows)
        {
            var driveType = WmiQuery.GetInt32(row, "DriveType");
            if (driveType != 3)
                continue;

            var size = WmiQuery.GetInt64(row, "Size");
            var free = WmiQuery.GetInt64(row, "FreeSpace");
            double? fraction = size is long s && free is long f && s > 0 ? (double)f / s : null;

            volumes.Add(new StorageVolume
            {
                DeviceId = WmiQuery.GetString(row, "DeviceID") ?? "?",
                VolumeName = WmiQuery.GetString(row, "VolumeName"),
                SizeBytes = size,
                FreeBytes = free,
                FileSystem = WmiQuery.GetString(row, "FileSystem"),
                IsDirty = WmiQuery.GetBool(row, "VolumeDirty"),
                FreeFraction = fraction
            });
        }

        return (volumes, rows.Count > 0);
    }

    private List<PhysicalDiskInfo> ReadDisks()
    {
        var drives = WmiQuery.Query("SELECT Index, Model, Size, InterfaceType, MediaType FROM Win32_DiskDrive");
        var disks = new List<PhysicalDiskInfo>();

        var stack = QueryStack();
        foreach (var drive in drives)
        {
            var index = WmiQuery.GetInt32(drive, "Index");
            var model = WmiQuery.GetString(drive, "Model") ?? "(unknown model)";
            var size = WmiQuery.GetInt64(drive, "Size");

            disks.Add(new PhysicalDiskInfo
            {
                Model = model,
                SizeBytes = size,
                InterfaceType = WmiQuery.GetString(drive, "InterfaceType"),
                MediaTypeLabel = stack.MediaTypeLabel(index),
                Health = stack.HealthFor(index, model, size)
            });
        }

        return disks;
    }

    private StackData QueryStack()
    {
        var physical = WmiQuery.Query(
            "SELECT DeviceId, FriendlyName, Model, MediaType, HealthStatus, Size FROM MSFT_PhysicalDisk",
            StorageNamespace);
        var reliability = WmiQuery.Query(
            "SELECT DeviceId, Temperature, Wear, ReadErrorsCorrected, ReadErrorsUncorrected, WriteErrorsCorrected, WriteErrorsUncorrected FROM MSFT_StorageReliabilityCounter",
            StorageNamespace);

        StorageNamespaceQueried = physical.Count > 0;

        return new StackData(physical, reliability, StorageNamespaceQueried);
    }

    private sealed class StackData
    {
        private readonly IReadOnlyList<ManagementObjectProxy> _physical;
        private readonly IReadOnlyList<ManagementObjectProxy> _reliability;
        private readonly bool _namespaceQueried;

        public StackData(
            IReadOnlyList<System.Management.ManagementBaseObject> physical,
            IReadOnlyList<System.Management.ManagementBaseObject> reliability,
            bool namespaceQueried)
        {
            _physical = physical.Select(p => new ManagementObjectProxy(p)).ToList();
            _reliability = reliability.Select(r => new ManagementObjectProxy(r)).ToList();
            _namespaceQueried = namespaceQueried;
        }

        public string? MediaTypeLabel(int? index)
        {
            var match = FindPhysical(index, model: null, size: null);
            return match is null ? null : StorageHealthStatusMapper.MediaType(match.GetInt32("MediaType"));
        }

        public StorageHealth HealthFor(int? index, string model, long? size)
        {
            var physical = FindPhysical(index, model, size);
            if (physical is null)
                return new StorageHealth { StackQueried = _namespaceQueried };

            var reliability = _reliability.FirstOrDefault(r => MatchesDeviceId(r, index));
            var hasReliability = reliability is not null;

            return new StorageHealth
            {
                StackState = StorageHealthStatusMapper.Health(physical.GetInt32("HealthStatus")),
                StackQueried = true,
                HasReliabilityCounters = hasReliability,
                WearPercent = reliability?.GetInt32("Wear"),
                TemperatureCelsius = reliability?.GetInt32("Temperature"),
                ReadErrorsUncorrected = reliability?.GetInt64("ReadErrorsUncorrected"),
                WriteErrorsUncorrected = reliability?.GetInt64("WriteErrorsUncorrected"),
                ReadErrorsCorrected = reliability?.GetInt64("ReadErrorsCorrected"),
                WriteErrorsCorrected = reliability?.GetInt64("WriteErrorsCorrected")
            };
        }

        private ManagementObjectProxy? FindPhysical(int? index, string? model, long? size)
        {
            var byDeviceId = _physical.FirstOrDefault(p => MatchesDeviceId(p, index));
            if (byDeviceId is not null)
                return byDeviceId;

            if (model is null)
                return null;

            return _physical.FirstOrDefault(p =>
                string.Equals(p.GetString("Model"), model, StringComparison.OrdinalIgnoreCase)
                && (size is null || p.GetInt64("Size") == size));
        }

        private static bool MatchesDeviceId(ManagementObjectProxy obj, int? index)
        {
            if (index is not int i)
                return false;

            // MSFT_* returns DeviceId as a string ("0"); Win32 returns Index as UInt32.
            return obj.GetString("DeviceId") == i.ToString()
                   || obj.GetInt32("DeviceId") == i;
        }
    }

    /// <summary>Thin null-safe facade over ManagementBaseObject so stack reading stays tidy.</summary>
    private sealed class ManagementObjectProxy
    {
        private readonly System.Management.ManagementBaseObject _obj;
        public ManagementObjectProxy(System.Management.ManagementBaseObject obj) => _obj = obj;
        public int? GetInt32(string name) => WmiQuery.GetInt32(_obj, name);
        public long? GetInt64(string name) => WmiQuery.GetInt64(_obj, name);
        public string? GetString(string name) => WmiQuery.GetString(_obj, name);
    }

    private static IReadOnlyList<PerfDiskRow> QueryPerfDisk()
    {
        var rows = WmiQuery.Query(
            "SELECT Name, AvgDisksecPerRead, AvgDisksecPerRead_Base, AvgDisksecPerWrite, AvgDisksecPerWrite_Base, DiskReadsPersec, DiskWritesPersec FROM Win32_PerfRawData_PerfDisk_PhysicalDisk");
        return rows
            .Select(r => new PerfDiskRow
            {
                Name = WmiQuery.GetString(r, "Name"),
                AvgRead = WmiQuery.GetInt64(r, "AvgDisksecPerRead"),
                AvgReadBase = WmiQuery.GetInt64(r, "AvgDisksecPerRead_Base"),
                AvgWrite = WmiQuery.GetInt64(r, "AvgDisksecPerWrite"),
                AvgWriteBase = WmiQuery.GetInt64(r, "AvgDisksecPerWrite_Base"),
                Reads = WmiQuery.GetInt64(r, "DiskReadsPersec"),
                Writes = WmiQuery.GetInt64(r, "DiskWritesPersec")
            })
            .ToList();
    }

    private static IReadOnlyList<DiskLatencySample> BuildLatency(
        IReadOnlyList<PerfDiskRow> first,
        IReadOnlyList<PerfDiskRow> second,
        double elapsedSeconds)
    {
        var samples = new List<DiskLatencySample>();

        foreach (var row2 in second)
        {
            var row1 = first.FirstOrDefault(f => string.Equals(f.Name, row2.Name, StringComparison.OrdinalIgnoreCase));
            if (row1 is null)
                continue;

            var name = row2.Name ?? "_Total";
            var deltaReads = Delta(row1.Reads, row2.Reads);
            var deltaWrites = Delta(row1.Writes, row2.Writes);
            var deltaReadBase = Delta(row1.AvgReadBase, row2.AvgReadBase);
            var deltaWriteBase = Delta(row1.AvgWriteBase, row2.AvgWriteBase);
            var deltaReadTime = Delta(row1.AvgRead, row2.AvgRead);
            var deltaWriteTime = Delta(row1.AvgWrite, row2.AvgWrite);

            double? readsPerSec = deltaReads is long dr && elapsedSeconds > 0 ? dr / elapsedSeconds : null;
            double? writesPerSec = deltaWrites is long dw && elapsedSeconds > 0 ? dw / elapsedSeconds : null;
            double? avgReadSec = deltaReadBase is long rbase && rbase > 0 && deltaReadTime is long rtime
                ? rtime / (double)rbase * 1e-7
                : null;
            double? avgWriteSec = deltaWriteBase is long wbase && wbase > 0 && deltaWriteTime is long wtime
                ? wtime / (double)wbase * 1e-7
                : null;

            var hadActivity = (deltaReadBase ?? 0) + (deltaWriteBase ?? 0) > 0;

            samples.Add(new DiskLatencySample
            {
                Instance = name,
                AverageReadSeconds = avgReadSec,
                AverageWriteSeconds = avgWriteSec,
                ReadsPerSecond = readsPerSec,
                WritesPerSecond = writesPerSec,
                HadIoActivity = hadActivity
            });
        }

        return samples;
    }

    private static long? Delta(long? a, long? b)
        => a is long x && b is long y && y >= x ? y - x : null;

    private sealed record PerfDiskRow
    {
        public string? Name { get; init; }
        public long? AvgRead { get; init; }
        public long? AvgReadBase { get; init; }
        public long? AvgWrite { get; init; }
        public long? AvgWriteBase { get; init; }
        public long? Reads { get; init; }
        public long? Writes { get; init; }
    }
}

/// <summary>Maps MSFT_* raw numeric enums to friendly labels and health states.</summary>
public static class StorageHealthStatusMapper
{
    /// <summary>MSFT_StorageHealthStatus: 0 Healthy, 1 Warning, 2 Unhealthy, 3 Failing, else Unknown.</summary>
    public static StorageHealthState Health(int? status)
        => status switch
        {
            0 => StorageHealthState.Healthy,
            1 => StorageHealthState.Warning,
            2 or 3 => StorageHealthState.Unhealthy,
            _ => StorageHealthState.Unknown
        };

    /// <summary>MSFT_MediaType: 0 Unspecified, 1 HDD, 2 SSD, 3 SCM, else Unknown.</summary>
    public static string MediaType(int? type)
        => type switch
        {
            1 => "HDD",
            2 => "SSD",
            3 => "SCM",
            _ => "Unknown"
        };
}