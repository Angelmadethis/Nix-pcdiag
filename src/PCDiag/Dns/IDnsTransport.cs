using System.Net;

namespace PCDiag.Dns;

/// <summary>
/// Abstraction over the UDP DNS probe so network behavior can be mocked in tests.
/// Implementations must never throw: network errors are returned as failed/timeout results.
/// </summary>
public interface IDnsTransport
{
    /// <summary>
    /// Probe a single DNS resolver for an A record of <paramref name="domain"/>.
    /// </summary>
    Task<DnsProbeResult> ProbeAsync(IPAddress server, string domain, TimeSpan timeout, CancellationToken cancellationToken);
}