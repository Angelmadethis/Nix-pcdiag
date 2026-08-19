using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;

namespace PCDiag.Net;

/// <summary>
/// Real ICMP echo probe built on <see cref="Ping"/>. Timeouts and socket errors
/// become probe results instead of exceptions. Sending ICMP echo on Windows does
/// not require elevation.
/// </summary>
public sealed class SystemPingProbe : IPingProbe
{
    public async Task<PingProbeResult> ProbeAsync(
        IPAddress target,
        int payloadBytes,
        bool dontFragment,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var buffer = new byte[Math.Max(0, payloadBytes)];
        Array.Fill(buffer, (byte)'P');

        using var ping = new Ping();
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var reply = await ping.SendPingAsync(
                target,
                timeout,
                buffer,
                new PingOptions { DontFragment = dontFragment },
                timeoutCts.Token).ConfigureAwait(false);
            stopwatch.Stop();
            return Map(reply.Status, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new PingProbeResult { Outcome = PingProbeOutcome.TimedOut, RoundTripMs = 0, IcmpStatus = nameof(IPStatus.TimedOut) };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            return new PingProbeResult { Outcome = PingProbeOutcome.Failed, RoundTripMs = stopwatch.ElapsedMilliseconds, Error = ex.Message };
        }
    }

    /// <summary>
    /// Map a platform ping status to a probe outcome. Pure and unit-testable without
    /// constructing a <see cref="PingReply"/>.
    /// </summary>
    public static PingProbeResult Map(IPStatus status, long roundTripMs)
    {
        switch (status)
        {
            case IPStatus.Success:
                return new PingProbeResult { Outcome = PingProbeOutcome.Success, RoundTripMs = roundTripMs, IcmpStatus = nameof(IPStatus.Success) };
            case IPStatus.PacketTooBig:
                return new PingProbeResult { Outcome = PingProbeOutcome.FragmentationNeeded, RoundTripMs = roundTripMs, IcmpStatus = nameof(IPStatus.PacketTooBig) };
            case IPStatus.TimedOut:
                return new PingProbeResult { Outcome = PingProbeOutcome.TimedOut, RoundTripMs = 0, IcmpStatus = nameof(IPStatus.TimedOut) };
            case IPStatus.DestinationNetworkUnreachable:
            case IPStatus.DestinationHostUnreachable:
            case IPStatus.DestinationProtocolUnreachable:
            case IPStatus.DestinationPortUnreachable:
            case IPStatus.DestinationUnreachable:
            case IPStatus.BadDestination:
                return new PingProbeResult { Outcome = PingProbeOutcome.Unreachable, RoundTripMs = roundTripMs, IcmpStatus = status.ToString() };
            default:
                return new PingProbeResult { Outcome = PingProbeOutcome.Failed, RoundTripMs = roundTripMs, IcmpStatus = status.ToString() };
        }
    }
}