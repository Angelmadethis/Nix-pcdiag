namespace PCDiag.Events;

/// <summary>
/// A single Windows event log record as read from a channel. This is the data
/// contract produced by <see cref="IEventLogSource"/> and consumed by the
/// classifier and aggregator. The message is the localized rendered description,
/// when the provider provides one (some providers render an empty message).
/// </summary>
public sealed record EventLogRecord
{
    /// <summary>The channel/log the event was read from (e.g. "System").</summary>
    public required string Channel { get; init; }

    /// <summary>The provider (event source) name, e.g. "Microsoft-Windows-WHEA-Logger".</summary>
    public required string Provider { get; init; }

    /// <summary>The event ID as reported by the provider.</summary>
    public required int EventId { get; init; }

    /// <summary>The event level (0=LogAlways, 1=Critical, 2=Error, 3=Warning, 4=Information, 5=Verbose), when available.</summary>
    public byte? Level { get; init; }

    /// <summary>The UTC time the event was created, when available.</summary>
    public DateTime? TimeCreated { get; init; }

    /// <summary>The rendered message text, when the provider supplies one. May be null or empty.</summary>
    public string? Message { get; init; }
}