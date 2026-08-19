using PCDiag.Inventory;

namespace PCDiag.Tests.Inventory;

public class NetworkAdapterProviderTests
{
    [Fact]
    public void Collect_ShouldNeverThrow()
    {
        var info = NetworkAdapterProvider.Collect();

        Assert.NotNull(info);
    }

    [Fact]
    public void Collect_Adapters_ShouldNeverBeNull()
    {
        var info = NetworkAdapterProvider.Collect();

        Assert.NotNull(info.Adapters);
    }

    [Fact]
    public void Collect_EveryAdapter_ShouldHaveNameAndStatus()
    {
        var info = NetworkAdapterProvider.Collect();

        Assert.All(info.Adapters, a =>
        {
            Assert.NotEmpty(a.Name);
            Assert.NotEmpty(a.OperationalStatus);
        });
    }

    [Fact]
    public void Collect_ActiveConnection_ShouldBeActiveAdapter()
    {
        var info = NetworkAdapterProvider.Collect();

        if (info.ActiveConnection is not null)
        {
            Assert.True(info.ActiveConnection.IsActive);
            Assert.Contains(info.ActiveConnection, info.Adapters);
        }
    }
}