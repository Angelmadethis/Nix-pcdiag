using System.Net.NetworkInformation;

namespace PCDiag.Inventory;

/// <summary>
/// Collects network adapter inventory and identifies the active network connection
/// using the .NET <see cref="NetworkInterface"/> API.
/// </summary>
public static class NetworkAdapterProvider
{
    public static NetworkInfo Collect()
    {
        NetworkInterface[]? interfaces = null;
        try
        {
            interfaces = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch
        {
            interfaces = Array.Empty<NetworkInterface>();
        }

        var adapters = interfaces
            .Select(ToAdapterInfo)
            .Where(a => !string.IsNullOrEmpty(a.Name))
            .ToList();

        var active = adapters.FirstOrDefault(a => a.IsActive && a.GatewayAddresses.Count > 0)
                      ?? adapters.FirstOrDefault(a => a.IsActive);

        return new NetworkInfo
        {
            Adapters = adapters,
            ActiveConnection = active
        };
    }

    private static NetworkAdapterInfo ToAdapterInfo(NetworkInterface nic)
    {
        var addresses = new List<string>();
        var gateways = new List<string>();
        var dns = new List<string>();

        try
        {
            var props = nic.GetIPProperties();
            addresses.AddRange(props.UnicastAddresses.Select(a => a.Address.ToString()));
            gateways.AddRange(props.GatewayAddresses.Select(g => g.Address.ToString()));
            dns.AddRange(props.DnsAddresses.Select(d => d.ToString()));
        }
        catch
        {
            // Addresses unavailable (e.g. permission); leave the lists empty.
        }

        string mac = "";
        try
        {
            mac = string.Join(":", nic.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));
        }
        catch
        {
            mac = "";
        }

        bool isActive = nic.OperationalStatus == OperationalStatus.Up
                        && !nic.IsReceiveOnly
                        && nic.NetworkInterfaceType is not NetworkInterfaceType.Loopback
                        && nic.NetworkInterfaceType is not NetworkInterfaceType.Tunnel
                        && addresses.Any(a => a.Contains('.'));

        return new NetworkAdapterInfo
        {
            Name = nic.Name,
            Description = nic.Description,
            Type = nic.NetworkInterfaceType.ToString(),
            SpeedBps = nic.Speed > 0 ? nic.Speed : null,
            MacAddress = string.IsNullOrEmpty(mac) ? null : mac,
            OperationalStatus = nic.OperationalStatus.ToString(),
            IpAddresses = addresses,
            GatewayAddresses = gateways,
            DnsAddresses = dns,
            IsActive = isActive
        };
    }
}