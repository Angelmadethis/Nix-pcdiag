namespace PCDiag.Events;

/// <summary>
/// Describes how to read one channel: which channel to read, which providers and
/// event IDs to keep. Used to build an XPath filter so the OS does the filtering
/// natively and the tool never reads unrelated events.
/// </summary>
public sealed record EventChannelFilter
{
    /// <summary>The channel/log name (e.g. "System", "Microsoft-Windows-WHEA-Logger/Operational").</summary>
    public required string Channel { get; init; }

    /// <summary>Provider names to match. Empty means "any provider" for this channel.</summary>
    public IReadOnlyList<string> Providers { get; init; } = Array.Empty<string>();

    /// <summary>Event IDs to match, in addition to any provider names.</summary>
    public IReadOnlyList<int> EventIds { get; init; } = Array.Empty<int>();
}

/// <summary>A bounded read request for the event log engine.</summary>
public sealed record EventLogQueryRequest
{
    /// <summary>How far back to inspect (e.g. 14 days).</summary>
    public required TimeSpan Window { get; init; }

    /// <summary>Maximum events read from any single channel before stopping.</summary>
    public required int MaxEventsPerChannel { get; init; }

    /// <summary>The channels to inspect.</summary>
    public required IReadOnlyList<EventChannelFilter> Channels { get; init; }
}

/// <summary>Whether a channel could be read and, when not, why.</summary>
public sealed record EventChannelStatus
{
    /// <summary>The channel name.</summary>
    public required string Channel { get; init; }

    /// <summary>True when the channel was read successfully (even if it contained no matching events).</summary>
    public required bool IsAvailable { get; init; }

    /// <summary>Human-readable reason when the channel was unavailable.</summary>
    public string? Reason { get; init; }
}

/// <summary>The result of a bounded event log read.</summary>
public sealed record EventLogQueryResult
{
    /// <summary>The events read, newest first.</summary>
    public required IReadOnlyList<EventLogRecord> Events { get; init; }

    /// <summary>Per-channel availability so reports can distinguish unavailable from healthy.</summary>
    public required IReadOnlyList<EventChannelStatus> Channels { get; init; }
}

/// <summary>
/// Abstraction over the Windows event log reader. Implementations must never throw:
/// unreadable or missing channels are reported as unavailable channel statuses.
/// </summary>
public interface IEventLogSource
{
    /// <summary>Read matching events within the request window. Never throws.</summary>
    EventLogQueryResult Query(EventLogQueryRequest request);
}