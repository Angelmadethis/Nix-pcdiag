namespace PCDiag.Storage;

public enum StorageVerdict
{
    Healthy = 0,
    Suspicious = 1,
    Warning = 2,
    Critical = 3
}

public enum StorageFlag
{
    LowFreeSpace,
    CriticalFreeSpace,
    DirtyVolume,
    StackHealthWarning,
    StackHealthUnhealthy,
    StackHealthUnknown,
    ReliabilityUnavailable,
    WearHigh,
    TemperatureHigh,
    UncorrectedErrors,
    SlowLatency,
    VerySlowLatency,
    LatencyIdle,
    VolumesUnavailable,
    DisksUnavailable,
    HealthUnavailable
}

public sealed record StorageAssessment(
    StorageVerdict Verdict,
    IReadOnlyList<StorageFlag> Flags);

/// <summary>
/// Pure classifier for storage state. Free-space and health findings are contextual;
/// a drive with unknown SMART/NVMe data is reported as "not independently verified"
/// (flag ReliabilityUnavailable / StackHealthUnknown), never claimed perfect.
/// Latency is only judged when there was I/O during the sample window - an idle disk
/// is reported as idle, not slow.
/// </summary>
public static class StorageClassifier
{
    public static StorageAssessment Classify(StorageSnapshot snapshot, StorageOptions options)
    {
        var flags = new List<StorageFlag>();
        var verdict = StorageVerdict.Healthy;

        if (!snapshot.VolumesAvailable)
            flags.Add(StorageFlag.VolumesUnavailable);
        if (!snapshot.DisksAvailable)
            flags.Add(StorageFlag.DisksUnavailable);

        foreach (var volume in snapshot.Volumes)
        {
            if (volume.FreeFraction is double free)
            {
                if (free < options.CriticalFreeSpaceFraction)
                {
                    flags.Add(StorageFlag.CriticalFreeSpace);
                    verdict = Worst(verdict, StorageVerdict.Warning);
                }
                else if (free < options.LowFreeSpaceFraction)
                {
                    flags.Add(StorageFlag.LowFreeSpace);
                    verdict = Worst(verdict, StorageVerdict.Suspicious);
                }
            }

            if (volume.IsDirty == true)
            {
                flags.Add(StorageFlag.DirtyVolume);
                verdict = Worst(verdict, StorageVerdict.Warning);
            }
        }

        foreach (var disk in snapshot.Disks)
        {
            var health = disk.Health;

            if (!health.StackQueried)
            {
                flags.Add(StorageFlag.HealthUnavailable);
                continue;
            }

            switch (health.StackState)
            {
                case StorageHealthState.Warning:
                    flags.Add(StorageFlag.StackHealthWarning);
                    verdict = Worst(verdict, StorageVerdict.Suspicious);
                    break;
                case StorageHealthState.Unhealthy:
                    flags.Add(StorageFlag.StackHealthUnhealthy);
                    verdict = Worst(verdict, StorageVerdict.Warning);
                    break;
                case StorageHealthState.Unknown:
                    flags.Add(StorageFlag.StackHealthUnknown);
                    break;
            }

            if (!health.HasReliabilityCounters)
            {
                flags.Add(StorageFlag.ReliabilityUnavailable);
            }
            else
            {
                if (health.WearPercent is int wear && wear >= options.WearWarningPercent)
                {
                    flags.Add(StorageFlag.WearHigh);
                    verdict = Worst(verdict, StorageVerdict.Warning);
                }

                if (health.TemperatureCelsius is int temp && temp >= options.TemperatureSuspiciousCelsius)
                {
                    flags.Add(StorageFlag.TemperatureHigh);
                    verdict = Worst(verdict, StorageVerdict.Suspicious);
                }

                var uncorrected = (health.ReadErrorsUncorrected ?? 0) + (health.WriteErrorsUncorrected ?? 0);
                if (uncorrected > 0)
                {
                    flags.Add(StorageFlag.UncorrectedErrors);
                    verdict = Worst(verdict, StorageVerdict.Warning);
                }
            }
        }

        var anyActive = false;
        foreach (var sample in snapshot.Latency)
        {
            if (!sample.HadIoActivity)
            {
                flags.Add(StorageFlag.LatencyIdle);
                continue;
            }

            anyActive = true;
            var maxLatency = Math.Max(sample.AverageReadSeconds ?? 0, sample.AverageWriteSeconds ?? 0);
            if (maxLatency >= options.VerySlowLatencySeconds)
            {
                flags.Add(StorageFlag.VerySlowLatency);
                verdict = Worst(verdict, StorageVerdict.Warning);
            }
            else if (maxLatency >= options.SlowLatencySeconds)
            {
                flags.Add(StorageFlag.SlowLatency);
                verdict = Worst(verdict, StorageVerdict.Suspicious);
            }
        }

        if (!anyActive && snapshot.Latency.Count > 0 && !flags.Contains(StorageFlag.LatencyIdle))
            flags.Add(StorageFlag.LatencyIdle);

        return new StorageAssessment(verdict, flags);
    }

    private static StorageVerdict Worst(StorageVerdict current, StorageVerdict candidate)
        => candidate > current ? candidate : current;
}