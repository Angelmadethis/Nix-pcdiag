using PCDiag.Inventory;

namespace PCDiag.Tests.Inventory;

public class WindowsInfoProviderTests
{
    [Fact]
    public void Collect_ShouldNeverThrow()
    {
        var info = WindowsInfoProvider.Collect();

        Assert.NotNull(info);
    }

    [Fact]
    public void Collect_Uptime_ShouldBePositive()
    {
        var info = WindowsInfoProvider.Collect();

        Assert.True(info.Uptime > TimeSpan.Zero);
        Assert.True(info.BootTime < DateTime.UtcNow);
    }

    [Theory]
    [InlineData("Microsoft Windows 11 Pro", "Pro")]
    [InlineData("Microsoft Windows 11 Home", "Home")]
    [InlineData("Microsoft Windows 10 Enterprise", "Enterprise")]
    [InlineData("Microsoft Windows 11 Education", "Education")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("Microsoft Windows 11", null)]
    public void GetEdition_ShouldExtractKnownEditions(string? caption, string? expected)
    {
        var edition = WindowsInfoProvider.GetEdition(caption);

        Assert.Equal(expected, edition);
    }
}