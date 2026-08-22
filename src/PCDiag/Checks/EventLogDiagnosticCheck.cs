using PCDiag.Core;
using PCDiag.Events;

namespace PCDiag.Checks;

/// <summary>
/// Base class for the event-log checks. Owns the <see cref="EventLogAnalyzer"/> and
/// the configured <see cref="EventLogOptions"/>, and exposes the focus categories
/// each check inspects. Concrete checks build their own result from the analysis.
/// The full analysis is cached in DiagnosticContext so the three event-log checks
/// (EventLog, WHEA, Driver) share one event-log query pass instead of three.
/// </summary>
public abstract class EventLogDiagnosticCheck : DiagnosticCheck
{
    private readonly EventLogAnalyzer _analyzer;

    protected EventLogOptions Options { get; }

    protected EventLogDiagnosticCheck(PCDiag.Events.IEventLogSource? source = null, EventLogOptions? options = null)
    {
        Options = options ?? EventLogOptions.Default;
        _analyzer = new EventLogAnalyzer(source, Options);
    }

    /// <summary>The categories this check inspects; empty means all categories.</summary>
    protected abstract IReadOnlyList<PCDiag.Events.EventCategory> FocusCategories { get; }

    protected async Task<EventLogAnalysis> RunAnalysisAsync(DiagnosticContext context)
    {
        return await Task.Run(() =>
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            // Use the cached full analysis when available (set by the first event-log check).
            var fullAnalysis = context.CachedEventLogAnalysis;
            if (fullAnalysis is null)
            {
                fullAnalysis = _analyzer.Analyze(categories: null, context.CancellationToken);
                context.CachedEventLogAnalysis = fullAnalysis;
            }

            // When no focus categories, return the full analysis as-is.
            if (FocusCategories.Count == 0)
                return fullAnalysis;

            // Filter the full analysis to the focused categories.
            var filtered = fullAnalysis.ClassifiedEvents
                .Where(c => FocusCategories.Contains(c.Category))
                .ToList();

            var summaries = EventAggregator.Aggregate(filtered, Options.Window, Options);
            var patterns = EventPatternDetector.Detect(summaries, Options);

            return new EventLogAnalysis
            {
                WindowStart = fullAnalysis.WindowStart,
                WindowEnd = fullAnalysis.WindowEnd,
                Categories = summaries,
                Patterns = patterns,
                Channels = fullAnalysis.Channels,
                ClassifiedEvents = filtered
            };
        }, context.CancellationToken).ConfigureAwait(false);
    }
}