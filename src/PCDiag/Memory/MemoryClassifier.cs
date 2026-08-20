namespace PCDiag.Memory;

public enum MemoryVerdict
{
    Healthy = 0,
    Suspicious = 1,
    Warning = 2,
    Critical = 3
}

public enum MemoryFlag
{
    CommitElevated,
    CommitHigh,
    LowAvailable,
    CriticalAvailable,
    AbsoluteLowAvailable,
    HeavyPaging,
    OperatingSystemInfoUnavailable,
    PerfCountersUnavailable,
    PagefileUsageUnavailable
}

public sealed record MemoryAssessment(
    MemoryVerdict Verdict,
    IReadOnlyList<MemoryFlag> Flags,
    double? CommitRatio,
    double? AvailablePercent,
    double? PagesPerSecond);

/// <summary>
/// Pure classifier for memory state. Commit ratio and available-memory fraction are
/// contextual; unavailable counters are reported as flags, never fabricated. Low
/// available memory is treated as pressure (a symptom), not a diagnosis.
/// </summary>
public static class MemoryClassifier
{
    public static MemoryAssessment Classify(MemorySnapshot snapshot, MemoryOptions options)
    {
        var flags = new List<MemoryFlag>();
        var verdict = MemoryVerdict.Healthy;

        double? commitRatio = snapshot.CommittedBytes is long c && snapshot.CommitLimitBytes is long l && l > 0
            ? (double)c / l
            : null;
        double? availablePercent = snapshot.AvailableBytes is long a && snapshot.TotalPhysicalBytes is long t && t > 0
            ? (double)a / t
            : null;

        if (commitRatio is double cr)
        {
            if (cr >= options.CommitWarningRatio)
            {
                flags.Add(MemoryFlag.CommitHigh);
                verdict = Worst(verdict, MemoryVerdict.Warning);
            }
            else if (cr >= options.CommitSuspiciousRatio)
            {
                flags.Add(MemoryFlag.CommitElevated);
                verdict = Worst(verdict, MemoryVerdict.Suspicious);
            }
        }

        if (availablePercent is double ap)
        {
            if (ap < options.AvailableWarningFraction)
            {
                flags.Add(MemoryFlag.CriticalAvailable);
                verdict = Worst(verdict, MemoryVerdict.Warning);
            }
            else if (ap < options.AvailableSuspiciousFraction)
            {
                flags.Add(MemoryFlag.LowAvailable);
                verdict = Worst(verdict, MemoryVerdict.Suspicious);
            }

            if (snapshot.AvailableBytes is long ab && ab < options.AbsoluteLowAvailableBytes)
            {
                flags.Add(MemoryFlag.AbsoluteLowAvailable);
                verdict = Worst(verdict, MemoryVerdict.Suspicious);
            }
        }

        if (snapshot.PagesPerSecond is long pp && pp >= options.HeavyPagingPerSecond)
        {
            flags.Add(MemoryFlag.HeavyPaging);
            verdict = Worst(verdict, MemoryVerdict.Suspicious);
        }

        if (!snapshot.OperatingSystemInfoAvailable)
            flags.Add(MemoryFlag.OperatingSystemInfoUnavailable);
        if (!snapshot.PerfCountersAvailable)
            flags.Add(MemoryFlag.PerfCountersUnavailable);
        if (!snapshot.PagefileUsageAvailable)
            flags.Add(MemoryFlag.PagefileUsageUnavailable);

        return new MemoryAssessment(verdict, flags, commitRatio, availablePercent, snapshot.PagesPerSecond);
    }

    private static MemoryVerdict Worst(MemoryVerdict current, MemoryVerdict candidate)
        => candidate > current ? candidate : current;
}