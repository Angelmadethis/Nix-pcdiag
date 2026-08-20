using PCDiag.Inventory;
using PCDiag.Net.Tcp;

namespace PCDiag.Tests.Net.Tcp;

internal sealed class FakeTcpConnectionSource : ITcpConnectionSource
{
    public List<TcpConnectionRecord> Connections { get; } = new();

    public IReadOnlyList<TcpConnectionRecord> GetConnections() => Connections;
}

internal sealed class FakeTcpStatsSource : ITcpStatsSource
{
    public TcpCumulativeStats Stats { get; set; } = new();

    public TcpCumulativeStats GetStats() => Stats;
}

internal sealed class FakeTcpConfigSource : ITcpConfigSource
{
    public TcpConfiguration Config { get; set; } = new()
    {
        AutotuningLevel = TcpAutotuningLevel.Normal,
        DynamicPortStart = 49152,
        DynamicPortCount = 16384
    };

    public TcpConfiguration GetConfig() => Config;
}

internal sealed class FakeAdapterErrorSource : ITcpAdapterErrorSource
{
    public TcpAdapterErrorStats? Result { get; set; }
    public string? CalledName { get; private set; }
    public string? CalledDescription { get; private set; }

    public TcpAdapterErrorStats? GetFor(string? adapterName, string? adapterDescription)
    {
        CalledName = adapterName;
        CalledDescription = adapterDescription;
        return Result;
    }
}

internal static class TcpConn
{
    public static TcpConnectionRecord Listen(int port = 80, int pid = 4)
        => new(TcpConnectionState.Listen, "0.0.0.0", port, "0.0.0.0", 0, pid);

    public static TcpConnectionRecord Established(int port, int pid, int remotePort = 443)
        => new(TcpConnectionState.Established, "192.168.1.50", port, "203.0.113.10", remotePort, pid);

    public static TcpConnectionRecord TimeWait(int port, int pid = 0)
        => new(TcpConnectionState.TimeWait, "192.168.1.50", port, "203.0.113.10", 443, pid);

    public static TcpConnectionRecord CloseWait(int port, int pid)
        => new(TcpConnectionState.CloseWait, "192.168.1.50", port, "203.0.113.10", 443, pid);

    public static TcpConnectionRecord Bound(int port, int pid = 0)
        => new(TcpConnectionState.Bound, "192.168.1.50", port, "0.0.0.0", 0, pid);
}

/// <summary>Builds inventory with an active adapter and an optional uptime.</summary>
internal static class TcpInventory
{
    public const string AdapterName = "Wi-Fi";

    public static SystemInventory WithActiveAdapter(TimeSpan? uptime = null)
    {
        var adapter = new NetworkAdapterInfo
        {
            Name = AdapterName,
            Description = "Intel(R) Wi-Fi 6 AX201 160MHz",
            IpAddresses = new[] { "192.168.1.50" },
            IsActive = true,
            OperationalStatus = "Up"
        };

        return new SystemInventory
        {
            Network = new NetworkInfo { Adapters = new[] { adapter }, ActiveConnection = adapter },
            Windows = new WindowsInfo { Uptime = uptime }
        };
    }
}