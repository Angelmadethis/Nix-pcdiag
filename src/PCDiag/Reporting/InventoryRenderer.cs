using PCDiag.Core;
using PCDiag.Infrastructure;
using PCDiag.Inventory;
using SystemInfoModel = PCDiag.Inventory.OsInfo;

namespace PCDiag.Reporting;

/// <summary>
/// Renders a <see cref="SystemInventory"/> to a text writer in plain ASCII.
/// Unavailable information is shown as "(unavailable)".
/// </summary>
public sealed class InventoryRenderer
{
    private readonly TextWriter _output;

    public InventoryRenderer(TextWriter? output = null)
    {
        _output = output ?? Console.Out;
    }

    public void Print(SystemInventory inventory)
    {
        _output.WriteLine();
        WriteLine("PCDIAG SYSTEM INFORMATION", ConsoleColor.White);
        _output.WriteLine("================================");
        _output.WriteLine();

        PrintSystem(inventory.System);
        PrintHardware(inventory.Hardware);
        PrintNetwork(inventory.Network);
        PrintWindows(inventory.Windows);

        _output.WriteLine();
    }

    private void PrintSystem(SystemInfoModel system)
    {
        WriteLine("SYSTEM", ConsoleColor.White);
        PrintRow("Machine Name", system.MachineName);
        PrintRow("OS Version", system.OSVersionString);
        PrintRow("Build", system.WindowsBuild > 0 ? $"{system.WindowsBuild}.{system.Ubr}".TrimEnd('.') : null);
        PrintRow("Architecture", system.Architecture);
        PrintRow("64-bit", system.Is64Bit ? "Yes" : "No");
        PrintRow("Virtual Machine", system.IsVirtualMachine switch
        {
            true => "Yes",
            false => "No",
            null => null
        });
        _output.WriteLine();
    }

    private void PrintHardware(HardwareInfo hardware)
    {
        WriteLine("HARDWARE", ConsoleColor.White);
        PrintRow("CPU", hardware.CpuModel);
        PrintRow("Logical Processors", hardware.LogicalProcessors?.ToString());
        PrintRow("Physical Cores", hardware.PhysicalCores?.ToString());
        PrintRow("Max Clock", hardware.MaxClockSpeedMHz is long mhz ? $"{mhz} MHz" : null);
        PrintRow("RAM", hardware.RamBytes > 0 ? PCDiag.Infrastructure.SystemInfo.FormatBytes(hardware.RamBytes) : null);

        for (int i = 0; i < hardware.Gpus.Count; i++)
        {
            var gpu = hardware.Gpus[i];
            var suffix = hardware.Gpus.Count > 1 ? $" [{i + 1}]" : "";
            PrintRow($"GPU{suffix}", gpu.Name);
            if (!string.IsNullOrEmpty(gpu.DriverVersion))
                PrintRow("  Driver", gpu.DriverVersion);
        }
        if (hardware.Gpus.Count == 0)
            PrintRow("GPU", null);

        if (hardware.Motherboard is not null)
        {
            PrintRow("Motherboard",
                string.Join(" / ", new[] { hardware.Motherboard.Manufacturer, hardware.Motherboard.Product, hardware.Motherboard.Version }
                    .Where(v => !string.IsNullOrEmpty(v))));
        }
        else
        {
            PrintRow("Motherboard", null);
        }

        if (hardware.Bios is not null)
        {
            var bios = string.Join(" / ", new[] { hardware.Bios.Manufacturer, hardware.Bios.Version }
                .Where(v => !string.IsNullOrEmpty(v)));
            PrintRow("BIOS/UEFI", string.IsNullOrEmpty(bios) ? null : bios);
            if (hardware.Bios.ReleaseDate is DateTime release)
                PrintRow("  Released", release.ToLocalTime().ToString("yyyy-MM-dd"));
        }
        else
        {
            PrintRow("BIOS/UEFI", null);
        }

        for (int i = 0; i < hardware.StorageDevices.Count; i++)
        {
            var disk = hardware.StorageDevices[i];
            var label = hardware.StorageDevices.Count > 1 ? $"Storage [{i + 1}]" : "Storage";
            var details = new List<string> { disk.Model };
            if (!string.IsNullOrEmpty(disk.InterfaceType))
                details.Add(disk.InterfaceType);
            if (disk.SizeBytes is long size)
                details.Add(PCDiag.Infrastructure.SystemInfo.FormatBytes(size));
            PrintRow(label, string.Join(", ", details));
        }
        if (hardware.StorageDevices.Count == 0)
            PrintRow("Storage", null);

        _output.WriteLine();
    }

    private void PrintNetwork(NetworkInfo network)
    {
        WriteLine("NETWORK", ConsoleColor.White);

        if (network.ActiveConnection is not null)
        {
            PrintRow("Active Connection", network.ActiveConnection.Name);
            var ipv4 = network.ActiveConnection.IpAddresses
                .Where(ip => ip.Contains('.'))
                .ToList();
            if (ipv4.Count > 0)
                PrintRow("  IP Addresses", string.Join(", ", ipv4));
        }
        else
        {
            PrintRow("Active Connection", null);
        }

        PrintRow("Adapters", network.Adapters.Count.ToString());
        foreach (var adapter in network.Adapters)
        {
            var name = string.IsNullOrEmpty(adapter.Description) ? adapter.Name : $"{adapter.Name} ({adapter.Description})";
            PrintRow("  -", name);
            PrintRow("    Status", adapter.OperationalStatus);
            if (adapter.SpeedBps is long speed && speed > 0)
                PrintRow("    Speed", FormatSpeed(speed));
        }

        _output.WriteLine();
    }

    private void PrintWindows(WindowsInfo windows)
    {
        WriteLine("WINDOWS", ConsoleColor.White);
        PrintRow("Product", windows.ProductName);
        PrintRow("Edition", windows.Edition);
        PrintRow("Installed", windows.InstallDate is DateTime install ? install.ToLocalTime().ToString("yyyy-MM-dd") : null);
        PrintRow("Uptime", windows.Uptime is TimeSpan uptime ? FormatUptime(uptime) : null);
        PrintRow("Boot Time", windows.BootTime is DateTime boot ? boot.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : null);
        _output.WriteLine();
    }

    private static string FormatSpeed(long bps)
        => bps >= 1_000_000_000
            ? $"{bps / 1_000_000_000.0:F1} Gbps"
            : bps >= 1_000_000
                ? $"{bps / 1_000_000.0:F0} Mbps"
                : $"{bps / 1000.0:F0} Kbps";

    private static string FormatUptime(TimeSpan uptime)
    {
        var parts = new List<string>();
        if (uptime.Days > 0) parts.Add($"{uptime.Days}d");
        if (uptime.Hours > 0) parts.Add($"{uptime.Hours}h");
        if (uptime.Minutes > 0) parts.Add($"{uptime.Minutes}m");
        if (parts.Count == 0) parts.Add($"{uptime.Seconds}s");
        return string.Join(" ", parts);
    }

    private void PrintRow(string label, string? value)
    {
        var text = string.IsNullOrEmpty(value) ? "(unavailable)" : value;
        _output.Write($"  {label,-20} ");
        WriteLine(text, string.IsNullOrEmpty(value) ? ConsoleColor.DarkGray : ConsoleColor.Gray);
    }

    private void Write(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        _output.Write(text);
        Console.ResetColor();
    }

    private void WriteLine(string text, ConsoleColor color)
    {
        Write(text, color);
        _output.WriteLine();
    }
}