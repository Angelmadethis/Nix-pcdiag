using PCDiag.Net.Tcp;

namespace PCDiag.Tests.Net.Tcp;

public class TcpStateSummaryTests
{
    [Fact]
    public void Compute_MixedStates_ShouldCountCorrectly()
    {
        var connections = new[]
        {
            TcpConn.Listen(80, 4),
            TcpConn.Established(50000, 1000),
            TcpConn.Established(50001, 1000),
            TcpConn.Established(50002, 2000),
            TcpConn.TimeWait(50003),
            TcpConn.TimeWait(50004),
            TcpConn.CloseWait(50005, 3000),
            TcpConn.Bound(50006),
            new TcpConnectionRecord(TcpConnectionState.SynSent, "192.168.1.50", 50007, "203.0.113.10", 443, 0),
            new TcpConnectionRecord(TcpConnectionState.FinWait2, "192.168.1.50", 50008, "203.0.113.10", 443, 0)
        };

        var summary = TcpStateSummary.Compute(connections, 49152, 16384);

        Assert.Equal(10, summary.Total);
        Assert.Equal(1, summary.Listen);
        Assert.Equal(3, summary.Established);
        Assert.Equal(2, summary.TimeWait);
        Assert.Equal(1, summary.CloseWait);
        Assert.Equal(1, summary.Bound);
        Assert.Equal(1, summary.SynSent);
        Assert.Equal(1, summary.Other);
    }

    [Fact]
    public void Compute_CloseWaitByProcess_ShouldGroupAndSortDescending()
    {
        var connections = new[]
        {
            TcpConn.CloseWait(50000, 3000),
            TcpConn.CloseWait(50001, 1000),
            TcpConn.CloseWait(50002, 3000),
            TcpConn.CloseWait(50003, 3000),
            TcpConn.CloseWait(50004, 1000)
        };

        var summary = TcpStateSummary.Compute(connections);

        Assert.Equal(5, summary.CloseWait);
        Assert.Collection(summary.CloseWaitByProcess,
            top => { Assert.Equal(3000, top.ProcessId); Assert.Equal(3, top.Count); },
            next => { Assert.Equal(1000, next.ProcessId); Assert.Equal(2, next.Count); });
    }

    [Fact]
    public void Compute_EstablishedByProcess_ShouldGroup()
    {
        var connections = new[]
        {
            TcpConn.Established(50000, 1000),
            TcpConn.Established(50001, 1000),
            TcpConn.Established(50002, 2000)
        };

        var summary = TcpStateSummary.Compute(connections);

        Assert.Equal(2, summary.EstablishedByProcess[0].Count);
        Assert.Equal(1, summary.EstablishedByProcess[1].Count);
    }

    [Fact]
    public void Compute_ZeroPid_ShouldNotAppearInProcessBreakdown()
    {
        var connections = new[] { TcpConn.CloseWait(50000, 0), TcpConn.Established(50001, 0) };

        var summary = TcpStateSummary.Compute(connections);

        Assert.Empty(summary.CloseWaitByProcess);
        Assert.Empty(summary.EstablishedByProcess);
    }

    [Fact]
    public void Compute_DynamicRange_ShouldCountOnlyInRangePorts()
    {
        var connections = new[]
        {
            TcpConn.TimeWait(50000),
            TcpConn.TimeWait(80),
            TcpConn.Established(50001, 1000)
        };

        var summary = TcpStateSummary.Compute(connections, 49152, 16384);

        Assert.Equal(2, summary.DistinctLocalPorts);
    }

    [Fact]
    public void Compute_NoDynamicRange_ShouldCountAllDistinctPorts()
    {
        var connections = new[]
        {
            TcpConn.TimeWait(50000),
            TcpConn.TimeWait(50000),
            TcpConn.TimeWait(80)
        };

        var summary = TcpStateSummary.Compute(connections);

        Assert.Equal(2, summary.DistinctLocalPorts);
    }
}