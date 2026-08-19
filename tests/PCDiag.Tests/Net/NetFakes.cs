using System.Net;
using PCDiag.Inventory;
using PCDiag.Net;

namespace PCDiag.Tests.Net;

internal static class PingResults
{
    public static PingProbeResult Success(long ms = 5)
        => new() { Outcome = PingProbeOutcome.Success, RoundTripMs = ms, IcmpStatus = "Success" };

    public static PingProbeResult Frag(long ms = 5)
        => new() { Outcome = PingProbeOutcome.FragmentationNeeded, RoundTripMs = ms, IcmpStatus = "PacketTooBig" };

    public static PingProbeResult Timeout()
        => new() { Outcome = PingProbeOutcome.TimedOut, RoundTripMs = 0, IcmpStatus = "TimedOut" };

    public static PingProbeResult Unreachable(long ms = 5)
        => new() { Outcome = PingProbeOutcome.Unreachable, RoundTripMs = ms, IcmpStatus = "DestinationHostUnreachable" };
}

/// <summary>
/// Scriptable ping probe: delegates every call to a supplied function so tests can
/// simulate cooperative, black-hole, lossy, slow, or dead paths.
/// </summary>
internal sealed class FakePingProbe : IPingProbe
{
    private readonly Func<IPAddress, int, bool, TimeSpan, CancellationToken, Task<PingProbeResult>> _impl;
    public int CallCount;
    public List<IPAddress> CalledTargets { get; } = new();

    public FakePingProbe(Func<IPAddress, int, bool, TimeSpan, CancellationToken, Task<PingProbeResult>> impl)
    {
        _impl = impl;
    }

    public Task<PingProbeResult> ProbeAsync(IPAddress target, int payloadBytes, bool dontFragment, TimeSpan timeout, CancellationToken cancellationToken)
    {
        CallCount++;
        CalledTargets.Add(target);
        return _impl(target, payloadBytes, dontFragment, timeout, cancellationToken);
    }
}

/// <summary>Stub interface-MTU source with a fixed value.</summary>
internal sealed class FakeMtuSource : IInterfaceMtuSource
{
    private readonly int? _mtu;
    public FakeMtuSource(int? mtu) => _mtu = mtu;

    public int? GetMtu(IReadOnlyList<string> adapterIpAddresses, string? adapterName = null) => _mtu;
}

/// <summary>Helpers for building inventory with a gateway for the checks.</summary>
internal static class NetInventory
{
    public const string AdapterIp = "192.168.1.50";
    public static readonly IPAddress GatewayIp = IPAddress.Parse("192.168.1.1");
    public static readonly IPAddress InternetIp = IPAddress.Parse("1.1.1.1");

    public static PCDiag.Inventory.SystemInventory WithGateway(string? gateway = "192.168.1.1")
    {
        var adapter = new NetworkAdapterInfo
        {
            Name = "Ethernet",
            Description = "Test Adapter",
            IpAddresses = new[] { AdapterIp },
            GatewayAddresses = gateway is null ? Array.Empty<string>() : new[] { gateway },
            IsActive = true,
            OperationalStatus = "Up"
        };

        return new PCDiag.Inventory.SystemInventory
        {
            Network = new NetworkInfo { Adapters = new[] { adapter }, ActiveConnection = adapter }
        };
    }

    public static PCDiag.Inventory.SystemInventory WithNoActiveConnection()
        => new() { Network = new NetworkInfo() };
}

/// <summary>
/// Simulates a path with a fixed MTU. Sizes up to <paramref name="pathMtu"/> succeed;
/// larger sizes return either "fragmentation needed" (cooperative) or time out (black hole).
/// </summary>
internal static class PathSimulator
{
    public static Func<IPAddress, int, bool, TimeSpan, CancellationToken, Task<PingProbeResult>> Cooperative(int pathMtu, long rttMs = 5)
        => (_, payload, _, _, _) =>
            Task.FromResult(payload + MtuOptions.IcmpIpv4Overhead <= pathMtu ? PingResults.Success(rttMs) : PingResults.Frag());

    public static Func<IPAddress, int, bool, TimeSpan, CancellationToken, Task<PingProbeResult>> BlackHole(int pathMtu)
        => (_, payload, _, _, _) =>
            Task.FromResult(payload + MtuOptions.IcmpIpv4Overhead <= pathMtu ? PingResults.Success() : PingResults.Timeout());

    public static Func<IPAddress, int, bool, TimeSpan, CancellationToken, Task<PingProbeResult>> Dead()
        => (_, _, _, _, _) => Task.FromResult(PingResults.Timeout());

    public static Func<IPAddress, int, bool, TimeSpan, CancellationToken, Task<PingProbeResult>> AlwaysSuccess(long rttMs = 5)
        => (_, _, _, _, _) => Task.FromResult(PingResults.Success(rttMs));

    /// <summary>Per-target behavior: gateway uses one path, internet targets another.</summary>
    public static Func<IPAddress, int, bool, TimeSpan, CancellationToken, Task<PingProbeResult>> GatewayThen(
        IPAddress gateway,
        Func<IPAddress, int, bool, TimeSpan, CancellationToken, Task<PingProbeResult>> gatewayBehavior,
        Func<IPAddress, int, bool, TimeSpan, CancellationToken, Task<PingProbeResult>> internetBehavior)
        => (target, payload, df, timeout, ct) =>
            target.Equals(gateway)
                ? gatewayBehavior(target, payload, df, timeout, ct)
                : internetBehavior(target, payload, df, timeout, ct);
}