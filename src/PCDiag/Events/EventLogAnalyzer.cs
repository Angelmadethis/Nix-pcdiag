using PCDiag.Core;

namespace PCDiag.Events;

/// <summary>
/// The full result of an event log analysis: per-category summaries, detected
/// patterns, channel availability, and the observation window.
/// </summary>
public sealed record EventLogAnalysis
{
    /// <summary>Start (oldest) of the observation window (UTC).</summary>
    public required DateTime WindowStart { get; init; }

    /// <summary>End (newest) of the observation window (UTC).</summary>
    public required DateTime WindowEnd { get; init; }

    /// <summary>Aggregated per-category summaries (only inspected categories with events).</summary>
    public required IReadOnlyList<EventCategorySummary> Categories { get; init; }

    /// <summary>Detected patterns across the included categories.</summary>
    public required IReadOnlyList<EventPattern> Patterns { get; init; }

    /// <summary>Per-channel availability so unavailable channels are reported, not hidden.</summary>
    public required IReadOnlyList<EventChannelStatus> Channels { get; init; }

    /// <summary>The classified events that produced the summaries.</summary>
    public required IReadOnlyList<ClassifiedEvent> ClassifiedEvents { get; init; }

    /// <summary>Total relevant events across all included categories.</summary>
    public int TotalEvents => Categories.Sum(c => c.Count);

    /// <summary>The worst severity across categories and patterns.</summary>
    public DiagnosticSeverity MaxSeverity
    {
        get
        {
            var categorySeverity = Categories.Count > 0 ? Categories.Max(c => c.MaxSeverity) : DiagnosticSeverity.Healthy;
            var patternSeverity = Patterns.Count > 0 ? Patterns.Max(p => p.Severity) : DiagnosticSeverity.Healthy;
            return (DiagnosticSeverity)Math.Max((int)categorySeverity, (int)patternSeverity);
        }
    }

    /// <summary>How many of the configured channels could not be read.</summary>
    public int UnavailableChannels => Channels.Count(c => !c.IsAvailable);
}

/// <summary>
/// Orchestrates the event log engine: query the source, classify, aggregate, and
/// detect patterns. Shared by the Event Log, WHEA, and Driver checks.
/// </summary>
public sealed class EventLogAnalyzer
{
    private readonly IEventLogSource _source;
    private readonly EventLogOptions _options;

    public EventLogAnalyzer(IEventLogSource? source = null, EventLogOptions? options = null)
    {
        _source = source ?? new WindowsEventLogSource();
        _options = options ?? EventLogOptions.Default;
    }

    /// <summary>
    /// Analyze event logs. When <paramref name="categories"/> is non-empty only those
    /// categories are aggregated and their patterns detected.
    /// </summary>
    public EventLogAnalysis Analyze(IReadOnlyList<EventCategory>? categories = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = new EventLogQueryRequest
        {
            Window = _options.Window,
            MaxEventsPerChannel = _options.MaxEventsPerChannel,
            Channels = _options.Channels
        };

        var result = _source.Query(request);
        cancellationToken.ThrowIfCancellationRequested();

        var classified = result.Events
            .Select(e => EventClassifier.Classify(e))
            .Where(c => c is not null)
            .Cast<ClassifiedEvent>()
            .ToList();

        if (categories is { Count: > 0 })
            classified = classified.Where(c => categories.Contains(c.Category)).ToList();

        var summaries = EventAggregator.Aggregate(classified, _options.Window, _options);
        var patterns = EventPatternDetector.Detect(summaries, _options);
        var now = DateTime.UtcNow;

        return new EventLogAnalysis
        {
            WindowStart = now - _options.Window,
            WindowEnd = now,
            Categories = summaries,
            Patterns = patterns,
            Channels = result.Channels,
            ClassifiedEvents = classified
        };
    }
}