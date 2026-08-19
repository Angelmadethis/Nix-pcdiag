using PCDiag.Inventory;

namespace PCDiag.Tests.Inventory;

public class HardwareInfoProviderTests
{
    [Fact]
    public void Collect_ShouldNeverThrow()
    {
        var info = HardwareInfoProvider.Collect();

        Assert.NotNull(info);
    }

    [Fact]
    public void Collect_LogicalProcessors_ShouldMatchEnvironment()
    {
        var info = HardwareInfoProvider.Collect();

        Assert.Equal(Environment.ProcessorCount, info.LogicalProcessors);
    }

    [Fact]
    public void Collect_Collections_ShouldNeverBeNull()
    {
        var info = HardwareInfoProvider.Collect();

        Assert.NotNull(info.Gpus);
        Assert.NotNull(info.StorageDevices);
    }

    [Fact]
    public void Collect_Ram_ShouldBePositive_OrZero()
    {
        var info = HardwareInfoProvider.Collect();

        Assert.True(info.RamBytes >= 0);
    }

    [Fact]
    public void Collect_EveryGpu_ShouldHaveName()
    {
        var info = HardwareInfoProvider.Collect();

        Assert.All(info.Gpus, gpu => Assert.NotEmpty(gpu.Name));
    }

    [Fact]
    public void Collect_EveryStorageDevice_ShouldHaveModel()
    {
        var info = HardwareInfoProvider.Collect();

        Assert.All(info.StorageDevices, disk => Assert.NotEmpty(disk.Model));
    }
}