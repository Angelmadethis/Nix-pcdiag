namespace PCDiag.Net;

/// <summary>
/// Aggregated statistics over a set of ICMP echo probes to a single target.
/// Computed by <see cref="Compute"/>; pure and unit-testable.
/// </summary>
public sealed record PingMeasurementStats
{
    public int Attempts { get; init; }
    public int Successes { get; init; }
    public int Failures { get; init; }
    public int Timeouts { get; init; }

    /// <summary>Successes / Attempts (0 when there are no attempts).</summary>
    public double SuccessRate { get; init; }

    /// <summary>(Failures + Timeouts) / Attempts (0 when there are no attempts).</summary>
    public double LossRate { get; init; }

    /// <summary>Average round-trip over received replies; null when none received.</summary>
    public double? AvgLatencyMs { get; init; }

    public long? MinLatencyMs { get; init; }
    public long? MaxLatencyMs { get; init; }

    /// <summary>Compute aggregate statistics from a set of probe results.</summary>
    public static PingMeasurementStats Compute(IReadOnlyList<PingProbeResult> probes)
    {
        var replies = probes.Where(p => p.Outcome == PingProbeOutcome.Success).ToList();
        var successes = replies.Count;
        var timeouts = probes.Count(p => p.Outcome == PingProbeOutcome.TimedOut);
        var failures = probes.Count - successes - timeouts;

        double? avg = null;
        long? min = null;
        long? max = null;
        if (successes > 0)
        {
            avg = replies.Average(p => p.RoundTripMs);
            min = replies.Min(p => p.RoundTripMs);
            max = replies.Max(p => p.RoundTripMs);
        }

        return new PingMeasurementStats
        {
            Attempts = probes.Count,
            Successes = successes,
            Failures = failures,
            Timeouts = timeouts,
            SuccessRate = probes.Count > 0 ? (double)successes / probes.Count : 0,
            LossRate = probes.Count > 0 ? (double)(failures + timeouts) / probes.Count : 0,
            AvgLatencyMs = avg,
            MinLatencyMs = min,
            MaxLatencyMs = max
        };
    }
}