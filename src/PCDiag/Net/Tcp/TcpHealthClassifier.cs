namespace PCDiag.Net.Tcp;

public enum TcpHealthVerdict
{
    Healthy = 0,
    Suspicious = 1,
    Warning = 2
}

public enum TcpHealthFlag
{
    RetransmissionUnavailable,
    RetransmissionElevated,
    RetransmissionHigh,
    FailureRatioUnavailable,
    ConnectionFailuresElevated,
    ConnectionFailuresHigh,
    AutotuningDisabled,
    AutotuningRestricted,
    AutotuningGroupPolicy,
    TcpTimedWaitDelayLow,
    MaxUserPortLow,
    WindowSizeOverridesAutotuning,
    UptimeUnknown,
    AdapterErrorsElevated,
    AdapterErrorsHigh
}

public sealed record TcpHealthAssessment(
    TcpHealthVerdict Verdict,
    IReadOnlyList<TcpHealthFlag> Flags);

/// <summary>
/// Combines cumulative statistics, configuration, and adapter error counters into a
/// single verdict. Ratios (failures/initiations, retransmitted/segments) keep values
/// contextual; unavailable counters are reported as such and never fabricated.
/// Registry values are only ever read - no tweaks are applied or recommended as fixes.
/// </summary>
public static class TcpHealthClassifier
{
    public static TcpHealthAssessment Classify(
        TcpCumulativeStats stats,
        TcpConfiguration config,
        double? adapterErrorRatePerSecond,
        TcpOptions options)
    {
        var flags = new List<TcpHealthFlag>();
        var verdict = TcpHealthVerdict.Healthy;

        if (stats.RetransmissionRatio is double rr)
        {
            if (rr >= options.RetransmissionWarningRatio)
            {
                flags.Add(TcpHealthFlag.RetransmissionHigh);
                verdict = Worst(verdict, TcpHealthVerdict.Warning);
            }
            else if (rr >= options.RetransmissionSuspiciousRatio)
            {
                flags.Add(TcpHealthFlag.RetransmissionElevated);
                verdict = Worst(verdict, TcpHealthVerdict.Suspicious);
            }
        }
        else if (stats.SegmentsSent + stats.SegmentsReceived == 0)
        {
            flags.Add(TcpHealthFlag.RetransmissionUnavailable);
        }

        if (stats.FailureRatio is double fr)
        {
            if (fr >= options.FailureWarningRatio)
            {
                flags.Add(TcpHealthFlag.ConnectionFailuresHigh);
                verdict = Worst(verdict, TcpHealthVerdict.Warning);
            }
            else if (fr >= options.FailureSuspiciousRatio)
            {
                flags.Add(TcpHealthFlag.ConnectionFailuresElevated);
                verdict = Worst(verdict, TcpHealthVerdict.Suspicious);
            }
        }
        else
        {
            flags.Add(TcpHealthFlag.FailureRatioUnavailable);
        }

        switch (config.AutotuningLevel)
        {
            case TcpAutotuningLevel.Disabled:
                flags.Add(TcpHealthFlag.AutotuningDisabled);
                verdict = Worst(verdict, TcpHealthVerdict.Suspicious);
                break;
            case TcpAutotuningLevel.Experimental:
            case TcpAutotuningLevel.Restricted:
            case TcpAutotuningLevel.HighlyRestricted:
                flags.Add(TcpHealthFlag.AutotuningRestricted);
                verdict = Worst(verdict, TcpHealthVerdict.Suspicious);
                break;
        }

        if (config.AutotuningGroupPolicy != TcpAutotuningLevel.Unknown)
            flags.Add(TcpHealthFlag.AutotuningGroupPolicy);

        if (config.TcpTimedWaitDelay is int twd && twd < 30)
        {
            flags.Add(TcpHealthFlag.TcpTimedWaitDelayLow);
            verdict = Worst(verdict, TcpHealthVerdict.Suspicious);
        }

        if (config.MaxUserPort is int mup && mup < 5000)
        {
            flags.Add(TcpHealthFlag.MaxUserPortLow);
            verdict = Worst(verdict, TcpHealthVerdict.Warning);
        }

        if (config.TcpWindowSize is not null || config.GlobalMaxTcpWindowSize is not null)
        {
            flags.Add(TcpHealthFlag.WindowSizeOverridesAutotuning);
            verdict = Worst(verdict, TcpHealthVerdict.Suspicious);
        }

        if (adapterErrorRatePerSecond is double rate)
        {
            if (rate >= options.AdapterErrorWarningPerSecond)
            {
                flags.Add(TcpHealthFlag.AdapterErrorsHigh);
                verdict = Worst(verdict, TcpHealthVerdict.Warning);
            }
            else if (rate >= options.AdapterErrorSuspiciousPerSecond)
            {
                flags.Add(TcpHealthFlag.AdapterErrorsElevated);
                verdict = Worst(verdict, TcpHealthVerdict.Suspicious);
            }
        }
        else
        {
            flags.Add(TcpHealthFlag.UptimeUnknown);
        }

        return new TcpHealthAssessment(verdict, flags);
    }

    private static TcpHealthVerdict Worst(TcpHealthVerdict current, TcpHealthVerdict candidate)
        => candidate > current ? candidate : current;
}