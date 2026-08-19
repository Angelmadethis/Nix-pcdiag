using PCDiag.Inventory;

namespace PCDiag.Tests.Inventory;

public class SystemInventoryCollectorTests
{
    [Fact]
    public void Collect_ShouldNeverThrow()
    {
        var inventory = SystemInventoryCollector.Collect();

        Assert.NotNull(inventory);
    }

    [Fact]
    public void Collect_ShouldPopulateAllSections()
    {
        var inventory = SystemInventoryCollector.Collect();

        Assert.NotNull(inventory.System);
        Assert.NotNull(inventory.Hardware);
        Assert.NotNull(inventory.Network);
        Assert.NotNull(inventory.Windows);
    }

    [Fact]
    public void Collect_SystemSection_ShouldHaveBasics()
    {
        var inventory = SystemInventoryCollector.Collect();

        Assert.Equal(Environment.MachineName, inventory.System.MachineName);
        Assert.NotEmpty(inventory.System.OSVersionString);
    }
}