using PCDiag.Core;
using PCDiag.Infrastructure;
using PCDiag.Memory;

namespace PCDiag.Checks.Performance;

/// <summary>
/// Reads installed RAM, available memory, commit usage versus the commit limit,
/// paging activity, pagefile usage, and kernel pool sizes, then classifies memory
/// pressure. Read-only; reports pressure as a symptom, never a diagnosis or cause.
/// </summary>
public sealed class MemoryCheck : DiagnosticCheck
{
    private readonly IMemorySnapshotSource _source;
    private readonly MemoryOptions _options;

    public override string CheckId => "PERF-MEM-001";
    public override string Name => "Memory Usage & Pressure";
    public override DiagnosticCategory Category => DiagnosticCategory.Performance;
    public override string Description =>
        "Reads memory usage, commit pressure, paging activity, and pagefile usage to detect memory pressure.";

    public MemoryCheck(IMemorySnapshotSource? source = null, MemoryOptions? options = null)
    {
        _source = source ?? new WmiMemorySnapshotSource();
        _options = options ?? MemoryOptions.Default;
    }

    private static readonly IReadOnlyList<string> CheckLimitations = new[]
    {
        "This is a single point-in-time snapshot, not sustained monitoring; brief peaks are normal.",
        "Available memory includes the reclaimable standby cache, so Windows will free memory under pressure before pages are needed.",
        "Paging activity (Pages/sec) is instantaneous at the sample moment and is never judged on its own.",
        "Kernel pool sizes are a snapshot and cannot show a growing leak by themselves; no process-level attribution is made here.",
        "Findings describe pressure, never a root cause."
    };

    protected override async Task<DiagnosticResult> RunAsync(DiagnosticContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = await Task.Run(() => _source.GetSnapshot(), cancellationToken).ConfigureAwait(false);

        if (!snapshot.OperatingSystemInfoAvailable && !snapshot.PerfCountersAvailable && !snapshot.PagefileUsageAvailable)
        {
            return Unavailable("Memory data could not be read on this system.");
        }

        var assessment = MemoryClassifier.Classify(snapshot, _options);
        var severity = ToSeverity(assessment.Verdict);

        return BuildResult(
            severity,
            severity == DiagnosticSeverity.Healthy ? DiagnosticStatus.Passed : DiagnosticStatus.Finding,
            BuildSummary(assessment),
            detail: BuildDetail(assessment),
            evidence: BuildEvidence(snapshot, assessment),
            recommendations: BuildRecommendations(severity),
            possibleCauses: severity >= DiagnosticSeverity.Suspicious
                ? new[]
                {
                    "Many applications/background processes running at once, a specific process using more memory than expected, or too little RAM for the workload (as possibilities - not established)."
                }
                : Array.Empty<string>(),
            limitations: CheckLimitations,
            confidence: Confidence(assessment));
    }

    private static string BuildSummary(MemoryAssessment assessment)
        => assessment.Verdict switch
        {
            MemoryVerdict.Healthy => "Memory usage is within normal bounds.",
            MemoryVerdict.Suspicious => "Memory usage is elevated; commit usage or available memory is outside the normal range.",
            _ => "Memory usage is high; commit usage or available memory is at a level that can affect stability."
        };

    private static string BuildDetail(MemoryAssessment assessment)
    {
        var parts = new List<string>
        {
            "Commit usage is the total virtual memory Windows has reserved across all processes, compared against the commit " +
            "limit (physical RAM plus the pagefile). Available memory includes the reclaimable standby cache, so low available " +
            "memory is pressure Windows can relieve by reclaiming cache - it is a symptom, not a diagnosis."
        };

        if (assessment.CommitRatio is double cr)
            parts.Add($"Commit usage is {Format.Percent(cr)} of the commit limit.");
        if (assessment.AvailablePercent is double ap)
            parts.Add($"Available memory is {Format.Percent(ap)} of installed RAM.");

        return string.Join(" ", parts);
    }

    private IReadOnlyList<DiagnosticEvidence> BuildEvidence(MemorySnapshot s, MemoryAssessment a)
    {
        var evidence = new List<DiagnosticEvidence>
        {
            new() { Description = "Installed RAM", Value = Format.Bytes(s.TotalPhysicalBytes), Source = "Win32_OperatingSystem" },
            new()
            {
                Description = "Available Memory",
                Value = a.AvailablePercent is double ap
                    ? $"{Format.Bytes(s.AvailableBytes)} ({Format.Percent(ap)} of installed)"
                    : Format.Bytes(s.AvailableBytes),
                Source = "PerfOS Memory"
            },
            new()
            {
                Description = "Committed Memory",
                Value = a.CommitRatio is double cr
                    ? $"{Format.Bytes(s.CommittedBytes)} of {Format.Bytes(s.CommitLimitBytes)} commit limit ({Format.Percent(cr)})"
                    : $"{Format.Bytes(s.CommittedBytes)} of {Format.Bytes(s.CommitLimitBytes)} commit limit",
                Source = "PerfOS Memory"
            },
            new()
            {
                Description = "Pagefile Usage",
                Value = s.PagefileUsageAvailable
                    ? $"{Format.Bytes(s.PagefileCurrentBytes)} used of {Format.Bytes(s.PagefileAllocatedBytes)} allocated (peak {Format.Bytes(s.PagefilePeakBytes)})"
                    : "unavailable",
                Source = "Win32_PageFileUsage"
            },
            new() { Description = "Paging Activity", Value = $"{s.PagesPerSecond ?? 0}/sec (instantaneous)", Source = "PerfOS Memory" },
            new()
            {
                Description = "Kernel Pools",
                Value = $"Nonpaged {Format.Bytes(s.PoolNonpagedBytes)} | Paged {Format.Bytes(s.PoolPagedBytes)}",
                Source = "PerfOS Memory"
            }
        };

        var unavailable = new List<string>();
        if (!s.OperatingSystemInfoAvailable) unavailable.Add("installed RAM (Win32_OperatingSystem)");
        if (!s.PerfCountersAvailable) unavailable.Add("perf counters (PerfOS Memory)");
        if (!s.PagefileUsageAvailable) unavailable.Add("pagefile usage (Win32_PageFileUsage)");
        if (unavailable.Count > 0)
        {
            evidence.Add(new DiagnosticEvidence
            {
                Description = "Data Availability",
                Value = $"The following could not be read and are reported as unavailable: {string.Join(", ", unavailable)}.",
                Source = "WMI"
            });
        }

        evidence.Add(ThresholdRow());
        return evidence;
    }

    private static IReadOnlyList<DiagnosticRecommendation> BuildRecommendations(DiagnosticSeverity severity)
    {
        if (severity < DiagnosticSeverity.Suspicious)
            return Array.Empty<DiagnosticRecommendation>();

        return new List<DiagnosticRecommendation>
        {
            new()
            {
                Text = "Close memory-heavy applications and restart background programs to relieve pressure, then re-run this check after a few minutes.",
                Priority = 2
            },
            new()
            {
                Text = "Check for a single process using an unexpectedly large amount of memory (e.g. Task Manager by memory). A single point-in-time reading cannot prove a leak.",
                Priority = 3
            }
        };
    }

    private static double Confidence(MemoryAssessment a)
        => a.Flags.Any(f => f is MemoryFlag.OperatingSystemInfoUnavailable or MemoryFlag.PerfCountersUnavailable) ? 0.6 : 0.85;

    private static DiagnosticEvidence ThresholdRow()
        => new()
        {
            Description = "Threshold Reference",
            Value =
                "Commit ratio (committed / commit limit): >= 70% suspicious, >= 85% warning. " +
                "Available memory: < 15% of installed suspicious, < 5% warning (or below 1.5 GB absolute). " +
                "Pages/sec >= 200 is reported as heavy paging but never judged alone.",
            Source = "documented in SPEC.md Phase 9"
        };

    private static DiagnosticSeverity ToSeverity(MemoryVerdict verdict)
        => verdict switch
        {
            MemoryVerdict.Suspicious => DiagnosticSeverity.Suspicious,
            MemoryVerdict.Warning => DiagnosticSeverity.Warning,
            MemoryVerdict.Critical => DiagnosticSeverity.Critical,
            _ => DiagnosticSeverity.Healthy
        };
}