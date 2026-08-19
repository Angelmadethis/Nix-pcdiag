using System.Net;
using PCDiag.Infrastructure;

namespace PCDiag.Dns;

/// <summary>
/// Reads the active DNS servers from WMI (<c>Win32_NetworkAdapterConfiguration</c>),
/// deduplicating and normalizing the raw strings. Never throws.
/// </summary>
public sealed class WmiDnsServerSource : IDnsServerSource
{
    public IReadOnlyList<IPAddress> GetServers()
    {
        var raw = new List<string>();
        foreach (var row in WmiQuery.Query(
                     "SELECT IPEnabled, DNSServerSearchOrder FROM Win32_NetworkAdapterConfiguration"))
        {
            if (WmiQuery.GetBool(row, "IPEnabled") != true)
                continue;

            string[]? order = null;
            try
            {
                order = row["DNSServerSearchOrder"] as string[];
            }
            catch
            {
                order = null;
            }

            if (order is not null)
                raw.AddRange(order);
        }

        return ParseAndDedupe(raw);
    }

    /// <summary>
    /// Normalize raw DNS server strings into a deduplicated list of valid IP addresses.
    /// Pure and unit-testable.
    /// </summary>
    public static IReadOnlyList<IPAddress> ParseAndDedupe(IEnumerable<string> raw)
    {
        var result = new List<IPAddress>();
        var seen = new HashSet<IPAddress>();
        foreach (var value in raw)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;
            if (IPAddress.TryParse(value.Trim(), out var address) && seen.Add(address))
                result.Add(address);
        }
        return result;
    }
}