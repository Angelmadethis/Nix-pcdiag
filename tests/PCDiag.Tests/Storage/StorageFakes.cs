using PCDiag.Storage;

namespace PCDiag.Tests.Storage;

internal sealed class FakeStorageInfoSource : IStorageInfoSource
{
    public StorageSnapshot Snapshot { get; set; } = new();

    public Task<StorageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
        => Task.FromResult(Snapshot);
}

internal static class Sto
{
    private const long Mb = 1024L * 1024;
    private const long Gb = 1024L * Mb;

    public static StorageVolume Volume(string deviceId, double freeFraction, double sizeGb = 100, bool? dirty = null)
        => new()
        {
            DeviceId = deviceId,
            SizeBytes = (long)(sizeGb * Gb),
            FreeBytes = (long)(freeFraction * sizeGb * Gb),
            FreeFraction = freeFraction,
            FileSystem = "NTFS",
            IsDirty = dirty
        };

    public static PhysicalDiskInfo Disk(StorageHealth? health = null)
        => new()
        {
            Model = "TestDisk NVMe 256GB",
            SizeBytes = 256L * Gb,
            InterfaceType = "NVMe",
            MediaTypeLabel = "SSD",
            Health = health ?? HealthyHealth()
        };

    public static StorageHealth HealthyHealth()
        => new()
        {
            StackQueried = true,
            StackState = StorageHealthState.Healthy,
            HasReliabilityCounters = true,
            WearPercent = 20,
            TemperatureCelsius = 40,
            ReadErrorsUncorrected = 0,
            WriteErrorsUncorrected = 0
        };

    public static StorageHealth UnknownNoReliability()
        => new()
        {
            StackQueried = true,
            StackState = StorageHealthState.Unknown,
            HasReliabilityCounters = false
        };

    public static DiskLatencySample Latency(string instance, double? readSeconds = null, double? writeSeconds = null, bool active = false)
        => new()
        {
            Instance = instance,
            AverageReadSeconds = readSeconds,
            AverageWriteSeconds = writeSeconds,
            ReadsPerSecond = active ? 1 : 0,
            WritesPerSecond = active ? 1 : 0,
            HadIoActivity = active
        };

    public static StorageSnapshot Snapshot(
        StorageVolume[]? volumes = null,
        PhysicalDiskInfo[]? disks = null,
        DiskLatencySample[]? latency = null,
        bool volumesAvailable = true,
        bool disksAvailable = true,
        bool namespaceAvailable = true)
        => new()
        {
            Volumes = volumes ?? Array.Empty<StorageVolume>(),
            Disks = disks ?? Array.Empty<PhysicalDiskInfo>(),
            Latency = latency ?? Array.Empty<DiskLatencySample>(),
            VolumesAvailable = volumesAvailable,
            DisksAvailable = disksAvailable,
            StorageNamespaceAvailable = namespaceAvailable
        };
}