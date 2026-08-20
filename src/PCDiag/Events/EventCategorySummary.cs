using PCDiag.Core;

namespace PCDiag.Events;

/// <summary>Count and severity of events for a single event ID within a category.</summary>
public sealed record EventIdGroup
{
    /// <summary>The event ID.</summary>
    public required int EventId { get; init; }

    /// <summary>How many events with this ID were seen in the window.</summary>
    public required int Count { get; init; }

    /// <summary>The most severe classification observed for this ID.</summary>
    public required DiagnosticSeverity Severity { get; init; }
}

/// <summary>Count of events attributed to a single affected component within a category.</summary>
public sealed record ComponentGroup
{
    /// <summary>The affected component (friendly provider name).</summary>
    public required string Component { get; init; }

    /// <summary>How many events were attributed to this component in the window.</summary>
    public required int Count { get; init; }
}

/// <summary>
/// Aggregated view of one event category over the observation window. This is the
/// unit reported per category: event count, first/most recent occurrence, event IDs,
/// affected components, frequency, and max severity.
/// </summary>
public sealed record EventCategorySummary
{
    /// <summary>The category.</summary>
    public required EventCategory Category { get; init; }

    /// <summary>Human-readable label (e.g. "WHEA Hardware Errors").</summary>
    public required string Label { get; init; }

    /// <summary>Total matching events in the window.</summary>
    public required int Count { get; init; }

    /// <summary>Events at Suspicious severity or worse (the count used for repetition patterns).
    /// Informational events that happen to match a category do not inflate pattern detection.</summary>
    public required int ConcerningCount { get; init; }

    /// <summary>First (oldest) occurrence in the window.</summary>
    public DateTime? First { get; init; }

    /// <summary>Most recent occurrence in the window.</summary>
    public DateTime? Last { get; init; }

    /// <summary>Event IDs seen, grouped with counts and severity.</summary>
    public IReadOnlyList<EventIdGroup> EventIds { get; init; } = Array.Empty<EventIdGroup>();

    /// <summary>Affected components, grouped with counts.</summary>
    public IReadOnlyList<ComponentGroup> Components { get; init; } = Array.Empty<ComponentGroup>();

    /// <summary>Events per day over the observation window.</summary>
    public double FrequencyPerDay { get; init; }

    /// <summary>Highest severity seen in this category.</summary>
    public required DiagnosticSeverity MaxSeverity { get; init; }
}

/// <summary>Human-readable labels for each inspected category.</summary>
public static class EventCategoryLabel
{
    public static string For(EventCategory category)
        => category switch
        {
            EventCategory.Whea => "WHEA Hardware Errors",
            EventCategory.Disk => "Disk Errors",
            EventCategory.Ntfs => "NTFS Filesystem Errors",
            EventCategory.StorageController => "Storage Controller (storahci/stornvme)",
            EventCategory.DisplayGpu => "Display / GPU Driver",
            EventCategory.KernelPower => "Kernel-Power",
            EventCategory.NetworkAdapter => "Network Adapter",
            EventCategory.Usb => "USB",
            EventCategory.ServiceFailure => "Service Failures",
            EventCategory.DriverFailure => "Driver Failures",
            _ => category.ToString()
        };
}