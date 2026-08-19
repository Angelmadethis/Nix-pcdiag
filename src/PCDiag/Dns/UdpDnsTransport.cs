using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace PCDiag.Dns;

/// <summary>
/// Real DNS probe over UDP (port 53) using a minimal RFC 1035 query/response.
/// Measures round-trip time per probe; timeouts and socket errors become
/// failed/timeout results instead of exceptions.
/// </summary>
public sealed class UdpDnsTransport : IDnsTransport
{
    private const int DnsPort = 53;

    public async Task<DnsProbeResult> ProbeAsync(IPAddress server, string domain, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var query = DnsMessage.BuildQuery(domain, out var queryId);
        var stopwatch = Stopwatch.StartNew();

        using var client = new UdpClient(server.AddressFamily);
        try
        {
            client.Connect(server, DnsPort);
            await client.SendAsync(query, timeoutCts.Token).ConfigureAwait(false);

            var response = await client.ReceiveAsync(timeoutCts.Token).ConfigureAwait(false);
            stopwatch.Stop();

            var parsed = DnsMessage.ParseResponse(response.Buffer, queryId);
            if (!parsed.WellFormed || !parsed.MatchesId)
                return Failed("Malformed or mismatched response.", stopwatch.ElapsedMilliseconds);
            if (parsed.RCode != 0)
                return Failed($"Lookup failed (RCODE {parsed.RCode}).", stopwatch.ElapsedMilliseconds, parsed.RCode);

            return new DnsProbeResult
            {
                Outcome = DnsProbeOutcome.Success,
                RoundTripMs = stopwatch.ElapsedMilliseconds,
                RCode = 0
            };
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new DnsProbeResult { Outcome = DnsProbeOutcome.TimedOut, RoundTripMs = 0, RCode = -1 };
        }
        catch (SocketException ex)
        {
            stopwatch.Stop();
            return Failed(ex.Message, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            return Failed(ex.Message, stopwatch.ElapsedMilliseconds);
        }
    }

    private static DnsProbeResult Failed(string error, long roundTripMs, int rcode = -1)
        => new()
        {
            Outcome = DnsProbeOutcome.Failed,
            RoundTripMs = roundTripMs,
            RCode = rcode,
            Error = error
        };
}