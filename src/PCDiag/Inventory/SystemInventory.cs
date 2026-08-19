namespace PCDiag.Inventory;

/// <summary>
/// Aggregate of all system inventory sections gathered by the inventory providers.
/// All sections are always present; individual fields may be null or empty when
/// the information could not be obtained (missing, virtual machine, permissions).
/// </summary>
public sealed record SystemInventory
{
    public OsInfo System { get; init; } = new();
    public HardwareInfo Hardware { get; init; } = new();
    public NetworkInfo Network { get; init; } = new();
    public WindowsInfo Windows { get; init; } = new();
}

/// <summary>Basic operating-system-level information.</summary>
public sealed record OsInfo
{
    public string MachineName { get; init; } = "";
    public string OSVersionString { get; init; } = "";
    public int WindowsBuild { get; init; }
    public int Ubr { get; init; }
    public string Architecture { get; init; } = "";
    public bool Is64Bit { get; init; }
    public bool? IsVirtualMachine { get; init; }
}

/// <summary>Physical hardware inventory (CPU, RAM, GPUs, storage, motherboard, BIOS).</summary>
public sealed record HardwareInfo
{
    public string? CpuModel { get; init; }
    public int? LogicalProcessors { get; init; }
    public int? PhysicalCores { get; init; }
    public long? MaxClockSpeedMHz { get; init; }
    public long RamBytes { get; init; }
    public IReadOnlyList<GpuInfo> Gpus { get; init; } = Array.Empty<GpuInfo>();
    public IReadOnlyList<StorageDeviceInfo> StorageDevices { get; init; } = Array.Empty<StorageDeviceInfo>();
    public MotherboardInfo? Motherboard { get; init; }
    public BiosInfo? Bios { get; init; }
}

/// <summary>A single GPU / video controller.</summary>
public sealed record GpuInfo
{
    public string Name { get; init; } = "";
    public string? DriverVersion { get; init; }
    public long? VideoMemoryBytes { get; init; }
    public string? VideoProcessor { get; init; }
}

/// <summary>A single storage device.</summary>
public sealed record StorageDeviceInfo
{
    public string Model { get; init; } = "";
    public string? InterfaceType { get; init; }
    public long? SizeBytes { get; init; }
    public string? MediaType { get; init; }
    public string? SerialNumber { get; init; }
}

/// <summary>Motherboard / base board information.</summary>
public sealed record MotherboardInfo
{
    public string? Manufacturer { get; init; }
    public string? Product { get; init; }
    public string? Version { get; init; }
}

/// <summary>BIOS / UEFI firmware information.</summary>
public sealed record BiosInfo
{
    public string? Manufacturer { get; init; }
    public string? Version { get; init; }
    public DateTime? ReleaseDate { get; init; }
}

/// <summary>Network adapters and the active network connection.</summary>
public sealed record NetworkInfo
{
    public IReadOnlyList<NetworkAdapterInfo> Adapters { get; init; } = Array.Empty<NetworkAdapterInfo>();
    public NetworkAdapterInfo? ActiveConnection { get; init; }
}

/// <summary>A single network adapter.</summary>
public sealed record NetworkAdapterInfo
{
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public string? Type { get; init; }
    public long? SpeedBps { get; init; }
    public string? MacAddress { get; init; }
    public string OperationalStatus { get; init; } = "";
    public IReadOnlyList<string> IpAddresses { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> GatewayAddresses { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DnsAddresses { get; init; } = Array.Empty<string>();
    public bool IsActive { get; init; }
}

/// <summary>Windows-specific information (product, edition, uptime, boot time).</summary>
public sealed record WindowsInfo
{
    public string? ProductName { get; init; }
    public string? Edition { get; init; }
    public DateTime? InstallDate { get; init; }
    public TimeSpan? Uptime { get; init; }
    public DateTime? BootTime { get; init; }
}