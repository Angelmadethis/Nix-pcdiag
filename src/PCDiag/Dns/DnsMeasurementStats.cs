namespace PCDiag.Dns;

/// <summary>
/// Aggregated statistics over a set of DNS probes for a single resolver.
/// Computed by <see cref="Compute"/>; pure and unit-testable.
/// </summary>
public sealed record DnsMeasurementStats
{
    public int Attempts { get; init; }
    public int Successes { get; init; }
    public int Failures { get; init; }
    public int Timeouts { get; init; }

    /// <summary>Successes / Attempts (0 when there are no attempts).</summary>
    public double SuccessRate { get; init; }

    /// <summary>(Failures + Timeouts) / Attempts (0 when there are no attempts).</summary>
    public double FailureRate { get; init; }

    /// <summary>Average round-trip over received responses (Success + Failed); null when none received.</summary>
    public double? AvgLatencyMs { get; init; }

    public long? MinLatencyMs { get; init; }
    public long? MaxLatencyMs { get; init; }

    /// <summary>Compute aggregate statistics from a set of probe results.</summary>
    public static DnsMeasurementStats Compute(IReadOnlyList<DnsProbeResult> probes)
    {
        var received = probes
            .Where(p => p.Outcome != DnsProbeOutcome.TimedOut)
            .ToList();

        double? avg = null;
        long? min = null;
        long? max = null;
        if (received.Count > 0)
        {
            avg = received.Average(p => p.RoundTripMs);
            min = received.Min(p => p.RoundTripMs);
            max = received.Max(p => p.RoundTripMs);
        }

        var successes = probes.Count(p => p.Outcome == DnsProbeOutcome.Success);
        var failures = probes.Count(p => p.Outcome == DnsProbeOutcome.Failed);
        var timeouts = probes.Count(p => p.Outcome == DnsProbeOutcome.TimedOut);

        return new DnsMeasurementStats
        {
            Attempts = probes.Count,
            Successes = successes,
            Failures = failures,
            Timeouts = timeouts,
            SuccessRate = probes.Count > 0 ? (double)successes / probes.Count : 0,
            FailureRate = probes.Count > 0 ? (double)(failures + timeouts) / probes.Count : 0,
            AvgLatencyMs = avg,
            MinLatencyMs = min,
            MaxLatencyMs = max
        };
    }
}