using PCDiag.Core;
using PCDiag.Events;

namespace PCDiag.Checks;

/// <summary>
/// Base class for the event-log checks. Owns the <see cref="EventLogAnalyzer"/> and
/// the configured <see cref="EventLogOptions"/>, and exposes the focus categories
/// each check inspects. Concrete checks build their own result from the analysis.
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

    protected Task<EventLogAnalysis> RunAnalysisAsync(CancellationToken cancellationToken)
        => Task.Run(() => _analyzer.Analyze(FocusCategories, cancellationToken), cancellationToken);
}