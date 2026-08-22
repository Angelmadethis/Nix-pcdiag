using PCDiag.Core;
using PCDiag.Events;

namespace PCDiag.Checks.Hardware;

/// <summary>
/// Focused check for driver stability: display/GPU driver resets (TDR), driver
/// load/signature failures, bugchecks, and storage controller driver errors
/// (storahci/stornvme/StorPort). Reports what the logs show; it does not claim a
/// specific driver is at fault.
/// </summary>
public sealed class DriverCheck : EventLogDiagnosticCheck
{
    public override string CheckId => "HW-DRV-001";
    public override string Name => "Driver & Display Stability";
    public override DiagnosticCategory Category => DiagnosticCategory.Hardware;
    public override string Description =>
        "Reviews display/GPU driver resets (TDR), driver load/signature failures, bugchecks, and storage controller errors.";

    public DriverCheck(IEventLogSource? source = null, EventLogOptions? options = null)
        : base(source, options)
    {
    }

    protected override IReadOnlyList<EventCategory> FocusCategories => new[]
    {
        EventCategory.DisplayGpu,
        EventCategory.DriverFailure,
        EventCategory.StorageController
    };

    private static readonly IReadOnlyList<string> CheckLimitations = new[]
    {
        $"Only the last {EventLogOptions.Default.Window.TotalDays:F0} days of events are inspected.",
        "The affected component is inferred from the event provider; the event log alone cannot prove which driver or device is at fault.",
        "TDR events (display driver stopped responding and recovered) are common once; repetition is the concern."
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
            return "No display, driver, or storage controller error events were found in the recent event logs.";

        if (analysis.Patterns.Count == 0)
        {
            return analysis.Categories.Count == 1
                ? $"{analysis.TotalEvents} {analysis.Categories[0].Label} event(s) were found, but no repeating pattern was detected."
                : $"{analysis.TotalEvents} driver-related event(s) were found across {analysis.Categories.Count} categories, but no repeating pattern was detected.";
        }

        var names = string.Join(", ", analysis.Patterns.Select(p => p.Name));
        return $"Driver-related logs show {analysis.TotalEvents} event(s) with detected pattern(s): {names}.";
    }

    private static string BuildDetail(EventLogAnalysis analysis)
    {
        var parts = new List<string>
        {
            "This check covers display/GPU driver resets (TDR - event 4101 and similar), driver load/signature failures, bugchecks, " +
            "and storage controller driver errors. Patterns are detected from repetition and from critical single events such as bugchecks."
        };

        if (analysis.Patterns.Any(p => p.Name.Contains("GPU driver resets", StringComparison.OrdinalIgnoreCase)))
        {
            parts.Add("Repeated TDR events mean the display driver stopped responding and recovered several times. This can indicate a " +
                      "driver problem, overheating, or GPU instability - this log alone does not identify which.");
        }

        if (analysis.Patterns.Any(p => p.Name.Contains("bugcheck", StringComparison.OrdinalIgnoreCase)))
        {
            parts.Add("A bugcheck record means the system crashed and restarted. Check kernel dump files (C:\\Windows\\Minidump) for the " +
                      "faulting module; the event log alone does not name it.");
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
            Description = "Driver Event ID Reference",
            Value = "4101 = display driver TDR; BugCheck / WER-SystemErrorReporting 1001 = system bugcheck; SCM 7026 = driver failed to load; " +
                    "CodeIntegrity 3004/3023/3033 = driver signature/load; storahci/stornvme/StorPort = storage controller driver. " +
                    "TDR repeated >= 2 suspicious, >= 5 warning; driver failures >= 2 suspicious, >= 5 warning.",
            Source = "documented in SPEC.md Phase 7"
        });

        return evidence;
    }

    private static IReadOnlyList<DiagnosticRecommendation> BuildRecommendations(EventLogAnalysis analysis)
    {
        var recommendations = new List<DiagnosticRecommendation>();

        if (analysis.Patterns.Any(p => p.Severity == DiagnosticSeverity.Critical))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "A bugcheck (system crash) was recorded. Inspect kernel dump files (C:\\Windows\\Minidump) to find the faulting driver " +
                       "and update the relevant drivers, especially the display or storage driver.",
                RequiresAdmin = false,
                Priority = 1
            });
        }

        if (analysis.Patterns.Any(p => p.Name.Contains("GPU driver resets", StringComparison.OrdinalIgnoreCase)))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "Repeated display-driver resets (TDR) can indicate a driver problem, overheating, or GPU instability. Update the display " +
                       "driver (clean install), monitor GPU temperatures, and if it persists check the GPU hardware.",
                RequiresAdmin = false,
                Priority = 2
            });
        }

        if (analysis.Patterns.Any(p => p.Name.Contains("storage controller", StringComparison.OrdinalIgnoreCase)))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "Repeated storage controller errors (storahci/stornvme/StorPort) can indicate a driver issue or failing hardware. Update " +
                       "the storage driver and check the drive health (SMART) and cabling.",
                RequiresAdmin = false,
                Priority = 2
            });
        }

        if (analysis.Patterns.Any(p => p.Name.Contains("driver failures", StringComparison.OrdinalIgnoreCase)))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "Multiple driver load/signature failures were recorded. Update or reinstall the affected drivers, and check that the " +
                       "relevant driver is not blocked by policy.",
                RequiresAdmin = true,
                Priority = 2
            });
        }

        if (analysis.Patterns.Count > 0)
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
        if (analysis.Patterns.Any(p => p.Name.Contains("GPU driver resets", StringComparison.OrdinalIgnoreCase)))
        {
            causes.Add("Display driver issue, overheating GPU, or GPU hardware instability (as possibilities - not established).");
        }
        if (analysis.Patterns.Any(p => p.Name.Contains("storage controller", StringComparison.OrdinalIgnoreCase)))
        {
            causes.Add("Storage controller driver issue or failing storage hardware (as possibilities - not established).");
        }
        if (analysis.Patterns.Any(p => p.Name.Contains("driver failures", StringComparison.OrdinalIgnoreCase)))
        {
            causes.Add("Outdated, incompatible, or blocked drivers (as possibilities - not established).");
        }
        if (analysis.Patterns.Any(p => p.Name.Contains("bugcheck", StringComparison.OrdinalIgnoreCase)))
        {
            causes.Add("A crashing driver or hardware fault (as possibilities - confirm with kernel dump analysis).");
        }
        return causes;
    }

    private static double ComputeConfidence(EventLogAnalysis analysis)
        => analysis.MaxSeverity switch
        {
            DiagnosticSeverity.Healthy => 0.9,
            DiagnosticSeverity.Suspicious => 0.5,
            DiagnosticSeverity.Warning => 0.6,
            _ => 0.6
        };
}