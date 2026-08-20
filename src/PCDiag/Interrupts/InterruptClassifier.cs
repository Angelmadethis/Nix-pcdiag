namespace PCDiag.Interrupts;

public enum InterruptVerdict
{
    Healthy = 0,
    Suspicious = 1,
    Warning = 2
}

public enum InterruptFlag
{
    ElevatedInterruptRate,
    HighInterruptRate,
    ElevatedDpcRate,
    HighDpcRate,
    HighPrivilegedTime,
    VeryHighPrivilegedTime,
    ConcentratedInterruptLoad,
    CountersUnavailable,
    TopologyUnavailable
}

public sealed record InterruptAssessment(
    InterruptVerdict Verdict,
    IReadOnlyList<InterruptFlag> Flags);

/// <summary>
/// Pure classifier for interrupt/DPC activity. Rates are compared against conservative
/// heuristic thresholds; the verdict reflects how many independent signals agree, and
/// unreadable counters are reported as flags, never fabricated. This classifier judges
/// activity rates only - true per-DPC latency is explicitly out of scope.
/// </summary>
public static class InterruptClassifier
{
    public static InterruptAssessment Classify(InterruptSnapshot snapshot, InterruptOptions options)
    {
        var flags = new List<InterruptFlag>();
        var verdict = InterruptVerdict.Healthy;

        var total = snapshot.Total;
        var haveCounters = total is not null
                           && (total.InterruptsPerSecond is not null || total.DpcsPerSecond is not null);
        if (!haveCounters)
        {
            flags.Add(InterruptFlag.CountersUnavailable);
            return new InterruptAssessment(verdict, flags);
        }

        if (snapshot.Cores.Count == 0)
            flags.Add(InterruptFlag.TopologyUnavailable);

        if (total!.InterruptsPerSecond is double interrupts)
        {
            if (interrupts >= options.InterruptsPerSecondWarning)
            {
                flags.Add(InterruptFlag.HighInterruptRate);
                verdict = Worst(verdict, InterruptVerdict.Warning);
            }
            else if (interrupts >= options.InterruptsPerSecondSuspicious)
            {
                flags.Add(InterruptFlag.ElevatedInterruptRate);
                verdict = Worst(verdict, InterruptVerdict.Suspicious);
            }
        }

        if (total.DpcsPerSecond is double dpcs)
        {
            if (dpcs >= options.DpcsPerSecondWarning)
            {
                flags.Add(InterruptFlag.HighDpcRate);
                verdict = Worst(verdict, InterruptVerdict.Warning);
            }
            else if (dpcs >= options.DpcsPerSecondSuspicious)
            {
                flags.Add(InterruptFlag.ElevatedDpcRate);
                verdict = Worst(verdict, InterruptVerdict.Suspicious);
            }
        }

        if (total.PrivilegedPercent is double privileged)
        {
            if (privileged >= options.PrivilegedTimeWarning)
            {
                flags.Add(InterruptFlag.VeryHighPrivilegedTime);
                verdict = Worst(verdict, InterruptVerdict.Warning);
            }
            else if (privileged >= options.PrivilegedTimeSuspicious)
            {
                flags.Add(InterruptFlag.HighPrivilegedTime);
                verdict = Worst(verdict, InterruptVerdict.Suspicious);
            }
        }

        if (HasConcentratedInterruptLoad(snapshot.Cores, options))
        {
            flags.Add(InterruptFlag.ConcentratedInterruptLoad);
            verdict = Worst(verdict, InterruptVerdict.Suspicious);
        }

        return new InterruptAssessment(verdict, flags);
    }

    /// <summary>
    /// Confidence in the verdict, weighted by how many independent activity signals
    /// agree and by data availability. A healthy verdict on a full counter set is the
    /// most confident; a finding based on a single signal is the least confident.
    /// </summary>
    public static double ComputeConfidence(InterruptAssessment assessment)
    {
        if (assessment.Flags.Contains(InterruptFlag.CountersUnavailable))
            return 0.4;

        if (assessment.Verdict == InterruptVerdict.Healthy)
            return 0.9;

        var signals = 0;
        if (assessment.Flags.Any(f => f is InterruptFlag.ElevatedInterruptRate or InterruptFlag.HighInterruptRate))
            signals++;
        if (assessment.Flags.Any(f => f is InterruptFlag.ElevatedDpcRate or InterruptFlag.HighDpcRate))
            signals++;
        if (assessment.Flags.Any(f => f is InterruptFlag.HighPrivilegedTime or InterruptFlag.VeryHighPrivilegedTime))
            signals++;
        if (assessment.Flags.Contains(InterruptFlag.ConcentratedInterruptLoad))
            signals++;

        var confidence = 0.5 + (signals - 1) * 0.08;
        if (assessment.Verdict == InterruptVerdict.Warning)
            confidence += 0.05;

        return Math.Clamp(confidence, 0.05, 0.95);
    }

    private static bool HasConcentratedInterruptLoad(
        IReadOnlyList<InterruptCoreSample> cores,
        InterruptOptions options)
    {
        var rates = cores
            .Select(c => c.InterruptsPerSecond)
            .Where(v => v is not null)
            .Cast<double>()
            .OrderBy(v => v)
            .ToList();

        if (rates.Count == 0)
            return false;

        var max = rates[^1];
        if (max < options.ConcentrationFloorPerSecond)
            return false;

        var median = Median(rates);
        return max >= options.ConcentrationFactor * median;
    }

    private static double Median(IReadOnlyList<double> values)
    {
        var n = values.Count;
        return n % 2 == 1 ? values[n / 2] : (values[n / 2 - 1] + values[n / 2]) / 2;
    }

    private static InterruptVerdict Worst(InterruptVerdict current, InterruptVerdict candidate)
        => candidate > current ? candidate : current;
}