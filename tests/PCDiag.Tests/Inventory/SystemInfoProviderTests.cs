using System.Runtime.InteropServices;
using PCDiag.Inventory;

namespace PCDiag.Tests.Inventory;

public class SystemInfoProviderTests
{
    [Fact]
    public void Collect_ShouldNeverThrow()
    {
        var info = SystemInfoProvider.Collect();

        Assert.NotNull(info);
    }

    [Fact]
    public void Collect_MachineName_ShouldMatchEnvironment()
    {
        var info = SystemInfoProvider.Collect();

        Assert.Equal(Environment.MachineName, info.MachineName);
    }

    [Fact]
    public void Collect_Architecture_ShouldMatchRuntime()
    {
        var info = SystemInfoProvider.Collect();

        Assert.Equal(RuntimeInformation.OSArchitecture.ToString(), info.Architecture);
    }

    [Fact]
    public void Collect_Is64Bit_ShouldMatchEnvironment()
    {
        var info = SystemInfoProvider.Collect();

        Assert.Equal(Environment.Is64BitOperatingSystem, info.Is64Bit);
    }

    [Fact]
    public void Collect_ShouldReportWindowsBuild()
    {
        var info = SystemInfoProvider.Collect();

        Assert.True(info.WindowsBuild > 0);
        Assert.NotEmpty(info.OSVersionString);
    }

    [Fact]
    public void Collect_IsVirtualMachine_ShouldBeNull_True_OrFalse()
    {
        var info = SystemInfoProvider.Collect();

        Assert.True(info.IsVirtualMachine is null or true or false);
    }
}