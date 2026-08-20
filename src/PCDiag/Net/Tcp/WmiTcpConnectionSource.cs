using PCDiag.Infrastructure;

namespace PCDiag.Net.Tcp;

/// <summary>
/// Reads the TCP connection table from WMI (<c>MSFT_NetTCPConnection</c> in
/// <c>root\StandardCimv2</c>). Never throws; returns an empty list on any failure.
/// </summary>
public sealed class WmiTcpConnectionSource : ITcpConnectionSource
{
    public IReadOnlyList<TcpConnectionRecord> GetConnections()
    {
        var result = new List<TcpConnectionRecord>();
        foreach (var row in WmiQuery.Query(
                     "SELECT State, LocalAddress, LocalPort, RemoteAddress, RemotePort, OwningProcess FROM MSFT_NetTCPConnection",
                     "root\\StandardCimv2"))
        {
            var state = TcpConnectionStateExtensions.FromMibState(WmiQuery.GetInt32(row, "State") ?? 0);
            result.Add(new TcpConnectionRecord(
                state,
                WmiQuery.GetString(row, "LocalAddress") ?? "",
                WmiQuery.GetInt32(row, "LocalPort") ?? 0,
                WmiQuery.GetString(row, "RemoteAddress") ?? "",
                WmiQuery.GetInt32(row, "RemotePort") ?? 0,
                WmiQuery.GetInt32(row, "OwningProcess") ?? 0));
        }
        return result;
    }
}