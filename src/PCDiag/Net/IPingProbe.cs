using System.Net;

namespace PCDiag.Net;

/// <summary>
/// Abstraction over a single ICMP echo probe so network behavior can be mocked in
/// tests. Implementations must never throw: network errors are returned as probe
/// results instead of exceptions.
/// </summary>
public interface IPingProbe
{
    /// <summary>
    /// Send one ICMP echo request with the given payload size, optionally setting the
    /// Don't-Fragment bit, and wait up to <paramref name="timeout"/> for a reply.
    /// </summary>
    Task<PingProbeResult> ProbeAsync(
        IPAddress target,
        int payloadBytes,
        bool dontFragment,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}