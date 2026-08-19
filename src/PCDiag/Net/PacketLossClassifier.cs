namespace PCDiag.Net;

/// <summary>Health classification for packet-loss and latency measurements.</summary>
public enum PacketLossHealth
{
    /// <summary>Reliable and responsive.</summary>
    Healthy,

    /// <summary>Reliable but with elevated latency.</summary>
    Slow,

    /// <summary>Low but notable packet loss.</summary>
    Elevated,

    /// <summary>Significant packet loss.</summary>
    Lossy,

    /// <summary>No probe received a reply.</summary>
    Unreachable,

    /// <summary>The gateway is reachable but every internet endpoint is unreachable.</summary>
    InternetUnreachable
}

/// <summary>
/// Pure classification of packet-loss and latency statistics. Testable without network access.
/// </summary>
public static class PacketLossClassifier
{
    public static PacketLossHealth ClassifyTarget(PingMeasurementStats stats, NetOptions options, double slowLatencyMs)
    {
        if (stats.Attempts == 0 || stats.Successes == 0)
            return PacketLossHealth.Unreachable;

        if (stats.LossRate >= options.LossWarningRate)
            return PacketLossHealth.Lossy;

        if (stats.LossRate >= options.LossSuspiciousRate)
            return PacketLossHealth.Elevated;

        if (stats.AvgLatencyMs is double avg && avg >= slowLatencyMs)
            return PacketLossHealth.Slow;

        return PacketLossHealth.Healthy;
    }

    public static PacketLossHealth ClassifyOverall(PacketLossHealth gateway, IReadOnlyList<PacketLossHealth> internet)
    {
        if (gateway == PacketLossHealth.Unreachable)
            return PacketLossHealth.Unreachable;

        if (internet.Count > 0 && internet.All(h => h == PacketLossHealth.Unreachable))
            return PacketLossHealth.InternetUnreachable;

        if (gateway == PacketLossHealth.Lossy || internet.Any(h => h == PacketLossHealth.Lossy))
            return PacketLossHealth.Lossy;

        if (gateway == PacketLossHealth.Elevated || internet.Any(h => h == PacketLossHealth.Elevated))
            return PacketLossHealth.Elevated;

        if (gateway == PacketLossHealth.Slow || internet.Any(h => h == PacketLossHealth.Slow))
            return PacketLossHealth.Slow;

        return PacketLossHealth.Healthy;
    }
}