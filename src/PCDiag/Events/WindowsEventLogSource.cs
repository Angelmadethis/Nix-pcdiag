using System.Diagnostics.Eventing.Reader;

namespace PCDiag.Events;

/// <summary>
/// Real Windows Event Log reader backed by
/// <see cref="System.Diagnostics.Eventing.Reader.EventLogReader"/>. Queries run
/// newest-first with an XPath filter so the OS skips unrelated events, and reads are
/// bounded per channel. Missing or unreadable channels are reported as unavailable
/// rather than thrown, so reports distinguish "no events" from "cannot read".
/// </summary>
public sealed class WindowsEventLogSource : IEventLogSource
{
    private const int MaxMessageLength = 300;

    public EventLogQueryResult Query(EventLogQueryRequest request)
    {
        var events = new List<EventLogRecord>();
        var channels = new List<EventChannelStatus>();
        var windowStart = DateTime.UtcNow - request.Window;

        foreach (var filter in request.Channels)
        {
            try
            {
                var found = ReadChannel(filter, windowStart, request.MaxEventsPerChannel);
                events.AddRange(found);
                channels.Add(new EventChannelStatus { Channel = filter.Channel, IsAvailable = true });
            }
            catch (EventLogNotFoundException)
            {
                channels.Add(new EventChannelStatus { Channel = filter.Channel, IsAvailable = false, Reason = "Channel not found on this system." });
            }
            catch (UnauthorizedAccessException)
            {
                channels.Add(new EventChannelStatus { Channel = filter.Channel, IsAvailable = false, Reason = "Access denied (needs elevated privileges)." });
            }
            catch (Exception ex)
            {
                channels.Add(new EventChannelStatus { Channel = filter.Channel, IsAvailable = false, Reason = $"Could not be read: {ex.Message}" });
            }
        }

        return new EventLogQueryResult
        {
            Events = events,
            Channels = channels
        };
    }

    private static IReadOnlyList<EventLogRecord> ReadChannel(EventChannelFilter filter, DateTime windowStart, int maxEvents)
    {
        var records = new List<EventLogRecord>();

        foreach (var queryString in EventQueryBuilder.BuildChunked(filter))
        {
            var query = new EventLogQuery(filter.Channel, PathType.LogName, queryString)
            {
                ReverseDirection = true
            };

            using var reader = new EventLogReader(query) { BatchSize = 128 };

            for (int i = 0; i < maxEvents; i++)
            {
                EventRecord? record;
                try
                {
                    record = reader.ReadEvent();
                }
                catch
                {
                    break;
                }

                if (record is null)
                    break;

                using (record)
                {
                    if (record.TimeCreated is DateTime created && created.ToUniversalTime() < windowStart)
                        break;

                    records.Add(ToRecord(record, filter.Channel));
                }
            }
        }

        return records;
    }

    private static EventLogRecord ToRecord(EventRecord record, string channel)
    {
        var time = record.TimeCreated is DateTime created
            ? DateTime.SpecifyKind(created, created.Kind == DateTimeKind.Utc ? DateTimeKind.Utc : DateTimeKind.Local).ToUniversalTime()
            : (DateTime?)null;

        return new EventLogRecord
        {
            Channel = channel,
            Provider = record.ProviderName ?? "Unknown",
            EventId = record.Id,
            Level = record.Level,
            TimeCreated = time,
            Message = Truncate(SafeFormat(record))
        };
    }

    private static string? SafeFormat(EventRecord record)
    {
        try
        {
            return record.FormatDescription();
        }
        catch
        {
            return null;
        }
    }

    private static string? Truncate(string? message)
    {
        if (string.IsNullOrEmpty(message))
            return message;
        return message.Length <= MaxMessageLength ? message : message[..MaxMessageLength] + "…";
    }
}