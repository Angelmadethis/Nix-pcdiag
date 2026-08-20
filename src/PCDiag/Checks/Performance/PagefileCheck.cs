using PCDiag.Core;
using PCDiag.Infrastructure;
using PCDiag.Memory;

namespace PCDiag.Checks.Performance;

/// <summary>
/// Reads pagefile configuration (read-only registry) and usage, then classifies the
/// pagefile state. A disabled pagefile is reported as a risk, never "must disable",
/// and a system-managed pagefile is never flagged for high peak usage because Windows
/// grows it automatically.
/// </summary>
public sealed class PagefileCheck : DiagnosticCheck
{
    private readonly IPagefileSource _source;
    private readonly MemoryOptions _options;

    public override string CheckId => "PERF-PAG-001";
    public override string Name => "Pagefile Configuration";
    public override DiagnosticCategory Category => DiagnosticCategory.Performance;
    public override string Description =>
        "Reads the pagefile configuration and usage to assess paging capacity and configuration health.";

    public PagefileCheck(IPagefileSource? source = null, MemoryOptions? options = null)
    {
        _source = source ?? new WmiPagefileSource();
        _options = options ?? MemoryOptions.Default;
    }

    private static readonly IReadOnlyList<string> CheckLimitations = new[]
    {
        "Pagefile configuration is read from the registry (read-only); nothing is ever written or changed.",
        "For a system-managed pagefile the allocated size changes automatically, so percentages are informational.",
        "This is a single point-in-time snapshot of usage; peak usage is since boot.",
        "This check never recommends disabling the pagefile."
    };

    protected override async Task<DiagnosticResult> RunAsync(DiagnosticContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var info = await Task.Run(() => _source.GetInfo(), cancellationToken).ConfigureAwait(false);

        if (info.Config is null && !info.UsageAvailable)
        {
            return Unavailable("Pagefile configuration and usage could not be read on this system.");
        }

        var assessment = PagefileClassifier.Classify(info, _options);
        var severity = ToSeverity(assessment.Verdict);

        return BuildResult(
            severity,
            severity == DiagnosticSeverity.Healthy ? DiagnosticStatus.Passed : DiagnosticStatus.Finding,
            BuildSummary(assessment),
            detail: BuildDetail(assessment),
            evidence: BuildEvidence(info, assessment),
            recommendations: BuildRecommendations(info, assessment),
            possibleCauses: severity >= DiagnosticSeverity.Suspicious
                ? new[]
                {
                    "The pagefile was deliberately disabled or sized down, or paging demand is high relative to configured capacity (as possibilities - not established)."
                }
                : Array.Empty<string>(),
            limitations: CheckLimitations,
            confidence: 0.8);
    }

    private static string BuildSummary(PagefileAssessment assessment)
        => assessment.Verdict switch
        {
            PagefileVerdict.Healthy => assessment.Flags.Contains(PagefileFlag.SystemManaged)
                ? "The pagefile is system-managed and has adequate headroom."
                : "The pagefile is configured with adequate headroom.",
            _ => "The pagefile configuration or usage warrants attention."
        };

    private static string BuildDetail(PagefileAssessment assessment)
        => assessment.Flags.Contains(PagefileFlag.NoPagefile)
            ? "No pagefile is configured. Without a pagefile the commit limit is physical RAM only, so memory pressure can crash processes " +
              "or the system. Some users disable the pagefile deliberately; this reports the risk, it does not assume the cause."
            : assessment.Flags.Contains(PagefileFlag.UsageNearAllocated) || assessment.Flags.Contains(PagefileFlag.PeakHigh)
                ? "A fixed-size pagefile cannot grow. High usage or a high peak against its allocated size means paging demand is near capacity."
                : "The pagefile is sized with headroom for normal paging demand.";

    private IReadOnlyList<DiagnosticEvidence> BuildEvidence(PagefileInfo info, PagefileAssessment assessment)
    {
        var evidence = new List<DiagnosticEvidence>
        {
            new()
            {
                Description = "Configuration",
                Value = DescribeConfig(info),
                Source = "Registry (read-only)"
            }
        };

        if (info.UsageAvailable)
        {
            var allocated = info.TotalAllocatedBytes is long a && a > 0 ? a : (long?)null;

            evidence.Add(new DiagnosticEvidence
            {
                Description = "Current Usage",
                Value = allocated is long alloc
                    ? $"{Format.Bytes(info.TotalCurrentBytes)} of {Format.Bytes(alloc)} allocated ({Format.Percent((double)(info.TotalCurrentBytes ?? 0) / alloc)})"
                    : $"{Format.Bytes(info.TotalCurrentBytes)}",
                Source = "Win32_PageFileUsage"
            });
            evidence.Add(new DiagnosticEvidence
            {
                Description = "Peak Usage",
                Value = allocated is long alloc2
                    ? $"{Format.Bytes(info.TotalPeakBytes)} ({Format.Percent((double)(info.TotalPeakBytes ?? 0) / alloc2)} of allocated)"
                    : $"{Format.Bytes(info.TotalPeakBytes)}",
                Source = "Win32_PageFileUsage"
            });
        }
        else
        {
            evidence.Add(new DiagnosticEvidence
            {
                Description = "Usage",
                Value = "unavailable",
                Source = "Win32_PageFileUsage"
            });
        }

        if (info.PhysicalBytes is not null)
        {
            evidence.Add(new DiagnosticEvidence
            {
                Description = "Context",
                Value = $"{Format.Bytes(info.TotalAllocatedBytes ?? 0)} pagefile against {Format.Bytes(info.PhysicalBytes)} installed RAM",
                Source = "Win32_ComputerSystem"
            });
        }

        if (assessment.Flags.Contains(PagefileFlag.ConfigUnavailable))
        {
            evidence.Add(new DiagnosticEvidence
            {
                Description = "Data Availability",
                Value = "The pagefile configuration could not be read from the registry; usage is still reported.",
                Source = "Registry (read-only)"
            });
        }

        evidence.Add(new DiagnosticEvidence
        {
            Description = "Threshold Reference",
            Value =
                "System-managed pagefile: healthy. Disabled (no pagefile): suspicious (never critical - a deliberate choice for some users). " +
                "Fixed-size pagefile with current usage >= 95% of allocated or peak >= 90%: suspicious (it cannot grow).",
            Source = "documented in SPEC.md Phase 9"
        });

        return evidence;
    }

    private static IReadOnlyList<DiagnosticRecommendation> BuildRecommendations(PagefileInfo info, PagefileAssessment assessment)
    {
        var recommendations = new List<DiagnosticRecommendation>();

        if (assessment.Flags.Contains(PagefileFlag.NoPagefile))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "Consider enabling a system-managed pagefile so the commit limit includes paging capacity. This is a manual change made in System Properties (requires admin); nothing is applied by this tool.",
                RequiresAdmin = true,
                Priority = 2
            });
        }

        if (assessment.Flags.Contains(PagefileFlag.UsageNearAllocated) || assessment.Flags.Contains(PagefileFlag.PeakHigh))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "A fixed-size pagefile is near its capacity. Consider raising the pagefile maximum or letting Windows manage its size (manual change, requires admin).",
                RequiresAdmin = true,
                Priority = 2
            });
        }

        if (recommendations.Count > 0)
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "Re-run this check after heavy use to see whether usage stays near the limit.",
                Priority = 3
            });
        }

        return recommendations;
    }

    private static string DescribeConfig(PagefileInfo info)
    {
        if (info.Config is null)
            return "unavailable";

        if (info.Config.Entries.Count == 0)
            return "No pagefile configured";

        if (info.Config.IsSystemManaged)
            return $"System-managed ({string.Join(", ", info.Config.Entries)})";

        return $"Custom: {string.Join("; ", info.Config.Entries)}";
    }

    private static DiagnosticSeverity ToSeverity(PagefileVerdict verdict)
        => verdict switch
        {
            PagefileVerdict.Suspicious => DiagnosticSeverity.Suspicious,
            PagefileVerdict.Warning => DiagnosticSeverity.Warning,
            _ => DiagnosticSeverity.Healthy
        };
}