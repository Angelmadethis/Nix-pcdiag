using PCDiag.Infrastructure;

namespace PCDiag.Net.Tcp;

/// <summary>Cumulative network-adapter receive/transmit error counters since boot.</summary>
public sealed record TcpAdapterErrorStats(
    string InstanceName,
    long ReceivedErrors,
    long OutboundErrors,
    long ReceivedDiscarded,
    long OutboundDiscarded)
{
    public long TotalErrors => ReceivedErrors + OutboundErrors;
    public long TotalDiscards => ReceivedDiscarded + OutboundDiscarded;
}

/// <summary>Abstraction over adapter error counters so checks can be tested with fakes.</summary>
public interface ITcpAdapterErrorSource
{
    /// <summary>Adapter error counters for the given adapter, or null when unavailable.</summary>
    TcpAdapterErrorStats? GetFor(string? adapterName, string? adapterDescription);
}

/// <summary>
/// Reads adapter error counters from <c>Win32_PerfRawData_Tcpip_NetworkInterface</c>.
/// Perf instance names differ from .NET adapter names ("Intel[R] Wi-Fi 6..." vs
/// "Intel(R) Wi-Fi 6..."), so matching is done on a normalized alphanumeric form.
/// Never throws; returns null when the counters are unavailable.
/// </summary>
public sealed class WmiTcpAdapterErrorSource : ITcpAdapterErrorSource
{
    public TcpAdapterErrorStats? GetFor(string? adapterName, string? adapterDescription)
    {
        var rows = new List<(string Name, TcpAdapterErrorStats Stats)>();
        foreach (var row in WmiQuery.Query(
                     "SELECT Name, PacketsReceivedErrors, PacketsOutboundErrors, PacketsReceivedDiscarded, PacketsOutboundDiscarded FROM Win32_PerfRawData_Tcpip_NetworkInterface"))
        {
            var name = WmiQuery.GetString(row, "Name");
            if (string.IsNullOrWhiteSpace(name))
                continue;

            rows.Add((name, new TcpAdapterErrorStats(
                name,
                WmiQuery.GetInt64(row, "PacketsReceivedErrors") ?? 0,
                WmiQuery.GetInt64(row, "PacketsOutboundErrors") ?? 0,
                WmiQuery.GetInt64(row, "PacketsReceivedDiscarded") ?? 0,
                WmiQuery.GetInt64(row, "PacketsOutboundDiscarded") ?? 0)));
        }

        if (rows.Count == 0)
            return null;

        var wanted = Normalize(adapterDescription) ?? Normalize(adapterName);
        if (wanted is not null)
        {
            var match = rows.FirstOrDefault(r => Normalize(r.Name) == wanted);
            if (match.Stats is not null)
                return match.Stats;
        }

        return rows.Count == 1 ? rows[0].Stats : null;
    }

    /// <summary>Normalize a name for loose matching: lowercase, drop non-alphanumerics. Pure and unit-testable.</summary>
    public static string? Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        return new string(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }
}