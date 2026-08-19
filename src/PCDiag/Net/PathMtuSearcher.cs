namespace PCDiag.Net;

/// <summary>A single payload-size probe performed during a path MTU search.</summary>
public sealed record SizeProbeRecord(int Payload, PingProbeOutcome Outcome, long RoundTripMs);

/// <summary>
/// The outcome of a path MTU search against a single target.
/// <see cref="DetectedPathMtu"/> is null when no Don't-Fragment packet of any
/// tested size received a reply (target dead, or ICMP echo blocked).
/// </summary>
public sealed record PathMtuResult
{
    /// <summary>Largest payload that succeeded with the Don't-Fragment bit set; 0 if none.</summary>
    public int MaxPayloadSucceeded { get; init; }

    /// <summary>Highest payload tested during the search.</summary>
    public int PayloadLimitTested { get; init; }

    /// <summary>True when the boundary was confirmed by additional probes.</summary>
    public bool BoundaryConfirmed { get; init; }

    /// <summary>True when a router reported "fragmentation needed" (cooperative PMTU discovery).</summary>
    public bool SawFragmentationNeeded { get; init; }

    /// <summary>
    /// True when Don't-Fragment packets larger than the success boundary timed out
    /// instead of receiving an ICMP error (possible PMTU black hole).
    /// </summary>
    public bool SawBlackHole { get; init; }

    /// <summary>Total probes sent during the search and confirmation.</summary>
    public int ProbeCount { get; init; }

    /// <summary>Every probe performed, in order, for evidence.</summary>
    public IReadOnlyList<SizeProbeRecord> Trace { get; init; } = Array.Empty<SizeProbeRecord>();

    /// <summary>
    /// Detected path MTU (largest successful payload + IPv4/ICMP overhead),
    /// or null when no payload succeeded.
    /// </summary>
    public int? DetectedPathMtu
        => MaxPayloadSucceeded > 0 ? MaxPayloadSucceeded + MtuOptions.IcmpIpv4Overhead : null;
}

/// <summary>
/// Measures the largest Don't-Fragment ICMP payload that traverses a path using a
/// bounded binary search plus boundary confirmation. The probe budget is roughly
/// log2 of the search range plus confirmations, so it never floods the network and
/// always terminates. A non-success reply (fragmentation needed, unreachable, or a
/// timeout) is treated as "this size does not pass".
/// </summary>
public static class PathMtuSearcher
{
    public static async Task<PathMtuResult> MeasureAsync(
        int minPayload,
        int maxPayload,
        int confirmationProbes,
        Func<int, CancellationToken, Task<PingProbeResult>> probe,
        CancellationToken cancellationToken)
    {
        minPayload = Math.Max(0, minPayload);
        maxPayload = Math.Max(minPayload, maxPayload);

        var trace = new List<SizeProbeRecord>();
        var sawFrag = false;
        var largestSuccess = 0;

        int low = minPayload;
        int high = maxPayload;

        while (low < high)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mid = (low + high + 1) / 2;
            var result = await probe(mid, cancellationToken).ConfigureAwait(false);
            trace.Add(new SizeProbeRecord(mid, result.Outcome, result.RoundTripMs));

            if (result.Outcome == PingProbeOutcome.Success)
            {
                largestSuccess = Math.Max(largestSuccess, mid);
                low = mid;
            }
            else
            {
                high = mid - 1;
                if (result.Outcome == PingProbeOutcome.FragmentationNeeded)
                    sawFrag = true;
            }
        }

        var confirmed = false;
        if (largestSuccess >= minPayload && confirmationProbes > 0)
        {
            var ok = 0;
            for (int i = 0; i < confirmationProbes; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await probe(largestSuccess, cancellationToken).ConfigureAwait(false);
                trace.Add(new SizeProbeRecord(largestSuccess, result.Outcome, result.RoundTripMs));
                if (result.Outcome == PingProbeOutcome.Success)
                    ok++;
                else if (result.Outcome == PingProbeOutcome.FragmentationNeeded)
                    sawFrag = true;
            }
            confirmed = ok == confirmationProbes;
        }

        var blackHole = largestSuccess >= minPayload
                        && trace.Any(t => t.Payload > largestSuccess && t.Outcome == PingProbeOutcome.TimedOut);

        return new PathMtuResult
        {
            MaxPayloadSucceeded = largestSuccess,
            PayloadLimitTested = maxPayload,
            BoundaryConfirmed = confirmed,
            SawFragmentationNeeded = sawFrag,
            SawBlackHole = blackHole,
            ProbeCount = trace.Count,
            Trace = trace
        };
    }
}