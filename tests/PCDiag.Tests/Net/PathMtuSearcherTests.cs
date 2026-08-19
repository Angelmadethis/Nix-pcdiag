using PCDiag.Net;

namespace PCDiag.Tests.Net;

public class PathMtuSearcherTests
{
    private static readonly MtuOptions Options = MtuOptions.Default;

    private static Task<PathMtuResult> Measure(
        Func<int, CancellationToken, Task<PingProbeResult>> probe,
        int maxPayload = 1472,
        int minPayload = 68,
        int confirmations = 2)
        => PathMtuSearcher.MeasureAsync(minPayload, maxPayload, confirmations, probe, CancellationToken.None);

    private static Func<int, CancellationToken, Task<PingProbeResult>> Search(Func<int, PingProbeResult> outcome)
        => (payload, _) => Task.FromResult(outcome(payload));

    private static PingProbeResult ByMtu(int payload, int pathMtu)
        => payload + MtuOptions.IcmpIpv4Overhead <= pathMtu ? PingResults.Success() : PingResults.Frag();

    [Fact]
    public async Task CooperativeFullMtu_ShouldDetectInterfaceLimit()
    {
        var result = await Measure(Search(p => ByMtu(p, 1500)));

        Assert.Equal(1472, result.MaxPayloadSucceeded);
        Assert.Equal(1500, result.DetectedPathMtu);
        Assert.True(result.BoundaryConfirmed);
        Assert.False(result.SawFragmentationNeeded);
        Assert.False(result.SawBlackHole);
    }

    [Fact]
    public async Task CooperativePppoeMtu_ShouldDetect1492()
    {
        var result = await Measure(Search(p => ByMtu(p, 1492)));

        Assert.Equal(1464, result.MaxPayloadSucceeded);
        Assert.Equal(1492, result.DetectedPathMtu);
        Assert.True(result.BoundaryConfirmed);
    }

    [Fact]
    public async Task CooperativeReducedPath_ShouldDetectLowerMtu()
    {
        var result = await Measure(Search(p => ByMtu(p, 1400)));

        Assert.Equal(1372, result.MaxPayloadSucceeded);
        Assert.Equal(1400, result.DetectedPathMtu);
        Assert.True(result.BoundaryConfirmed);
        Assert.True(result.SawFragmentationNeeded);
        Assert.False(result.SawBlackHole);
    }

    [Fact]
    public async Task BlackHole_ShouldDetectLowerMtuAndFlagBlackHole()
    {
        var result = await Measure(Search(p => p + MtuOptions.IcmpIpv4Overhead <= 1400 ? PingResults.Success() : PingResults.Timeout()));

        Assert.Equal(1372, result.MaxPayloadSucceeded);
        Assert.Equal(1400, result.DetectedPathMtu);
        Assert.True(result.SawBlackHole);
        Assert.False(result.SawFragmentationNeeded);
        Assert.True(result.BoundaryConfirmed);
    }

    [Fact]
    public async Task DeadTarget_ShouldReportNoMeasurement()
    {
        var result = await Measure(Search(_ => PingResults.Timeout()));

        Assert.Equal(0, result.MaxPayloadSucceeded);
        Assert.Null(result.DetectedPathMtu);
        Assert.False(result.BoundaryConfirmed);
        Assert.False(result.SawBlackHole);
    }

    [Fact]
    public async Task SearchRange_ShouldBeBounded()
    {
        var result = await Measure(Search(_ => PingResults.Timeout()));

        // ~log2(1472-68) iterations + confirmations; must stay small and fixed.
        Assert.True(result.ProbeCount <= 14, $"probe count was {result.ProbeCount}");
        Assert.True(result.ProbeCount >= 10, $"probe count was {result.ProbeCount}");
    }

    [Fact]
    public async Task RespectsCustomMaxPayload_FromInterfaceMtu()
    {
        // Interface MTU 1492 => payloads searched only up to 1464.
        var result = await Measure(Search(p => ByMtu(p, 1500)), maxPayload: 1464);

        Assert.Equal(1464, result.MaxPayloadSucceeded);
        Assert.Equal(1492, result.DetectedPathMtu);
    }

    [Fact]
    public async Task UnconfirmedBoundary_WhenConfirmationFails()
    {
        // Boundary succeeds once during the search but confirmation probes fail.
        var calls = new List<int>();
        var result = await Measure((payload, ct) =>
        {
            calls.Add(payload);
            return Task.FromResult(
                payload + MtuOptions.IcmpIpv4Overhead <= 1400
                    ? (calls.Count(p => p == payload) == 1 ? PingResults.Success() : PingResults.Timeout())
                    : PingResults.Frag());
        });

        Assert.Equal(1372, result.MaxPayloadSucceeded);
        Assert.Equal(1400, result.DetectedPathMtu);
        Assert.False(result.BoundaryConfirmed);
    }
}