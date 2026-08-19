using System.Net;

namespace PCDiag.Dns;

/// <summary>
/// Abstraction over discovery of the system's active DNS servers so it can be
/// mocked in tests. Implementations must never throw.
/// </summary>
public interface IDnsServerSource
{
    /// <summary>The active, configured DNS server addresses (deduplicated).</summary>
    IReadOnlyList<IPAddress> GetServers();
}