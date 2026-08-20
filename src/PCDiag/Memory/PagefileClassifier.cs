namespace PCDiag.Memory;

public enum PagefileVerdict
{
    Healthy = 0,
    Suspicious = 1,
    Warning = 2
}

public enum PagefileFlag
{
    SystemManaged,
    NoPagefile,
    UsageNearAllocated,
    PeakHigh,
    ConfigUnavailable,
    UsageUnavailable
}

public sealed record PagefileAssessment(
    PagefileVerdict Verdict,
    IReadOnlyList<PagefileFlag> Flags);

/// <summary>
/// Pure classifier for pagefile state. A disabled pagefile is Suspicious (a deliberate
/// choice for some users, but a real stability risk under memory pressure) - never
/// Critical and never "must enable". A system-managed pagefile that grows automatically
/// is not flagged for high peak usage; a fixed-size pagefile cannot grow, so high usage
/// or a high peak there is meaningful.
/// </summary>
public static class PagefileClassifier
{
    public static PagefileAssessment Classify(PagefileInfo info, MemoryOptions options)
    {
        var flags = new List<PagefileFlag>();
        var verdict = PagefileVerdict.Healthy;

        if (info.Config is not null)
        {
            if (info.Config.IsSystemManaged)
            {
                flags.Add(PagefileFlag.SystemManaged);
            }
            else if (info.Config.Entries.Count == 0)
            {
                flags.Add(PagefileFlag.NoPagefile);
                verdict = Worst(verdict, PagefileVerdict.Suspicious);
            }
        }
        else
        {
            flags.Add(PagefileFlag.ConfigUnavailable);
        }

        if (info.UsageAvailable)
        {
            var hasCustomFixedSize = info.Config is not null && !info.Config.IsSystemManaged && info.Config.Entries.Count > 0;

            if (hasCustomFixedSize)
            {
                foreach (var entry in info.Usage)
                {
                    if (entry.AllocatedBytes is long allocated && allocated > 0)
                    {
                        if (entry.CurrentBytes is long current && (double)current / allocated >= options.PagefileUsageNearFullRatio)
                        {
                            flags.Add(PagefileFlag.UsageNearAllocated);
                            verdict = Worst(verdict, PagefileVerdict.Suspicious);
                        }

                        if (entry.PeakBytes is long peak && (double)peak / allocated >= options.PagefilePeakHighRatio)
                        {
                            flags.Add(PagefileFlag.PeakHigh);
                            verdict = Worst(verdict, PagefileVerdict.Suspicious);
                        }
                    }
                }
            }
        }
        else
        {
            flags.Add(PagefileFlag.UsageUnavailable);
        }

        return new PagefileAssessment(verdict, flags);
    }

    private static PagefileVerdict Worst(PagefileVerdict current, PagefileVerdict candidate)
        => candidate > current ? candidate : current;
}