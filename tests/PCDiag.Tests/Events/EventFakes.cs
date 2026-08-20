using PCDiag.Core;
using PCDiag.Events;

namespace PCDiag.Tests.Events;

internal sealed class FakeEventLogSource : IEventLogSource
{
    public List<EventLogRecord> Records { get; } = new();
    public List<EventChannelStatus> Statuses { get; } = new();

    public EventLogQueryResult Query(EventLogQueryRequest request)
    {
        var windowStart = DateTime.UtcNow - request.Window;
        var inWindow = Records
            .Where(r => r.TimeCreated is null || r.TimeCreated >= windowStart)
            .Take(request.MaxEventsPerChannel)
            .ToList();

        return new EventLogQueryResult { Events = inWindow, Channels = Statuses };
    }
}

internal static class Ev
{
    public static EventLogRecord New(
        string provider,
        int id,
        DateTime? time = null,
        byte? level = null,
        string channel = "System",
        string? message = null)
        => new()
        {
            Channel = channel,
            Provider = provider,
            EventId = id,
            Level = level,
            TimeCreated = time ?? DateTime.UtcNow,
            Message = message
        };

    public static EventCategorySummary Category(
        EventCategory category,
        int count,
        DiagnosticSeverity maxSeverity = DiagnosticSeverity.Warning,
        params (int Id, DiagnosticSeverity Severity)[] ids)
        => new()
        {
            Category = category,
            Label = EventCategoryLabel.For(category),
            Count = count,
            ConcerningCount = ids.Length == 0 || ids.All(g => g.Severity >= DiagnosticSeverity.Suspicious)
                ? count
                : ids.Count(g => g.Severity >= DiagnosticSeverity.Suspicious),
            First = DateTime.UtcNow.AddDays(-3),
            Last = DateTime.UtcNow.AddHours(-1),
            EventIds = ids
                .Select(g => new EventIdGroup { EventId = g.Id, Count = 1, Severity = g.Severity })
                .ToList(),
            Components = new[] { new ComponentGroup { Component = "TestComponent", Count = count } },
            FrequencyPerDay = count / 14.0,
            MaxSeverity = maxSeverity
        };
}

internal static class EventLogTestOptions
{
    /// <summary>Default options with a short window so fixed test timestamps stay in range.</summary>
    public static EventLogOptions WithWindow(TimeSpan window)
        => EventLogOptions.Default with { Window = window };
}