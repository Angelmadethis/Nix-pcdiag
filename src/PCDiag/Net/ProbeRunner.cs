using System.Net;

namespace PCDiag.Net;

/// <summary>
/// Sends a small, bounded set of ICMP echo probes to one target with early abort
/// on consecutive timeouts so unreachable targets are not hammered.
/// </summary>
public static class ProbeRunner
{
    public static async Task<IReadOnlyList<PingProbeResult>> ProbeAsync(
        IPingProbe probe,
        IPAddress target,
        int count,
        TimeSpan timeout,
        int maxTimeoutsBeforeAbort,
        int payloadBytes,
        bool dontFragment,
        CancellationToken cancellationToken)
    {
        var results = new List<PingProbeResult>(Math.Max(0, count));
        var consecutiveTimeouts = 0;

        for (int i = 0; i < count; i++)
        {
            var result = await probe
                .ProbeAsync(target, payloadBytes, dontFragment, timeout, cancellationToken)
                .ConfigureAwait(false);

            results.Add(result);
            consecutiveTimeouts = result.Outcome == PingProbeOutcome.TimedOut ? consecutiveTimeouts + 1 : 0;

            if (consecutiveTimeouts >= maxTimeoutsBeforeAbort)
                break;
        }

        return results;
    }
}