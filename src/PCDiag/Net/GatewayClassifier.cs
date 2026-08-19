namespace PCDiag.Net;

/// <summary>Health classification for the default gateway.</summary>
public enum GatewayHealth
{
    /// <summary>Reachable and responsive.</summary>
    Healthy,

    /// <summary>Reachable with elevated latency but no significant loss.</summary>
    Slow,

    /// <summary>Reachable with significant packet loss.</summary>
    Lossy,

    /// <summary>No probe received a reply.</summary>
    Unreachable
}

/// <summary>
/// Pure classification of gateway measurement statistics. Testable without network access.
/// </summary>
public static class GatewayClassifier
{
    public static GatewayHealth Classify(PingMeasurementStats stats, NetOptions options)
    {
        if (stats.Attempts == 0 || stats.Successes == 0)
            return GatewayHealth.Unreachable;

        if (stats.LossRate >= options.LossWarningRate)
            return GatewayHealth.Lossy;

        if (stats.AvgLatencyMs is double avg && avg >= options.GatewaySlowLatencyMs)
            return GatewayHealth.Slow;

        if (stats.LossRate >= options.LossSuspiciousRate)
            return GatewayHealth.Lossy;

        return GatewayHealth.Healthy;
    }
}