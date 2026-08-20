namespace PCDiag.Net.Tcp;

public enum TcpConnectionsHealth
{
    Healthy = 0,
    Elevated = 1,
    Warning = 2
}

public enum TcpConnectionsFlag
{
    TimeWaitElevated,
    TimeWaitHigh,
    CloseWaitCluster,
    CloseWaitSingleProcess,
    EstablishedElevated,
    EstablishedHigh
}

public sealed record TcpConnectionsAssessment(
    TcpConnectionsHealth Health,
    IReadOnlyList<TcpConnectionsFlag> Flags);

/// <summary>
/// Interprets a connection snapshot in context. A high TIME_WAIT count is NOT treated
/// as automatically bad: it is judged against the size of the dynamic port pool, and
/// only counts approaching exhaustion become a warning. CLOSE_WAIT is judged both on
/// total sockets and on per-process ownership (a leak concentrates in one PID).
/// </summary>
public static class TcpConnectionsClassifier
{
    public static TcpConnectionsAssessment Classify(
        TcpStateSummary summary,
        TcpOptions options,
        int? dynamicPortCount)
    {
        var flags = new List<TcpConnectionsFlag>();
        var health = TcpConnectionsHealth.Healthy;

        var ports = dynamicPortCount is int p && p > 0 ? p : options.TimeWaitPortPoolFallback;
        var timeWaitFraction = ports > 0 ? (double)summary.TimeWait / ports : 0;
        if (timeWaitFraction >= options.TimeWaitWarningPortFraction)
        {
            flags.Add(TcpConnectionsFlag.TimeWaitHigh);
            health = Worst(health, TcpConnectionsHealth.Warning);
        }
        else if (timeWaitFraction >= options.TimeWaitElevatedPortFraction)
        {
            flags.Add(TcpConnectionsFlag.TimeWaitElevated);
            health = Worst(health, TcpConnectionsHealth.Elevated);
        }

        if (summary.CloseWait > options.CloseWaitWarning)
        {
            flags.Add(TcpConnectionsFlag.CloseWaitCluster);
            health = Worst(health, TcpConnectionsHealth.Warning);
        }
        else if (summary.CloseWait > options.CloseWaitSuspicious)
        {
            flags.Add(TcpConnectionsFlag.CloseWaitCluster);
            health = Worst(health, TcpConnectionsHealth.Elevated);
        }
        if (summary.CloseWaitByProcess.Count > 0 && summary.CloseWaitByProcess[0].Count > options.CloseWaitPerProcessSuspicious)
        {
            flags.Add(TcpConnectionsFlag.CloseWaitSingleProcess);
            health = Worst(health, TcpConnectionsHealth.Elevated);
        }

        if (summary.Established > options.EstablishedWarning)
        {
            flags.Add(TcpConnectionsFlag.EstablishedHigh);
            health = Worst(health, TcpConnectionsHealth.Warning);
        }
        else if (summary.Established > options.EstablishedSuspicious)
        {
            flags.Add(TcpConnectionsFlag.EstablishedElevated);
            health = Worst(health, TcpConnectionsHealth.Elevated);
        }

        return new TcpConnectionsAssessment(health, flags);
    }

    private static TcpConnectionsHealth Worst(TcpConnectionsHealth current, TcpConnectionsHealth candidate)
        => candidate > current ? candidate : current;
}