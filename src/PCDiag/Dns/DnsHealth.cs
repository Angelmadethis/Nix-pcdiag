namespace PCDiag.Dns;

/// <summary>
/// Health classification for DNS resolvers and for the overall configuration.
/// </summary>
public enum DnsHealth
{
    /// <summary>Reliable and responsive.</summary>
    Healthy,

    /// <summary>Reliable but with elevated latency. Not, by itself, evidence to change DNS.</summary>
    Slow,

    /// <summary>Reachable but with significant failures/timeouts, or partly unreachable.</summary>
    Unreliable,

    /// <summary>No resolver responded to any query.</summary>
    Unreachable,

    /// <summary>No active DNS servers are configured.</summary>
    NoConfiguration
}

/// <summary>
/// Pure classification logic mapping measurement statistics to a
/// <see cref="DnsHealth"/>. Testable without any network access.
/// </summary>
public static class DnsClassifier
{
    /// <summary>Classify a single resolver from its probe statistics.</summary>
    public static DnsHealth Classify(DnsMeasurementStats stats, DnsOptions options)
    {
        if (stats.Attempts == 0)
            return DnsHealth.Unreachable;

        if (stats.Successes == 0)
            return stats.Timeouts >= stats.Attempts
                ? DnsHealth.Unreachable
                : DnsHealth.Unreliable;

        if (stats.FailureRate >= options.UnreliableFailureRate)
            return DnsHealth.Unreliable;

        if (stats.AvgLatencyMs is double avg && avg >= options.SlowLatencyMs)
            return DnsHealth.Slow;

        return DnsHealth.Healthy;
    }

    /// <summary>
    /// Classify the overall DNS configuration from per-resolver health:
    /// no resolvers → NoConfiguration; all unreachable → Unreachable;
    /// any unreliable or (partially) unreachable → Unreliable;
    /// otherwise the slowest reliable resolver → Slow; else Healthy.
    /// </summary>
    public static DnsHealth ClassifyOverall(IReadOnlyList<DnsHealth> resolverHealths)
    {
        if (resolverHealths.Count == 0)
            return DnsHealth.NoConfiguration;

        if (resolverHealths.All(h => h == DnsHealth.Unreachable))
            return DnsHealth.Unreachable;

        if (resolverHealths.Any(h => h is DnsHealth.Unreliable or DnsHealth.Unreachable))
            return DnsHealth.Unreliable;

        if (resolverHealths.Any(h => h == DnsHealth.Slow))
            return DnsHealth.Slow;

        return DnsHealth.Healthy;
    }
}