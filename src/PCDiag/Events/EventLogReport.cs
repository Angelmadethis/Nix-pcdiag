using PCDiag.Core;

namespace PCDiag.Events;

/// <summary>
/// Pure formatting helpers that turn an <see cref="EventLogAnalysis"/> into evidence
/// rows and prose for the diagnostic checks. Kept separate from the checks so the
/// exact wording is consistent across the Event Log, WHEA, and Driver checks.
/// </summary>
public static class EventLogReport
{
    /// <summary>Evidence row summarizing one category (count, window, first/last, IDs, components, frequency, severity).</summary>
    public static DiagnosticEvidence CategoryRow(EventCategorySummary summary, TimeSpan window)
    {
        var idText = summary.EventIds.Count == 0
            ? "none"
            : string.Join(", ", summary.EventIds.Select(g => g.Count == 1 ? $"{g.EventId}" : $"{g.EventId} ({g.Count})"));
        var componentText = summary.Components.Count == 0
            ? "unknown"
            : string.Join(", ", summary.Components.Select(c => c.Count == 1 ? c.Component : $"{c.Component} ({c.Count})"));
        var firstText = summary.First?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "unknown";
        var lastText = summary.Last?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "unknown";

        var value =
            $"Count: {summary.Count} | Frequency: {summary.FrequencyPerDay:F2}/day over {FormatWindow(window)} | " +
            $"First: {firstText} | Last: {lastText} | " +
            $"Event IDs: {idText} | Components: {componentText} | Severity: {summary.MaxSeverity}";

        return new DiagnosticEvidence
        {
            Description = summary.Label,
            Value = value,
            Source = "Windows Event Log"
        };
    }

    /// <summary>Evidence row describing a detected pattern.</summary>
    public static DiagnosticEvidence PatternRow(EventPattern pattern)
        => new()
        {
            Description = pattern.Name,
            Value = pattern.Description,
            Source = "Windows Event Log"
        };

    /// <summary>Evidence row listing how many channels were inspected and any that were unavailable.</summary>
    public static DiagnosticEvidence ChannelsRow(IReadOnlyList<EventChannelStatus> channels)
    {
        var available = channels.Count(c => c.IsAvailable);
        var unavailable = channels.Where(c => !c.IsAvailable).ToList();

        var value = unavailable.Count == 0
            ? $"{available} channel(s) inspected successfully"
            : $"{available} channel(s) inspected; {unavailable.Count} unavailable: " +
              string.Join("; ", unavailable.Select(c => $"{c.Channel} ({c.Reason})"));

        return new DiagnosticEvidence
        {
            Description = "Inspected Channels",
            Value = value,
            Source = "Windows Event Log"
        };
    }

    /// <summary>Evidence row for the observation window and total relevant events.</summary>
    public static DiagnosticEvidence WindowRow(EventLogAnalysis analysis)
        => new()
        {
            Description = "Observation Window",
            Value = $"{analysis.WindowStart.ToLocalTime():yyyy-MM-dd HH:mm} to {analysis.WindowEnd.ToLocalTime():yyyy-MM-dd HH:mm} " +
                    $"({analysis.TotalEvents} relevant event(s), {analysis.UnavailableChannels} unavailable channel(s))",
            Source = "Windows Event Log"
        };

    private static string FormatWindow(TimeSpan window)
    {
        if (window.TotalDays >= 1)
            return $"{window.TotalDays:F0} days";
        if (window.TotalHours >= 1)
            return $"{window.TotalHours:F0} hours";
        return $"{window.TotalMinutes:F0} minutes";
    }
}