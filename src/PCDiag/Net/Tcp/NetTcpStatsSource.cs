using System.Net.NetworkInformation;
using PCDiag.Infrastructure;

namespace PCDiag.Net.Tcp;

/// <summary>
/// Reads cumulative TCP statistics from the .NET <see cref="TcpStatistics"/> API
/// (connection failures, resets) and from the <c>Win32_PerfRawData_Tcpip_TCPv4</c>
/// perf counters (segment retransmission totals). Never throws; missing counters are
/// left as zero so callers can tell what was available.
/// </summary>
public sealed class NetTcpStatsSource : ITcpStatsSource
{
    public TcpCumulativeStats GetStats()
    {
        var stats = new TcpCumulativeStats();

        try
        {
            var tcp = IPGlobalProperties.GetIPGlobalProperties().GetTcpIPv4Statistics();
            stats = stats with
            {
                ConnectionFailures = tcp.FailedConnectionAttempts,
                ConnectionsInitiated = tcp.ConnectionsInitiated,
                ConnectionsAccepted = tcp.ConnectionsAccepted,
                CumulativeConnections = tcp.CumulativeConnections,
                ResetsSent = tcp.ResetsSent
            };
        }
        catch
        {
            // Statistics unavailable; leave the values at zero.
        }

        var row = WmiQuery.Query(
                "SELECT SegmentsRetransmittedPersec, SegmentsSentPersec, SegmentsReceivedPersec, ConnectionsReset FROM Win32_PerfRawData_Tcpip_TCPv4")
            .FirstOrDefault();
        if (row is not null)
        {
            if (WmiQuery.GetInt64(row, "SegmentsRetransmittedPersec") is long retrans)
                stats = stats with { SegmentsRetransmitted = retrans };
            if (WmiQuery.GetInt64(row, "SegmentsSentPersec") is long sent)
                stats = stats with { SegmentsSent = sent };
            if (WmiQuery.GetInt64(row, "SegmentsReceivedPersec") is long received)
                stats = stats with { SegmentsReceived = received };
            if (WmiQuery.GetInt64(row, "ConnectionsReset") is long resets)
                stats = stats with { ResetsReceived = resets };
        }

        return stats;
    }
}