using PCDiag.Core;
using PCDiag.Events;
using PCDiag.Infrastructure;
using PCDiag.Storage;

namespace PCDiag.Checks.Performance;

/// <summary>
/// Inspects storage: volumes and free space, filesystems, physical disks, storage-stack
/// health and SMART/NVMe reliability counters where available, recent disk errors from
/// the event log, and a short passive latency sample. Read-only; no destructive tests
/// and no drive benchmarking. A drive whose SMART/NVMe data is unavailable is reported
/// as "not independently verified", never as healthy on missing data.
/// </summary>
public sealed class StorageCheck : DiagnosticCheck
{
    private readonly IStorageInfoSource _storage;
    private readonly IEventLogSource? _eventSource;
    private readonly StorageOptions _options;
    private readonly EventLogOptions _eventOptions;

    public override string CheckId => "PERF-DISK-001";
    public override string Name => "Storage & Disk Health";
    public override DiagnosticCategory Category => DiagnosticCategory.Performance;
    public override string Description =>
        "Inspects volumes, free space, disk health (SMART/NVMe where available), recent disk errors, and disk latency.";

    public StorageCheck(
        IStorageInfoSource? storage = null,
        IEventLogSource? eventSource = null,
        StorageOptions? options = null,
        EventLogOptions? eventOptions = null)
    {
        _storage = storage ?? new WmiStorageInfoSource();
        _eventSource = eventSource;
        _options = options ?? StorageOptions.Default;
        _eventOptions = eventOptions ?? EventLogOptions.Default;
    }

    private static readonly EventCategory[] DiskEventCategories =
        { EventCategory.Disk, EventCategory.Ntfs, EventCategory.StorageController };

    private static readonly IReadOnlyList<string> CheckLimitations = new[]
    {
        "Read-only: no destructive tests are performed and no repairs are applied.",
        "Disk latency is sampled passively over a short window - it is not a load test and the drive is never benchmarked.",
        "SMART/NVMe reliability data (wear, temperature, uncorrectable errors) may not be available; when unavailable, drive health is reported as not independently verified.",
        "Only the last 14 days of disk events are inspected.",
        "Findings describe observed state, never a root cause."
    };

    protected override async Task<DiagnosticResult> RunAsync(DiagnosticContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = await _storage.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var analysis = await Task.Run(
            () => new EventLogAnalyzer(_eventSource, _eventOptions).Analyze(DiskEventCategories, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        var anythingRead = snapshot.VolumesAvailable || snapshot.DisksAvailable || analysis.TotalEvents > 0
                           || snapshot.StorageNamespaceAvailable;
        if (!anythingRead)
        {
            return Unavailable("Storage data could not be read on this system.");
        }

        var assessment = StorageClassifier.Classify(snapshot, _options);
        var storageSeverity = ToSeverity(assessment.Verdict);
        var severity = (DiagnosticSeverity)Math.Max((int)storageSeverity, (int)analysis.MaxSeverity);

        return BuildResult(
            severity,
            severity == DiagnosticSeverity.Healthy ? DiagnosticStatus.Passed : DiagnosticStatus.Finding,
            BuildSummary(assessment, analysis),
            detail: BuildDetail(assessment),
            evidence: BuildEvidence(snapshot, assessment, analysis),
            recommendations: BuildRecommendations(snapshot, assessment, analysis),
            possibleCauses: severity >= DiagnosticSeverity.Suspicious
                ? new[]
                {
                    "Failing or aging storage, cabling/connection issues, a storage controller problem, or unusually high I/O demand (as possibilities - not established)."
                }
                : Array.Empty<string>(),
            limitations: CheckLimitations,
            confidence: Confidence(assessment, analysis));
    }

    private static string BuildSummary(StorageAssessment assessment, EventLogAnalysis analysis)
    {
        var parts = new List<string>();
        if (assessment.Verdict == StorageVerdict.Healthy)
            parts.Add("Volumes have adequate free space and no storage problems were detected.");
        else
            parts.Add("Storage findings were detected (free space, health, latency, or disk events).");

        if (analysis.TotalEvents > 0)
            parts.Add($"Recent disk event logs show {analysis.TotalEvents} relevant event(s).");

        return string.Join(" ", parts);
    }

    private static string BuildDetail(StorageAssessment assessment)
        => assessment.Flags.Contains(StorageFlag.LatencyIdle)
            ? "Disk latency was sampled passively over a short window; the disk was idle during the sample, so latency is not meaningful. " +
              "No load test was run and the drive was not benchmarked."
            : assessment.Flags.Any(f => f is StorageFlag.SlowLatency or StorageFlag.VerySlowLatency)
                ? "Active disk latency during the short passive sample was elevated. This is a point-in-time measurement, not a sustained load test."
                : "Volumes, free space, disk health, and recent disk events were inspected. Latency findings are only reported when the disk was active during the sample.";

    private IReadOnlyList<DiagnosticEvidence> BuildEvidence(
        StorageSnapshot snapshot,
        StorageAssessment assessment,
        EventLogAnalysis analysis)
    {
        var evidence = new List<DiagnosticEvidence>();

        if (snapshot.VolumesAvailable)
        {
            foreach (var volume in snapshot.Volumes)
            {
                var fs = volume.FileSystem is null ? "" : $" [{volume.FileSystem}]";
                var dirty = volume.IsDirty == true ? " [dirty bit set]" : "";
                var free = volume.FreeFraction is double f
                    ? $"{Format.Bytes(volume.FreeBytes)} free of {Format.Bytes(volume.SizeBytes)} ({Format.Percent(f)})"
                    : $"{Format.Bytes(volume.FreeBytes)} free";
                evidence.Add(new DiagnosticEvidence
                {
                    Description = $"Volume {volume.DeviceId}",
                    Value = $"{free}{fs}{dirty}",
                    Source = "Win32_LogicalDisk"
                });
            }
        }
        else
        {
            evidence.Add(new DiagnosticEvidence
            {
                Description = "Volumes",
                Value = "unavailable",
                Source = "Win32_LogicalDisk"
            });
        }

        foreach (var disk in snapshot.Disks)
        {
            var healthText = DescribeHealth(disk.Health);
            var identity = $"{disk.Model} ({Format.Bytes(disk.SizeBytes)}, {disk.InterfaceType ?? "interface unknown"}/{disk.MediaTypeLabel ?? "media unknown"})";
            evidence.Add(new DiagnosticEvidence
            {
                Description = $"Disk {identity}",
                Value = healthText,
                Source = "Win32_DiskDrive + MSFT_PhysicalDisk"
            });
        }

        if (snapshot.Disks.Count == 0 && !snapshot.DisksAvailable)
        {
            evidence.Add(new DiagnosticEvidence
            {
                Description = "Physical Disks",
                Value = "unavailable",
                Source = "Win32_DiskDrive"
            });
        }

        if (!snapshot.StorageNamespaceAvailable)
        {
            evidence.Add(new DiagnosticEvidence
            {
                Description = "Storage Health",
                Value = "The storage health namespace (MSFT_PhysicalDisk / SMART) could not be queried; health is reported as unavailable, not verified.",
                Source = "root\\microsoft\\windows\\storage"
            });
        }

        foreach (var sample in snapshot.Latency)
        {
            if (!sample.HadIoActivity)
            {
                evidence.Add(new DiagnosticEvidence
                {
                    Description = $"Disk Latency ({sample.Instance})",
                    Value = "idle during the sample window - latency not meaningful",
                    Source = "PerfDisk (passive sample)"
                });
            }
            else
            {
                evidence.Add(new DiagnosticEvidence
                {
                    Description = $"Disk Latency ({sample.Instance})",
                    Value = $"avg read {Format.Latency(sample.AverageReadSeconds)} / write {Format.Latency(sample.AverageWriteSeconds)} " +
                            $"({sample.ReadsPerSecond ?? 0:F0}/s reads, {sample.WritesPerSecond ?? 0:F0}/s writes)",
                    Source = "PerfDisk (passive sample)"
                });
            }
        }

        if (analysis.TotalEvents > 0)
        {
            evidence.Add(EventLogReport.WindowRow(analysis));
            foreach (var category in analysis.Categories.OrderByDescending(c => c.MaxSeverity))
                evidence.Add(EventLogReport.CategoryRow(category, _eventOptions.Window));
            foreach (var pattern in analysis.Patterns.OrderByDescending(p => p.Severity))
                evidence.Add(EventLogReport.PatternRow(pattern));
            evidence.Add(EventLogReport.ChannelsRow(analysis.Channels));
        }

        evidence.Add(new DiagnosticEvidence
        {
            Description = "Threshold Reference",
            Value =
                "Free space: < 15% suspicious, < 5% warning; dirty volume bit: warning. " +
                "Stack health: warning/unhealthy flagged; SMART/NVMe wear >= 90%, temperature >= 70C, or uncorrectable errors > 0: flagged. " +
                "Active latency: >= 30ms suspicious, >= 100ms warning (idle disks are not judged). " +
                "Disk event log: patterns from the event log engine (disk 51 critical, repeated disk errors suspicious+).",
            Source = "documented in SPEC.md Phase 9"
        });

        return evidence;
    }

    private static string DescribeHealth(StorageHealth health)
    {
        var parts = new List<string>();

        if (!health.StackQueried)
        {
            parts.Add("Storage stack health could not be queried.");
            return string.Join(" ", parts);
        }

        parts.Add(health.StackState switch
        {
            StorageHealthState.Healthy => "Health reported by storage stack: Healthy",
            StorageHealthState.Warning => "Health reported by storage stack: Warning",
            StorageHealthState.Unhealthy => "Health reported by storage stack: Unhealthy",
            _ => "Storage stack reports health status Unknown"
        });

        if (health.HasReliabilityCounters)
        {
            parts.Add($"wear {health.WearPercent}%, temperature {health.TemperatureCelsius}C, " +
                      $"uncorrected reads {health.ReadErrorsUncorrected ?? 0} / writes {health.WriteErrorsUncorrected ?? 0}, " +
                      $"corrected reads {health.ReadErrorsCorrected ?? 0} / writes {health.WriteErrorsCorrected ?? 0}");
        }
        else
        {
            parts.Add("SMART/NVMe reliability data (wear, temperature, uncorrectable errors) is not available on this system; " +
                      "drive health is not independently verified.");
        }

        return string.Join(" | ", parts);
    }

    private static IReadOnlyList<DiagnosticRecommendation> BuildRecommendations(
        StorageSnapshot snapshot,
        StorageAssessment assessment,
        EventLogAnalysis analysis)
    {
        var recommendations = new List<DiagnosticRecommendation>();

        if (assessment.Flags.Contains(StorageFlag.CriticalFreeSpace))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "A volume is critically low on free space. Free up space (move files, clean temporary files, uninstall unused applications) before writes start failing.",
                Priority = 1
            });
        }
        else if (assessment.Flags.Contains(StorageFlag.LowFreeSpace))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "A volume has low free space. Free up space to leave headroom for Windows updates and temporary files.",
                Priority = 2
            });
        }

        if (assessment.Flags.Contains(StorageFlag.DirtyVolume))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "A volume's dirty bit is set, meaning it was not cleanly dismounted. Back up important data and, if the problem recurs, run a read-only filesystem check (chkdsk) - this tool does not run repairs.",
                Priority = 2
            });
        }

        if (assessment.Flags.Contains(StorageFlag.WearHigh))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "An SSD is near its rated wear limit. Back up important data now and plan to replace the drive.",
                Priority = 1
            });
        }

        if (assessment.Flags.Contains(StorageFlag.UncorrectedErrors) || assessment.Flags.Contains(StorageFlag.StackHealthUnhealthy))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "A drive reported uncorrectable errors or an unhealthy storage-stack status. Back up important data immediately and investigate the drive's health.",
                Priority = 1
            });
        }

        if (assessment.Flags.Contains(StorageFlag.TemperatureHigh))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "A drive is running hot. Improve airflow/cooling and monitor temperatures.",
                Priority = 2
            });
        }

        if (assessment.Flags.Contains(StorageFlag.VerySlowLatency))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "Active disk latency was very high during the sample. If it persists, this can indicate a failing disk or a queue-bound workload.",
                Priority = 2
            });
        }

        if (analysis.Patterns.Any(p => p.Name.Contains("disk", StringComparison.OrdinalIgnoreCase)))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "Repeated disk errors appear in the event log. Back up important data and check drive health; the event log alone does not identify the cause.",
                Priority = 2
            });
        }

        return recommendations;
    }

    private static double Confidence(StorageAssessment assessment, EventLogAnalysis analysis)
    {
        var degraded = assessment.Flags.Contains(StorageFlag.ReliabilityUnavailable)
                       || assessment.Flags.Contains(StorageFlag.StackHealthUnknown)
                       || analysis.UnavailableChannels > 0;
        return degraded ? 0.6 : 0.85;
    }

    private static DiagnosticSeverity ToSeverity(StorageVerdict verdict)
        => verdict switch
        {
            StorageVerdict.Suspicious => DiagnosticSeverity.Suspicious,
            StorageVerdict.Warning => DiagnosticSeverity.Warning,
            StorageVerdict.Critical => DiagnosticSeverity.Critical,
            _ => DiagnosticSeverity.Healthy
        };
}