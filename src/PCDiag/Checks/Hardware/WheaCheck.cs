using PCDiag.Core;
using PCDiag.Events;

namespace PCDiag.Checks.Hardware;

/// <summary>
/// Focused check for WHEA (Windows Hardware Error Architecture) records in the
/// recent event log: fatal records (IDs 1/20) are critical, and repeated corrected
/// machine-check errors (IDs 18/19/41/47) form a warning pattern. Reports what the
/// logs show; it does not identify the failing component by itself.
/// </summary>
public sealed class WheaCheck : EventLogDiagnosticCheck
{
    public override string CheckId => "HW-WHEA-001";
    public override string Name => "WHEA Hardware Errors";
    public override DiagnosticCategory Category => DiagnosticCategory.Hardware;
    public override string Description =>
        "Reviews recent WHEA hardware error records (fatal and corrected machine checks) for repetition.";

    public WheaCheck(IEventLogSource? source = null, EventLogOptions? options = null)
        : base(source, options)
    {
    }

    protected override IReadOnlyList<EventCategory> FocusCategories => new[] { EventCategory.Whea };

    private static readonly IReadOnlyList<string> CheckLimitations = new[]
    {
        $"Only the last {EventLogOptions.Default.Window.TotalDays:F0} days of WHEA records are inspected.",
        "WHEA records do not name the failing component on their own; additional tools (memory/CPU diagnostics, temperatures) are needed to localize a fault.",
        "A single corrected machine-check error is common and is not itself alarming."
    };

    protected override async Task<DiagnosticResult> RunAsync(DiagnosticContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var analysis = await RunAnalysisAsync(cancellationToken);
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
            return "No WHEA hardware error records were found in the recent event logs.";

        if (analysis.Patterns.Count == 0)
            return $"{analysis.TotalEvents} WHEA record(s) were found, but no repeating or fatal pattern was detected.";

        var names = string.Join(", ", analysis.Patterns.Select(p => p.Name));
        return $"WHEA records show {analysis.TotalEvents} event(s) with detected pattern(s): {names}.";
    }

    private static string BuildDetail(EventLogAnalysis analysis)
    {
        var parts = new List<string>
        {
            "WHEA records are grouped by event ID. Event IDs 1 and 20 are fatal hardware error records; 18 and 19 are corrected " +
            "machine-check errors; 41 and 47 report error records. Corrected errors mean Windows recovered and continued, but " +
            "repetition can indicate a developing hardware fault."
        };

        if (analysis.Patterns.Count > 0)
        {
            parts.Add("Detected WHEA patterns reflect what the log shows; they do not by themselves identify the failing component. " +
                      "Confirm with CPU/memory diagnostics, temperature monitoring, and BIOS/firmware logs.");
        }

        return string.Join(" ", parts);
    }

    private IReadOnlyList<DiagnosticEvidence> BuildEvidence(EventLogAnalysis analysis)
    {
        var evidence = new List<DiagnosticEvidence> { EventLogReport.WindowRow(analysis) };

        foreach (var summary in analysis.Categories)
            evidence.Add(EventLogReport.CategoryRow(summary, Options.Window));

        foreach (var pattern in analysis.Patterns.OrderByDescending(p => p.Severity))
            evidence.Add(EventLogReport.PatternRow(pattern));

        evidence.Add(EventLogReport.ChannelsRow(analysis.Channels));

        evidence.Add(new DiagnosticEvidence
        {
            Description = "WHEA Event ID Reference",
            Value = "1/20 = fatal hardware error (critical); 18/19 = corrected machine check; 41/47 = error record reported. " +
                    "Repeated corrected records (>= 2 suspicious, >= 6 warning) form a pattern.",
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
                Text = "A fatal WHEA hardware error was recorded. Treat the hardware as suspect: run memory (Windows Memory Diagnostic) and " +
                       "CPU stress tests, check temperatures, and verify the system is not overclocked/undervolted.",
                RequiresAdmin = false,
                Priority = 1
            });
        }

        if (analysis.Patterns.Any(p => p.Name.Contains("Repeated WHEA", StringComparison.OrdinalIgnoreCase)))
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "Repeated corrected WHEA errors can indicate a developing hardware fault. Monitor temperatures, verify power delivery, " +
                       "and consider memory/CPU diagnostics. A single corrected error is common.",
                RequiresAdmin = false,
                Priority = 2
            });
        }

        if (analysis.Patterns.Count > 0)
        {
            recommendations.Add(new DiagnosticRecommendation
            {
                Text = "Check the Windows System event log for the full WHEA error records (source Microsoft-Windows-WHEA-Logger) and re-run " +
                       "this check after a few days to see whether the pattern persists.",
                RequiresAdmin = false,
                Priority = 3
            });
        }

        return recommendations;
    }

    private static IReadOnlyList<string> PossibleCauses(EventLogAnalysis analysis)
    {
        if (analysis.Patterns.Count == 0)
            return Array.Empty<string>();

        return new[]
        {
            "Failing CPU, memory, or motherboard (a possibility - the WHEA log alone does not identify the component).",
            "Thermal or power-delivery issues, or an unstable overclock/undervolt (as possibilities - not established)."
        };
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