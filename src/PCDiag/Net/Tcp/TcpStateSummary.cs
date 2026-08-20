namespace PCDiag.Net.Tcp;

/// <summary>
/// Aggregated counts of a TCP connection snapshot plus per-process breakdowns that
/// put raw counts into context (a leak shows up as one PID owning many CLOSE_WAIT
/// sockets; port pressure shows up in <see cref="DistinctLocalPorts"/>).
/// </summary>
public sealed record TcpStateSummary
{
    public int Total { get; init; }
    public int Listen { get; init; }
    public int Established { get; init; }
    public int TimeWait { get; init; }
    public int CloseWait { get; init; }
    public int Bound { get; init; }
    public int SynSent { get; init; }
    public int Other { get; init; }

    /// <summary>Top processes (PID, count) holding CLOSE_WAIT sockets, descending.</summary>
    public IReadOnlyList<(int ProcessId, int Count)> CloseWaitByProcess { get; init; } = Array.Empty<(int, int)>();

    /// <summary>Top processes (PID, count) holding established sockets, descending.</summary>
    public IReadOnlyList<(int ProcessId, int Count)> EstablishedByProcess { get; init; } = Array.Empty<(int, int)>();

    /// <summary>
    /// Number of distinct local ports in use inside the dynamic port range. When the
    /// range is not supplied, counts every distinct local port.
    /// </summary>
    public int DistinctLocalPorts { get; init; }

    public static TcpStateSummary Compute(
        IReadOnlyList<TcpConnectionRecord> connections,
        int dynamicPortStart = 0,
        int dynamicPortCount = 0)
    {
        int listen = 0, established = 0, timeWait = 0, closeWait = 0, bound = 0, synSent = 0, other = 0;
        var closeWaitPids = new Dictionary<int, int>();
        var establishedPids = new Dictionary<int, int>();
        var localPorts = new HashSet<int>();

        foreach (var c in connections)
        {
            switch (c.State)
            {
                case TcpConnectionState.Listen:
                    listen++;
                    break;
                case TcpConnectionState.Established:
                    established++;
                    Add(establishedPids, c.OwningProcess);
                    break;
                case TcpConnectionState.TimeWait:
                    timeWait++;
                    break;
                case TcpConnectionState.CloseWait:
                    closeWait++;
                    Add(closeWaitPids, c.OwningProcess);
                    break;
                case TcpConnectionState.Bound:
                    bound++;
                    break;
                case TcpConnectionState.SynSent:
                case TcpConnectionState.SynReceived:
                    synSent++;
                    break;
                default:
                    other++;
                    break;
            }

            if (c.LocalPort > 0 && c.State is not (TcpConnectionState.Closed or TcpConnectionState.DeleteTcb))
            {
                if (dynamicPortCount > 0 &&
                    (c.LocalPort < dynamicPortStart || c.LocalPort >= dynamicPortStart + dynamicPortCount))
                    continue;
                localPorts.Add(c.LocalPort);
            }
        }

        return new TcpStateSummary
        {
            Total = connections.Count,
            Listen = listen,
            Established = established,
            TimeWait = timeWait,
            CloseWait = closeWait,
            Bound = bound,
            SynSent = synSent,
            Other = other,
            CloseWaitByProcess = TopByProcess(closeWaitPids),
            EstablishedByProcess = TopByProcess(establishedPids),
            DistinctLocalPorts = localPorts.Count
        };
    }

    private static void Add(Dictionary<int, int> map, int pid)
    {
        if (pid <= 0)
            return;
        map.TryGetValue(pid, out var count);
        map[pid] = count + 1;
    }

    private static IReadOnlyList<(int, int)> TopByProcess(Dictionary<int, int> map)
        => map
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();
}