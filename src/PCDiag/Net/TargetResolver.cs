using System.Net;
using System.Net.Sockets;

namespace PCDiag.Net;

/// <summary>
/// Resolves configured test targets (hostnames or addresses) to IPv4 addresses and
/// extracts the default gateway from an adapter's gateway list. Never throws;
/// unresolvable targets are skipped.
/// </summary>
public static class TargetResolver
{
    /// <summary>Resolve up to <paramref name="maxTargets"/> targets, preferring IPv4.</summary>
    public static async Task<IReadOnlyList<IPAddress>> ResolveAsync(
        IReadOnlyList<string> configured,
        int maxTargets,
        CancellationToken cancellationToken)
    {
        var results = new List<IPAddress>();
        foreach (var target in configured.Take(Math.Max(0, maxTargets)))
        {
            var address = await ResolveOneAsync(target, cancellationToken).ConfigureAwait(false);
            if (address is not null)
                results.Add(address);
        }
        return results;
    }

    /// <summary>Resolve a single hostname or literal address to an IPv4 address, or null.</summary>
    public static async Task<IPAddress?> ResolveOneAsync(string target, CancellationToken cancellationToken)
    {
        try
        {
            if (IPAddress.TryParse(target, out var address))
                return address;

            var addresses = await System.Net.Dns.GetHostAddressesAsync(target, cancellationToken).ConfigureAwait(false);
            return addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>The first parseable gateway in the adapter's gateway list, or null.</summary>
    public static IPAddress? FirstGateway(IReadOnlyList<string> gateways)
    {
        foreach (var gateway in gateways)
        {
            if (IPAddress.TryParse(gateway, out var address))
                return address;
        }
        return null;
    }
}