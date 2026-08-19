namespace PCDiag.Net;

/// <summary>
/// Abstraction over reading the interface MTU of a network adapter so it can be
/// mocked in tests. Implementations must never throw.
/// </summary>
public interface IInterfaceMtuSource
{
    /// <summary>
    /// The IPv4 interface MTU for the adapter owning the given IPs, or null when unknown.
    /// <paramref name="adapterName"/> is an optional fallback key (e.g. the adapter alias)
    /// used when the IP-based lookup finds no MTU.
    /// </summary>
    int? GetMtu(IReadOnlyList<string> adapterIpAddresses, string? adapterName = null);
}