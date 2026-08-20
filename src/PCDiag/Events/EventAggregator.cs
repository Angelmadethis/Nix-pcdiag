using PCDiag.Core;

namespace PCDiag.Events;

/// <summary>
/// Pure aggregation of classified events into per-category summaries: event count,
/// first and most recent occurrence, event-ID groups, affected components, events
/// per day, and max severity. Unclassified events are never passed in here.
/// </summary>
public static class EventAggregator
{
    /// <summary>
    /// Aggregate classified events into one summary per inspected category. Categories
    /// with no events are omitted (absent = zero), matching the "distinguish
    /// unavailable from healthy" principle.
    /// </summary>
    public static IReadOnlyList<EventCategorySummary> Aggregate(
        IReadOnlyList<ClassifiedEvent> events,
        TimeSpan window,
        EventLogOptions options)
    {
        var grouped = events
            .GroupBy(e => e.Category)
            .OrderBy(g => g.Key);

        var summaries = new List<EventCategorySummary>();
        var windowDays = Math.Max(window.TotalDays, 1.0 / 24.0);

        foreach (var group in grouped)
        {
            var items = group.ToList();
            var withTime = items.Where(e => e.Record.TimeCreated is not null).ToList();

            var idGroups = items
                .GroupBy(e => e.Record.EventId)
                .Select(g => new EventIdGroup
                {
                    EventId = g.Key,
                    Count = g.Count(),
                    Severity = g.Max(e => e.Severity)
                })
                .OrderBy(g => g.EventId)
                .ToList();

            var componentGroups = items
                .GroupBy(e => e.Component)
                .Select(g => new ComponentGroup
                {
                    Component = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(g => g.Count)
                .ToList();

            var maxSeverity = items.Max(e => e.Severity);

            summaries.Add(new EventCategorySummary
            {
                Category = group.Key,
                Label = EventCategoryLabel.For(group.Key),
                Count = items.Count,
                ConcerningCount = idGroups.Where(g => g.Severity >= DiagnosticSeverity.Suspicious).Sum(g => g.Count),
                First = withTime.Count > 0 ? withTime.Min(e => e.Record.TimeCreated) : null,
                Last = withTime.Count > 0 ? withTime.Max(e => e.Record.TimeCreated) : null,
                EventIds = idGroups,
                Components = componentGroups,
                FrequencyPerDay = items.Count / windowDays,
                MaxSeverity = maxSeverity
            });
        }

        return summaries;
    }
}