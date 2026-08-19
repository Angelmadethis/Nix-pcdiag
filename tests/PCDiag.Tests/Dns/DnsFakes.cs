using System.Net;
using PCDiag.Dns;

namespace PCDiag.Tests.Dns;

internal static class DnsProbes
{
    public static DnsProbeResult Success(long ms = 10)
        => new() { Outcome = DnsProbeOutcome.Success, RoundTripMs = ms, RCode = 0 };

    public static DnsProbeResult Failure(long ms = 10, int rcode = 2, string? error = null)
        => new() { Outcome = DnsProbeOutcome.Failed, RoundTripMs = ms, RCode = rcode, Error = error };

    public static DnsProbeResult Timeout()
        => new() { Outcome = DnsProbeOutcome.TimedOut, RoundTripMs = 0, RCode = -1 };
}

/// <summary>
/// Deterministic probe transport: returns queued results in order, then repeats
/// the last result for any remaining probes.
/// </summary>
internal sealed class FakeDnsTransport : IDnsTransport
{
    private readonly DnsProbeResult[] _results;

    public FakeDnsTransport(params DnsProbeResult[] results)
    {
        _results = results;
    }

    public Task<DnsProbeResult> ProbeAsync(IPAddress server, string domain, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (_results.Length == 0)
            return Task.FromResult(DnsProbes.Success());

        var index = Math.Min(Interlocked.Increment(ref _callCount) - 1, _results.Length - 1);
        return Task.FromResult(_results[index]);
    }

    private int _callCount;
}

internal sealed class FakeDnsServerSource : IDnsServerSource
{
    private readonly IReadOnlyList<IPAddress> _servers;

    public FakeDnsServerSource(params string[] servers)
    {
        _servers = servers.Select(IPAddress.Parse).ToList();
    }

    public IReadOnlyList<IPAddress> GetServers() => _servers;
}