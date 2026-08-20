namespace PCDiag.Net.Tcp;

/// <summary>
/// Cumulative TCP statistics since boot. Ratios are computed from the raw totals so
/// values are interpreted in context (e.g. connection failures relative to how many
/// connections were actually initiated) rather than as bare, meaningless numbers.
/// </summary>
public sealed record TcpCumulativeStats
{
    public long ConnectionFailures { get; init; }
    public long ConnectionsInitiated { get; init; }
    public long ConnectionsAccepted { get; init; }
    public long CumulativeConnections { get; init; }
    public long ResetsSent { get; init; }
    public long ResetsReceived { get; init; }
    public long SegmentsRetransmitted { get; init; }
    public long SegmentsSent { get; init; }
    public long SegmentsReceived { get; init; }

    /// <summary>Failed connections / connections initiated, or null when no initiations.</summary>
    public double? FailureRatio
        => ConnectionsInitiated > 0 ? (double)ConnectionFailures / ConnectionsInitiated : null;

    /// <summary>Retransmitted segments / (sent + received) segments, or null when no segments.</summary>
    public double? RetransmissionRatio
        => SegmentsSent + SegmentsReceived > 0
            ? (double)SegmentsRetransmitted / (SegmentsSent + SegmentsReceived)
            : null;
}

/// <summary>Abstraction over reading cumulative TCP statistics so checks can be tested with fakes.</summary>
public interface ITcpStatsSource
{
    TcpCumulativeStats GetStats();
}