using System.IO;
using System.Text;
using PCDiag.Inventory;
using PCDiag.Reporting;

namespace PCDiag.Tests.Inventory;

public class InventoryRendererTests
{
    private static string Render(SystemInventory inventory)
    {
        var sb = new StringBuilder();
        using (var writer = new StringWriter(sb))
        {
            new InventoryRenderer(writer).Print(inventory);
        }

        return sb.ToString();
    }

    private static SystemInventory Stub() => new();

    [Fact]
    public void Print_ShouldEmitHeader()
    {
        var text = Render(Stub());

        Assert.Contains("PCDIAG SYSTEM INFORMATION", text);
    }

    [Fact]
    public void Print_EmptyInventory_ShouldShowUnavailablePlaceholder()
    {
        var text = Render(Stub());

        Assert.Contains("(unavailable)", text);
    }

    [Fact]
    public void Print_PopulatedInventory_ShouldShowValues()
    {
        var inventory = new SystemInventory
        {
            System = new OsInfo
            {
                MachineName = "TEST-PC",
                OSVersionString = "Windows 11 Pro",
                WindowsBuild = 22631,
                Ubr = 4541,
                Architecture = "X64",
                Is64Bit = true,
                IsVirtualMachine = false
            },
            Hardware = new HardwareInfo
            {
                CpuModel = "Intel(R) Core(TM) i7-11700",
                LogicalProcessors = 16,
                PhysicalCores = 8,
                MaxClockSpeedMHz = 4800,
                RamBytes = 34_877_243_392
            },
            Windows = new WindowsInfo
            {
                ProductName = "Microsoft Windows 11 Pro",
                Edition = "Pro",
                Uptime = new TimeSpan(1, 2, 3, 4)
            }
        };

        var text = Render(inventory);

        Assert.Contains("TEST-PC", text);
        Assert.Contains("Windows 11 Pro", text);
        Assert.Contains("22631.4541", text);
        Assert.Contains("Intel(R) Core(TM) i7-11700", text);
        Assert.Contains("16", text);
        Assert.Contains("1d 2h", text);
    }

    [Fact]
    public void Print_MultipleGpus_ShouldNumberThem()
    {
        var inventory = new SystemInventory
        {
            Hardware = new HardwareInfo
            {
                Gpus = new[]
                {
                    new GpuInfo { Name = "NVIDIA RTX 3080", DriverVersion = "31.0.15" },
                    new GpuInfo { Name = "Intel UHD 630", DriverVersion = "27.20" }
                }
            }
        };

        var text = Render(inventory);

        Assert.Contains("GPU [1]", text);
        Assert.Contains("GPU [2]", text);
        Assert.Contains("NVIDIA RTX 3080", text);
        Assert.Contains("Intel UHD 630", text);
    }

    [Fact]
    public void Print_MultipleAdapters_ShouldListEach()
    {
        var inventory = new SystemInventory
        {
            Network = new NetworkInfo
            {
                Adapters = new[]
                {
                    new NetworkAdapterInfo { Name = "Ethernet", OperationalStatus = "Up" },
                    new NetworkAdapterInfo { Name = "Wi-Fi", OperationalStatus = "Down" }
                }
            }
        };

        var text = Render(inventory);

        Assert.Contains("Ethernet", text);
        Assert.Contains("Wi-Fi", text);
        Assert.Contains("Up", text);
    }

    [Fact]
    public void Print_ActiveConnection_ShouldShowIt()
    {
        var inventory = new SystemInventory
        {
            Network = new NetworkInfo
            {
                ActiveConnection = new NetworkAdapterInfo
                {
                    Name = "Ethernet",
                    IpAddresses = new[] { "192.168.1.10" }
                }
            }
        };

        var text = Render(inventory);

        Assert.Contains("Active Connection", text);
        Assert.Contains("Ethernet", text);
        Assert.Contains("192.168.1.10", text);
    }
}