namespace PCDiag.Inventory;

/// <summary>
/// Orchestrates all inventory providers and returns a single aggregate
/// <see cref="SystemInventory"/>. Read-only; never modifies the system.
/// </summary>
public static class SystemInventoryCollector
{
    public static SystemInventory Collect()
    {
        return new SystemInventory
        {
            System = SystemInfoProvider.Collect(),
            Hardware = HardwareInfoProvider.Collect(),
            Network = NetworkAdapterProvider.Collect(),
            Windows = WindowsInfoProvider.Collect()
        };
    }
}