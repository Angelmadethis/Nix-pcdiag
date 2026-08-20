using PCDiag.Core;

namespace PCDiag.Events;

/// <summary>A detected repetition or critical event pattern across a category.</summary>
public sealed record EventPattern
{
    /// <summary>Short name, e.g. "Repeated GPU driver resets".</summary>
    public required string Name { get; init; }

    /// <summary>Evidence-based description of what was seen.</summary>
    public required string Description { get; init; }

    /// <summary>Severity of the pattern (independent of the base event severity).</summary>
    public required DiagnosticSeverity Severity { get; init; }
}

/// <summary>
/// Pure detector of meaningful patterns in aggregated event categories: repeated
/// WHEA / disk / GPU / network / USB / service / driver errors, plus single critical
/// signals such as fatal WHEA records, disk paging errors, unexpected shutdowns
/// (Kernel-Power 41), and bugchecks. It reports what the logs show, never a cause.
/// </summary>
public static class EventPatternDetector
{
    public static IReadOnlyList<EventPattern> Detect(
        IReadOnlyList<EventCategorySummary> summaries,
        EventLogOptions options)
    {
        var patterns = new List<EventPattern>();

        foreach (var summary in summaries)
        {
            switch (summary.Category)
            {
                case EventCategory.Whea:
                    if (HasAnyId(summary, 1, 20))
                    {
                        patterns.Add(new EventPattern
                        {
                            Name = "Fatal WHEA hardware error",
                            Description = $"A fatal WHEA hardware error record (event ID {(HasId(summary, 1) ? "1" : "20")}) was written. " +
                                "This records a serious hardware fault; the log does not identify the failing component by itself.",
                            Severity = DiagnosticSeverity.Critical
                        });
                    }
                    AddRepeated(patterns, summary, options.WheaSuspiciousCount, options.WheaWarningCount,
                        "Repeated WHEA errors",
                        "Multiple WHEA error records were written over the window. Corrected machine-check errors that repeat can " +
                        "indicate failing hardware; a single corrected occurrence is common and not itself alarming.");
                    break;

                case EventCategory.Disk:
                    if (HasAnyId(summary, 51))
                    {
                        patterns.Add(new EventPattern
                        {
                            Name = "Disk paging I/O error",
                            Description = "A disk event 51 (error while paging to/from disk) was recorded, which can indicate a failing disk or cabling.",
                            Severity = DiagnosticSeverity.Critical
                        });
                    }
                    AddRepeated(patterns, summary, options.DiskSuspiciousCount, options.DiskWarningCount,
                        "Repeated disk errors",
                        "Multiple disk errors (bad block, controller, surprise removal) were written over the window; repeating disk errors can indicate a failing drive.");
                    break;

                case EventCategory.Ntfs:
                    AddRepeated(patterns, summary, options.NtfsSuspiciousCount, options.NtfsWarningCount,
                        "Repeated NTFS filesystem errors",
                        "Multiple NTFS error events (corruption, repair needed) were written over the window.");
                    break;

                case EventCategory.StorageController:
                    AddRepeated(patterns, summary, options.StorageSuspiciousCount, options.StorageWarningCount,
                        "Repeated storage controller errors",
                        "Multiple storage controller errors (storahci/stornvme/StorPort) were written over the window; a failing controller or link can cause these.");
                    break;

                case EventCategory.DisplayGpu:
                    AddRepeated(patterns, summary, options.GpuSuspiciousCount, options.GpuWarningCount,
                        "Repeated GPU driver resets",
                        "The display driver stopped responding and recovered (TDR) multiple times over the window. Repeated TDR events can indicate " +
                        "a driver problem, overheating, or hardware instability - this log alone does not identify which.");
                    break;

                case EventCategory.KernelPower:
                    if (HasId(summary, 41))
                    {
                        patterns.Add(new EventPattern
                        {
                            Name = "Unexpected system shutdown",
                            Description = "Kernel-Power event 41 says the system rebooted without a clean shutdown. " +
                                "Possible causes include power loss, overheating, a crash, or hardware fault - this log alone does not determine the cause.",
                            Severity = DiagnosticSeverity.Critical
                        });
                    }
                    AddRepeated(patterns, summary, options.KernelPowerSuspiciousCount, options.KernelPowerWarningCount,
                        "Repeated Kernel-Power events",
                        "Multiple Kernel-Power error events were written over the window.");
                    break;

                case EventCategory.NetworkAdapter:
                    AddRepeated(patterns, summary, options.NetworkSuspiciousCount, options.NetworkWarningCount,
                        "Repeated network adapter resets",
                        "The network adapter reported errors/resets multiple times over the window. Repeating resets can indicate a driver issue, " +
                        "a faulty cable/port, or a failing adapter - this log alone does not identify which.");
                    break;

                case EventCategory.Usb:
                    AddRepeated(patterns, summary, options.UsbSuspiciousCount, options.UsbWarningCount,
                        "Repeated USB resets",
                        "USB host controllers/hubs reported resets or errors multiple times over the window; repeating resets can indicate a " +
                        "driver or power issue or a failing device.");
                    break;

                case EventCategory.ServiceFailure:
                    AddRepeated(patterns, summary, options.ServiceSuspiciousCount, options.ServiceWarningCount,
                        "Repeated service failures",
                        "Multiple Windows services failed to start or terminated unexpectedly over the window.");
                    break;

                case EventCategory.DriverFailure:
                    if (summary.MaxSeverity == DiagnosticSeverity.Critical)
                    {
                        patterns.Add(new EventPattern
                        {
                            Name = "System bugcheck",
                            Description = "A bugcheck (system crash) record was written. The system stopped and restarted after a bugcheck; " +
                                "this log does not identify the faulting driver by itself.",
                            Severity = DiagnosticSeverity.Critical
                        });
                    }
                    AddRepeated(patterns, summary, options.DriverSuspiciousCount, options.DriverWarningCount,
                        "Repeated driver failures",
                        "Multiple driver load/signature failures were recorded over the window.");
                    break;
            }
        }

        return patterns;
    }

    private static bool HasId(EventCategorySummary summary, int id)
        => summary.EventIds.Any(g => g.EventId == id);

    private static bool HasAnyId(EventCategorySummary summary, params int[] ids)
        => ids.Any(id => HasId(summary, id));

    private static void AddRepeated(
        List<EventPattern> patterns,
        EventCategorySummary summary,
        int suspicious,
        int warning,
        string name,
        string description)
    {
        if (summary.ConcerningCount < suspicious)
            return;

        var severity = summary.ConcerningCount >= warning
            ? DiagnosticSeverity.Warning
            : DiagnosticSeverity.Suspicious;

        patterns.Add(new EventPattern
        {
            Name = name,
            Description = $"{description} ({summary.ConcerningCount} concerning event(s) in the observation window.)",
            Severity = severity
        });
    }
}