using PCDiag.Core;

namespace PCDiag.Events;

/// <summary>
/// A classified event: an event log record plus the category, base severity, and
/// affected component assigned by the pure <see cref="EventClassifier"/>.
/// </summary>
public sealed record ClassifiedEvent
{
    /// <summary>The raw event record.</summary>
    public required EventLogRecord Record { get; init; }

    /// <summary>The diagnostic category this event belongs to (never <see cref="EventCategory.None"/> here).</summary>
    public required EventCategory Category { get; init; }

    /// <summary>The base severity assigned to this event before pattern amplification.</summary>
    public required DiagnosticSeverity Severity { get; init; }

    /// <summary>A friendly name for the affected component (usually the provider).</summary>
    public required string Component { get; init; }
}