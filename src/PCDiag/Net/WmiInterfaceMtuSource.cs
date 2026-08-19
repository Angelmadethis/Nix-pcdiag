using PCDiag.Infrastructure;

namespace PCDiag.Net;

/// <summary>
/// Reads the interface MTU from WMI. Prefers <c>Win32_NetworkAdapterConfiguration.MTU</c>
/// matched to the active adapter by IP; when that value is empty (common for Wi-Fi
/// adapters) it falls back to <c>MSFT_NetIPInterface.NlMtu</c> in
/// <c>root\StandardCimv2</c> matched by adapter name. Never throws; returns null when
/// the MTU cannot be determined.
/// </summary>
public sealed class WmiInterfaceMtuSource : IInterfaceMtuSource
{
    public int? GetMtu(IReadOnlyList<string> adapterIpAddresses, string? adapterName = null)
    {
        var fromConfig = FindMtu(ReadRows(), adapterIpAddresses);
        if (fromConfig is not null)
            return fromConfig;

        if (!string.IsNullOrWhiteSpace(adapterName))
            return FindMtuByInterfaceName(ReadNetIpInterfaceRows(), adapterName);

        return null;
    }

    internal static IEnumerable<(string[]? Ips, int? Mtu)> ReadRows()
    {
        foreach (var row in WmiQuery.Query(
                     "SELECT IPEnabled, IPAddress, MTU FROM Win32_NetworkAdapterConfiguration"))
        {
            if (WmiQuery.GetBool(row, "IPEnabled") != true)
                continue;

            string[]? ips = null;
            try
            {
                ips = row["IPAddress"] as string[];
            }
            catch
            {
                ips = null;
            }

            yield return (ips, WmiQuery.GetInt32(row, "MTU"));
        }
    }

    internal static IEnumerable<(string? InterfaceAlias, int? NlMtu)> ReadNetIpInterfaceRows()
    {
        foreach (var row in WmiQuery.Query(
                     "SELECT NlMtu, InterfaceAlias FROM MSFT_NetIPInterface WHERE AddressFamily = 2",
                     "root\\StandardCimv2"))
        {
            yield return (WmiQuery.GetString(row, "InterfaceAlias"), WmiQuery.GetInt32(row, "NlMtu"));
        }
    }

    /// <summary>
    /// Find the MTU of the first enabled row whose IPs intersect the adapter's IPs.
    /// Pure and unit-testable.
    /// </summary>
    public static int? FindMtu(IEnumerable<(string[]? Ips, int? Mtu)> rows, IReadOnlyList<string> adapterIpAddresses)
    {
        foreach (var (ips, mtu) in rows)
        {
            if (mtu is not null && mtu > 0 && IpsMatch(ips, adapterIpAddresses))
                return mtu;
        }
        return null;
    }

    /// <summary>
    /// Find the MTU of the first IPv4 interface row whose alias matches the adapter
    /// name (case-insensitive). Pure and unit-testable.
    /// </summary>
    public static int? FindMtuByInterfaceName(IEnumerable<(string? InterfaceAlias, int? NlMtu)> rows, string adapterName)
    {
        foreach (var (alias, mtu) in rows)
        {
            if (mtu is not null && mtu > 0 &&
                string.Equals(alias, adapterName, StringComparison.OrdinalIgnoreCase))
                return mtu;
        }
        return null;
    }

    /// <summary>True when any adapter IP matches any row IP (case-insensitive).</summary>
    public static bool IpsMatch(string[]? rowIps, IReadOnlyList<string> adapterIps)
    {
        if (rowIps is null || rowIps.Length == 0 || adapterIps.Count == 0)
            return false;

        foreach (var ip in adapterIps)
        {
            foreach (var rowIp in rowIps)
            {
                if (string.Equals(ip, rowIp, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }
}