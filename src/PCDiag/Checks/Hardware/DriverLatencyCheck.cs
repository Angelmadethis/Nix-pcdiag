using PCDiag.Core;
using PCDiag.Infrastructure;
using PCDiag.Interrupts;

namespace PCDiag.Checks.Hardware;

/// <summary>
/// Investigates driver-related latency indicators by sampling interrupt and DPC
/// activity from Windows performance counters over a short window and correlating
/// elevated activity with CPU load. Reports activity rates plus a confidence score.
/// It does NOT measure true per-DPC latency (that requires an admin ETW kernel
/// trace) and never attributes activity to a specific driver/device without one.
/// Driver/device inventory is context only; no driver is ever recommended for
/// uninstallation.
/// </summary>
public sealed class DriverLatencyCheck : DiagnosticCheck
{
    private readonly IInterruptSnapshotSource _source;
    private readonly InterruptOptions _options;

    public override string CheckId => "HW-LAT-001";
    public override string Name => "Interrupt & DPC Activity (driver latency indicators)";
    public override DiagnosticCategory Category => DiagnosticCategory.Hardware;
    public override string Description =>
        "Samples interrupt and DPC activity rates from performance counters to detect sustained elevation " +
        "that can correlate with microstutters, audio glitches, input delay, FPS hitching, or network spikes.";

    public DriverLatencyCheck(IInterruptSnapshotSource? source = null, InterruptOptions? options = null)
    {
        _source = source ?? new WmiInterruptSnapshotSource();
        _options = options ?? InterruptOptions.Default;
    }

    private static readonly IReadOnlyList<string> CheckLimitations = new[]
    {
        "This check measures interrupt/DPC activity RATES from performance counters - it does NOT measure true per-DPC latency in " +
        "microseconds. Real DPC latency requires an admin ETW kernel trace (e.g. LatencyMon or Windows Performance Recorder).",
        "The sample is a short point-in-time window; only sustained elevation is detected. Transient microstutters between samples are missed.",
        "Absolute thresholds are conservative heuristics and vary by machine and workload.",
        "Activity cannot be attributed to a specific driver or device without an ETW stack trace; the driver/device list is context only.",
        "No driver or device is recommended for uninstallation - ever."
    };

    protected override async Task<DiagnosticResult> RunAsync(DiagnosticContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = await _source.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (!snapshot.CountersAvailable)
        {
            return Unavailable(
                "Interrupt/DPC activity counters (Win32_PerfRawData_PerfOS_Processor) could not be read on this system. " +
                "This check cannot assess driver-related latency indicators without them.");
        }

        var assessment = InterruptClassifier.Classify(snapshot, _options);
        var severity = ToSeverity(assessment.Verdict);

        return BuildResult(
            severity,
            severity == DiagnosticSeverity.Healthy ? DiagnosticStatus.Passed : DiagnosticStatus.Finding,
            BuildSummary(assessment),
            detail: BuildDetail(snapshot, assessment),
            evidence: BuildEvidence(snapshot, assessment),
            recommendations: BuildRecommendations(assessment),
            possibleCauses: severity >= DiagnosticSeverity.Suspicious
                ? new[]
                {
                    "Sustained interrupt/DPC activity correlated with CPU load can indicate a device or driver servicing many interrupts " +
                    "or a driver doing heavy kernel work (as a possibility - not established without an ETW trace)."
                }
                : Array.Empty<string>(),
            limitations: CheckLimitations,
            confidence: severity == DiagnosticSeverity.Healthy
                ? InterruptClassifier.ComputeConfidence(assessment)
                : Math.Min(InterruptClassifier.ComputeConfidence(assessment), 0.7));
    }

    private static string BuildSummary(InterruptAssessment assessment)
        => assessment.Verdict switch
        {
            InterruptVerdict.Suspicious =>
                "Elevated interrupt or DPC activity was observed during the sample window (activity rates, not true DPC latency).",
            InterruptVerdict.Warning =>
                "High interrupt or DPC activity was observed and coincides with heavy kernel/CPU load (activity rates, not true DPC latency).",
            _ => "No sustained elevated interrupt or DPC activity was observed during the sample window."
        };

    private static string BuildDetail(InterruptSnapshot snapshot, InterruptAssessment assessment)
    {
        var parts = new List<string>
        {
            $"Interrupt and DPC (deferred procedure call) activity was sampled passively from Windows performance counters over " +
            $"{snapshot.SampleDurationSeconds:F1} seconds. Elevated rates sustained across the window can drive the symptoms this check " +
            "is looking for (microstutters, audio glitches, input delay, FPS hitching, network spikes)."
        };

        if (assessment.Flags.Contains(InterruptFlag.ConcentratedInterruptLoad))
        {
            parts.Add("Interrupt activity is concentrated on one logical processor well above its peers - consistent with a device whose " +
                      "interrupt affinity is pinned to a single core, which can cause localized stutter.");
        }

        parts.Add("This check reports activity rates and a confidence score. It does NOT measure true per-DPC latency and does not " +
                  "attribute the activity to a specific driver; a real latency trace (LatencyMon / wpr, as administrator) is required for that.");

        return string.Join(" ", parts);
    }

    private IReadOnlyList<DiagnosticEvidence> BuildEvidence(InterruptSnapshot snapshot, InterruptAssessment assessment)
    {
        var evidence = new List<DiagnosticEvidence>
        {
            new()
            {
                Description = "Sample Window",
                Value = $"{snapshot.SampleDurationSeconds:F1} seconds, {snapshot.Cores.Count} logical processor(s) sampled (passive, no load test)",
                Source = "Win32_PerfRawData_PerfOS_Processor"
            }
        };

        if (snapshot.Total is { } total)
            evidence.Add(Row("Total Activity (_Total)", total));

        foreach (var core in snapshot.Cores.OrderBy(c => c.Instance, StringComparer.OrdinalIgnoreCase))
            evidence.Add(Row($"CPU {core.Instance}", core));

        if (snapshot.Total is { ProcessorPercent: double cpu } t2
            && (t2.InterruptsPerSecond is double ir && ir >= _options.InterruptsPerSecondSuspicious
                || t2.DpcsPerSecond is double dr && dr >= _options.DpcsPerSecondSuspicious))
        {
            evidence.Add(new DiagnosticEvidence
            {
                Description = "CPU Correlation",
                Value = $"Elevated interrupt/DPC activity was observed while total CPU was {cpu:F1}% busy. This is a correlation, not a " +
                        "causal claim - it shows the elevated activity coincided with CPU load during the window.",
                Source = "correlation analysis (same counters)"
            });
        }

        if (snapshot.InventoryAvailable)
        {
            var driverCount = snapshot.LoadedDrivers.Count;
            var deviceCount = snapshot.Devices.Count;
            var topDrivers = string.Join(", ", snapshot.LoadedDrivers.Take(10));
            var topDevices = string.Join(", ", snapshot.Devices.Take(10));
            var driverSuffix = driverCount > 10 ? $" (and {driverCount - 10} more)" : "";
            var deviceSuffix = deviceCount > 10 ? $" (and {deviceCount - 10} more)" : "";

            evidence.Add(new DiagnosticEvidence
            {
                Description = "Loaded Drivers (context)",
                Value = $"{driverCount} running driver(s){driverSuffix}: {topDrivers}. Context only - this check does not attribute activity to any of them.",
                Source = "Win32_SystemDriver"
            });
            evidence.Add(new DiagnosticEvidence
            {
                Description = "Devices Present (context)",
                Value = $"{deviceCount} device(s){deviceSuffix}: {topDevices}. Context only - no attribution is made.",
                Source = "Win32_PnPEntity"
            });
        }
        else
        {
            evidence.Add(new DiagnosticEvidence
            {
                Description = "Driver/Device Inventory",
                Value = "unavailable on this system; reported as unknown rather than empty.",
                Source = "Win32_SystemDriver / Win32_PnPEntity"
            });
        }

        evidence.Add(new DiagnosticEvidence
        {
            Description = "Measurement Honesty",
            Value = "Performance counters provide interrupt/DPC activity RATES, not per-DPC latency. The deprecated % DPC Time / % Interrupt " +
                    "Time counters (always zero since Windows 8) are not used. True DPC latency requires an admin ETW kernel trace.",
            Source = "documented in SPEC.md Phase 11"
        });

        evidence.Add(new DiagnosticEvidence
        {
            Description = "Threshold Reference",
            Value = $"Interrupts: >= {_options.InterruptsPerSecondSuspicious:N0}/s suspicious, >= {_options.InterruptsPerSecondWarning:N0}/s high " +
                    $"(total). DPCs queued: >= {_options.DpcsPerSecondSuspicious:N0}/s suspicious, >= {_options.DpcsPerSecondWarning:N0}/s high. " +
                    $"Privileged time: >= {_options.PrivilegedTimeSuspicious:F0}% suspicious, >= {_options.PrivilegedTimeWarning:F0}% high. " +
                    "Heuristic reference values; absolute rates vary by machine and workload.",
            Source = "documented in SPEC.md Phase 11"
        });

        return evidence;
    }

    private static DiagnosticEvidence Row(string description, InterruptCoreSample sample)
        => new()
        {
            Description = description,
            Value = $"interrupts {sample.InterruptsPerSecond ?? 0:F0}/s | DPCs queued {sample.DpcsPerSecond ?? 0:F0}/s | " +
                    $"DPC rate {sample.DpcRate ?? 0:F0} | privileged {sample.PrivilegedPercent ?? 0:F1}% | CPU busy {sample.ProcessorPercent ?? 0:F1}%",
            Source = "Win32_PerfRawData_PerfOS_Processor"
        };

    private static IReadOnlyList<DiagnosticRecommendation> BuildRecommendations(InterruptAssessment assessment)
    {
        if (assessment.Verdict == InterruptVerdict.Healthy)
            return Array.Empty<DiagnosticRecommendation>();

        return new List<DiagnosticRecommendation>
        {
            new()
            {
                Text = "Run a true DPC-latency ETW trace as administrator (LatencyMon, or Windows Performance Recorder with the kernel " +
                       "interrupt/DPC providers) to measure real per-DPC latency and identify the offending driver. This check only " +
                       "detected elevated activity - it cannot name the driver.",
                RequiresAdmin = true,
                Priority = 1
            },
            new()
            {
                Text = "Check Device Manager for devices reporting errors and update drivers from the manufacturer. Audio, network, " +
                       "chipset, and storage drivers are common sources of heavy interrupt/DPC load.",
                RequiresAdmin = false,
                Priority = 2
            },
            new()
            {
                Text = "If a real latency trace confirms one driver, update or reinstall it (a clean install) from the manufacturer. " +
                       "Never uninstall a driver automatically - remove it only deliberately after confirming the fault.",
                RequiresAdmin = true,
                Priority = 3
            },
            new()
            {
                Text = "Re-run this check after a few days and at a different time of day; a short sample is a point-in-time snapshot.",
                RequiresAdmin = false,
                Priority = 4
            }
        };
    }

    private static DiagnosticSeverity ToSeverity(InterruptVerdict verdict)
        => verdict switch
        {
            InterruptVerdict.Suspicious => DiagnosticSeverity.Suspicious,
            InterruptVerdict.Warning => DiagnosticSeverity.Warning,
            _ => DiagnosticSeverity.Healthy
        };
}