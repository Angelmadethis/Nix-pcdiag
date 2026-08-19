using PCDiag.Infrastructure;

namespace PCDiag.Inventory;

/// <summary>
/// Collects physical hardware inventory (CPU, RAM, GPUs, storage, motherboard, BIOS)
/// via WMI/CIM. Every query degrades gracefully on missing data, virtual machines,
/// or permission limitations.
/// </summary>
public static class HardwareInfoProvider
{
    public static HardwareInfo Collect()
    {
        return new HardwareInfo
        {
            CpuModel = GetCpuModel(),
            LogicalProcessors = GetLogicalProcessors(),
            PhysicalCores = GetPhysicalCores(),
            MaxClockSpeedMHz = GetMaxClockSpeedMHz(),
            RamBytes = GetRamBytes(),
            Gpus = GetGpus(),
            StorageDevices = GetStorageDevices(),
            Motherboard = GetMotherboard(),
            Bios = GetBios()
        };
    }

    private static string? GetCpuModel()
    {
        var row = WmiQuery.Query("SELECT Name FROM Win32_Processor").FirstOrDefault();
        return row is null ? null : WmiQuery.GetString(row, "Name");
    }

    private static int? GetLogicalProcessors()
        => Safe(() => Environment.ProcessorCount);

    private static int? GetPhysicalCores()
    {
        var row = WmiQuery.Query("SELECT NumberOfCores FROM Win32_Processor").FirstOrDefault();
        return row is null ? null : WmiQuery.GetInt32(row, "NumberOfCores");
    }

    private static long? GetMaxClockSpeedMHz()
    {
        var row = WmiQuery.Query("SELECT MaxClockSpeed FROM Win32_Processor").FirstOrDefault();
        return row is null ? null : WmiQuery.GetInt32(row, "MaxClockSpeed");
    }

    private static long GetRamBytes()
    {
        var row = WmiQuery.Query("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem").FirstOrDefault();
        return row is null ? 0 : WmiQuery.GetInt64(row, "TotalPhysicalMemory") ?? 0;
    }

    private static IReadOnlyList<GpuInfo> GetGpus()
    {
        var gpus = new List<GpuInfo>();
        foreach (var row in WmiQuery.Query(
                     "SELECT Name, DriverVersion, AdapterRAM, VideoProcessor FROM Win32_VideoController"))
        {
            var name = WmiQuery.GetString(row, "Name");
            if (string.IsNullOrEmpty(name))
                continue;

            gpus.Add(new GpuInfo
            {
                Name = name,
                DriverVersion = WmiQuery.GetString(row, "DriverVersion"),
                VideoMemoryBytes = WmiQuery.GetInt64(row, "AdapterRAM"),
                VideoProcessor = WmiQuery.GetString(row, "VideoProcessor")
            });
        }
        return gpus;
    }

    private static IReadOnlyList<StorageDeviceInfo> GetStorageDevices()
    {
        var devices = new List<StorageDeviceInfo>();
        foreach (var row in WmiQuery.Query(
                     "SELECT Model, InterfaceType, Size, MediaType, SerialNumber FROM Win32_DiskDrive"))
        {
            var model = WmiQuery.GetString(row, "Model");
            if (string.IsNullOrEmpty(model))
                continue;

            devices.Add(new StorageDeviceInfo
            {
                Model = model,
                InterfaceType = WmiQuery.GetString(row, "InterfaceType"),
                SizeBytes = WmiQuery.GetInt64(row, "Size"),
                MediaType = WmiQuery.GetString(row, "MediaType"),
                SerialNumber = WmiQuery.GetString(row, "SerialNumber")
            });
        }
        return devices;
    }

    private static MotherboardInfo? GetMotherboard()
    {
        var row = WmiQuery.Query("SELECT Manufacturer, Product, Version FROM Win32_BaseBoard").FirstOrDefault();
        if (row is null)
            return null;

        return new MotherboardInfo
        {
            Manufacturer = WmiQuery.GetString(row, "Manufacturer"),
            Product = WmiQuery.GetString(row, "Product"),
            Version = WmiQuery.GetString(row, "Version")
        };
    }

    private static BiosInfo? GetBios()
    {
        var row = WmiQuery.Query("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS").FirstOrDefault();
        if (row is null)
            return null;

        return new BiosInfo
        {
            Manufacturer = WmiQuery.GetString(row, "Manufacturer"),
            Version = WmiQuery.GetString(row, "SMBIOSBIOSVersion"),
            ReleaseDate = WmiQuery.GetDateTime(row, "ReleaseDate")
        };
    }

    private static int? Safe(Func<int> action)
    {
        try
        {
            return action();
        }
        catch
        {
            return null;
        }
    }
}