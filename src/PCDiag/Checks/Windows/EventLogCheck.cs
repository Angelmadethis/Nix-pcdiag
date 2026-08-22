using PCDiag.Core;
using PCDiag.Events;

namespace PCDiag.Checks.Windows;

/// <summary>
/// Aggregates recent Windows event log entries across the inspected hardware,
/// driver, power, network, USB, and service categories and reports per-category
/// counts, first/last occurrence, event IDs, affected components, frequency, and
/// detected patterns. Read-only; reports patterns, never root causes.
/// </summary>
public sealed class EventLogCheck : EventLogDiagnosticCheck
{
    public override string CheckId => "WIN-EVT-001";
    public override string Name => "Event Log Analysis";
    public override DiagnosticCategory Category => DiagnosticCategory.Windows;
    public override string Description =>
        "Aggregates recent Windows event log entries by category and detects repeating error patterns.";

    public EventLogCheck(IEventLogSource? source = null, EventLogOptions? options = null)
        : base(source, options)
    {
    }

    protected override IReadOnlyList<EventCategory> FocusCategories => Array.Empty<EventCategory>();

    private static readonly IReadOnlyList<string> CheckLimitations = new[]
    {
        $"Only the last {EventLogOptions.Default.Window.TotalDays:F0} days of events are inspected; older history is not considered.",
        "Only well-known providers and event IDs are classified; events from unlisted providers are ignored.",
        "The affected component is inferred from the event provider; message text is not parsed.",
        "The event log alone cannot establish a root cause; findings are reported as observed patterns, not causes."
    };

    protected override async Task<DiagnosticResult> RunAsync(DiagnosticContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var analysis = await RunAnalysisAsync(context);
        cancellationToken.ThrowIfCancellationRequested();

        var severity = analysis.MaxSeverity;
        var status = severity == DiagnosticSeverity.Healthy ? DiagnosticStatus.Passed : DiagnosticStatus.Finding;

        return BuildResult(
            severity,
            status,
            BuildSummary(analysis),
            detail: BuildDetail(analysis),
            evidence: BuildEvidence(analysis),
            recommendations: BuildRecommendations(analysis),
            possibleCauses: PossibleCauses(analysis),
            limitations: CheckLimitations,
            confidence: ComputeConfidence(analysis));
    }

    private static string BuildSummary(EventLogAnalysis analysis)
    {
        if (analysis.TotalEvents == 0)
            return "No relevant error events were found in the recent Windows event logs.";

        if (analysis.Patterns.Count == 0)
        {
            return analysis.Categories.Count == 1
                ? $"Event logs show {analysis.TotalEvents} relevant event in {analysis.Categories[0].Label}, but no repeating pattern was detected."
                : $"Event logs show {analysis.TotalEvents} relevant events across {analysis.Categories.Count} categories, but no repeating pattern was detected.";
        }

        var names = string.Join(", ", analysis.Patterns.Select(p => p.Name));
        return $"Event logs show {analysis.TotalEvents} relevant events across {analysis.Categories.Count} categories with {analysis.Patterns.Count} detected pattern(s): {names}.";
    }

    private static string BuildDetail(EventLogAnalysis analysis)
    {
        var parts = new List<string>
        {
            "Events are aggregated by category over the observation window. Each category reports the number of events, " +
            "first and most recent occurrence, event IDs, affected components, frequency, and the highest severity seen. " +
            "Patterns are detected from repetition (counted from events at Suspicious severity or worse, so routine informational " +
            "events do not trigger patterns) and from critical single events (fatal WHEA records, disk paging errors, " +
            "Kernel-Power 41 unexpected shutdowns, bugchecks)."
        };

        if (analysis.Patterns.Count > 0)
        {
            parts.Add("Detected patterns indicate something repeated or serious in the logs, but the event log alone does not " +
                      "identify the root cause. Use the focused checks (pcdiag check whea / pcdiag check drivers) for more detail.");
        }

        if (analysis.UnavailableChannels > 0)
        {
            parts.Add($"{analysis.UnavailableChannels} channel(s) could not be read; those channels are reported as unavailable " +
                      "rather than treated as healthy.");
        }

        return string.Join(" ", parts);
    }

    private IReadOnlyList<DiagnosticEvidence> BuildEvidence(EventLogAnalysis analysis)
    {
        var evidence = new List<DiagnosticEvidence> { EventLogReport.WindowRow(analysis) };

        foreach (var summary in analysis.Categories.OrderByDescending(c => c.MaxSeverity).ThenBy(c => c.Label))
            evidence.Add(EventLogReport.CategoryRow(summary, Options.Window));

        foreach (var pattern in analysis.Patterns.OrderByDescending(p => p.Severity))
            evidence.Add(EventLogReport.PatternRow(pattern));

        evidence.Add(EventLogReport.ChannelsRow(analysis.Channels));

        evidence.Add(new DiagnosticEvidence
        {
            Description = "Threshold Reference",
            Value =
                "WHEA: fatal ID 1/20 critical; repeated corrected (18/19) suspicious >= 2, warning >= 6; " +
                "disk: ID 51 critical, repeated suspicious >= 2, warning >= 5; GPU driver resets (TDR 4101): suspicious >= 2, warning >= 5; " +
                "network resets: suspicious >= 5, warning >= 15; USB resets: suspicious >= 3, warning >= 8; " +
                "service failures: suspicious >= 3, warning >= 8; driver failures: suspicious >= 2, warning >= 5; " +
                "any bugcheck or Kernel-Power 41 is critical.",
            Source = "documented in SPEC.md Phase 7"
        });

        return evidence;
    }

    private static IReadOnlyList<DiagnosticRecommendation> BuildRecommendations(EventLogAnalysis analysis)
    {
        var recommendations = new List<DiagnosticRecommendation>();
        var patterns = analysis.Patterns;

        if (patterns.Any(p => p.Severity == DiagnosticSeverity.Critical))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "Critical event patterns were detected (fatal WHEA record, disk paging error, unexpected shutdown, or bugcheck). " +
                       "Investigate promptly: check for hardware faults, review the focused reports (pcdiag check whea / pcdiag check drivers), " +
                       "and check for kernel dump files for bugchecks.",
                RequiresAdmin = false,
                Priority = 1
            });
        }

        if (patterns.Any(p => p.Name.Contains("WHEA", StringComparison.OrdinalIgnoreCase)))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "Repeated WHEA errors can indicate failing hardware or a thermal/power issue. Run 'pcdiag check whea' for the " +
                       "WHEA detail, monitor temperatures, and consider hardware diagnostics. A single corrected WHEA event is common.",
                RequiresAdmin = false,
                Priority = 2
            });
        }

        if (patterns.Any(p => p.Name.Contains("GPU driver resets", StringComparison.OrdinalIgnoreCase)))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "Repeated display-driver resets (TDR) can indicate a driver problem, overheating, or GPU instability. Update the " +
                       "display driver and monitor GPU temperatures. Run 'pcdiag check drivers' for detail.",
                RequiresAdmin = false,
                Priority = 2
            });
        }

        if (patterns.Any(p => p.Name.Contains("disk", StringComparison.OrdinalIgnoreCase)))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "Repeated disk errors can indicate a failing drive, cabling, or a controller issue. Back up important data, check " +
                       "drive health (SMART), and check cabling.",
                RequiresAdmin = false,
                Priority = 2
            });
        }

        if (patterns.Any(p => p.Name.Contains("network adapter resets", StringComparison.OrdinalIgnoreCase)))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "Repeated network adapter resets can indicate a driver issue, a faulty cable/port, or a failing adapter. Update the " +
                       "network driver and check cabling/Wi-Fi signal.",
                RequiresAdmin = false,
                Priority = 2
            });
        }

        if (patterns.Any(p => p.Name.Contains("USB resets", StringComparison.OrdinalIgnoreCase)))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "Repeated USB resets can indicate a driver, power, or device issue. Try different ports, update USB drivers, and " +
                       "disconnect recently added devices to isolate the cause.",
                RequiresAdmin = false,
                Priority = 2
            });
        }

        if (patterns.Count > 0)
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "Re-run this check after a few days to see whether the pattern is sustained; a single run is a point-in-time snapshot.",
                RequiresAdmin = false,
                Priority = 3
            });
        }

        return recommendations;
    }

    private static IReadOnlyList<string> PossibleCauses(EventLogAnalysis analysis)
    {
        var causes = new List<string>();
        if (analysis.Patterns.Any(p => p.Name.Contains("WHEA", StringComparison.OrdinalIgnoreCase)))
        {
            causes.Add("Failing CPU, memory, or motherboard; thermal issues; overclocking/undervolting (as possibilities - not established).");
        }
        if (analysis.Patterns.Any(p => p.Name.Contains("disk", StringComparison.OrdinalIgnoreCase)))
        {
            causes.Add("Failing storage, cabling, or a storage controller issue (as possibilities - not established).");
        }
        if (analysis.Patterns.Any(p => p.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase)))
        {
            causes.Add("Display driver issue, overheating GPU, or GPU hardware instability (as possibilities - not established).");
        }
        if (analysis.Patterns.Any(p => p.Name.Contains("network adapter", StringComparison.OrdinalIgnoreCase)))
        {
            causes.Add("Network driver issue, faulty cable/port, or failing adapter (as possibilities - not established).");
        }
        if (analysis.Patterns.Any(p => p.Name.Contains("USB", StringComparison.OrdinalIgnoreCase)))
        {
            causes.Add("USB driver, power delivery, or a failing device (as possibilities - not established).");
        }
        if (analysis.Patterns.Any(p => p.Name.Contains("shutdown", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("bugcheck")))
        {
            causes.Add("Power loss, overheating, a driver crash, or hardware fault (as possibilities - the event log alone does not determine which).");
        }
        return causes;
    }

    private static double ComputeConfidence(EventLogAnalysis analysis)
        => analysis.MaxSeverity switch
        {
            DiagnosticSeverity.Healthy => 0.9,
            DiagnosticSeverity.Info => 0.6,
            DiagnosticSeverity.Suspicious => 0.6,
            DiagnosticSeverity.Warning => 0.7,
            _ => 0.7
        };
}