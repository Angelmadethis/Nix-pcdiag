using PCDiag.Core;
using PCDiag.Events;

namespace PCDiag.Tests.Events;

public class EventAggregatorTests
{
    private static readonly TimeSpan Window = TimeSpan.FromDays(14);

    private static ClassifiedEvent C(EventCategory category, DiagnosticSeverity severity, string provider, int id, DateTime? time = null)
        => new()
        {
            Record = Ev.New(provider, id, time),
            Category = category,
            Severity = severity,
            Component = provider
        };

    [Fact]
    public void EmptyInput_ProducesNoSummaries()
    {
        var result = EventAggregator.Aggregate(Array.Empty<ClassifiedEvent>(), Window, EventLogOptions.Default);
        Assert.Empty(result);
    }

    [Fact]
    public void SingleEvent_CountsOne()
    {
        var result = EventAggregator.Aggregate(new[] { C(EventCategory.Whea, DiagnosticSeverity.Suspicious, "Microsoft-Windows-WHEA-Logger", 18) }, Window, EventLogOptions.Default);

        var summary = Assert.Single(result);
        Assert.Equal(EventCategory.Whea, summary.Category);
        Assert.Equal(1, summary.Count);
        Assert.Equal(1.0 / 14.0, summary.FrequencyPerDay, 6);
        Assert.Equal(DiagnosticSeverity.Suspicious, summary.MaxSeverity);
        Assert.NotNull(summary.First);
        Assert.Equal(summary.First, summary.Last);
    }

    [Fact]
    public void MultipleCategories_ProduceSeparateSummaries()
    {
        var events = new[]
        {
            C(EventCategory.Whea, DiagnosticSeverity.Suspicious, "Microsoft-Windows-WHEA-Logger", 18),
            C(EventCategory.Disk, DiagnosticSeverity.Warning, "disk", 11),
            C(EventCategory.Disk, DiagnosticSeverity.Warning, "disk", 7)
        };

        var result = EventAggregator.Aggregate(events, Window, EventLogOptions.Default);

        Assert.Equal(2, result.Count);
        var whea = result.Single(s => s.Category == EventCategory.Whea);
        var disk = result.Single(s => s.Category == EventCategory.Disk);
        Assert.Equal(1, whea.Count);
        Assert.Equal(2, disk.Count);
    }

    [Fact]
    public void EventIds_AreGroupedWithCountsAndMaxSeverity()
    {
        var t0 = DateTime.UtcNow.AddDays(-2);
        var events = new[]
        {
            C(EventCategory.Whea, DiagnosticSeverity.Suspicious, "Microsoft-Windows-WHEA-Logger", 18, t0),
            C(EventCategory.Whea, DiagnosticSeverity.Suspicious, "Microsoft-Windows-WHEA-Logger", 18, t0.AddHours(1)),
            C(EventCategory.Whea, DiagnosticSeverity.Critical, "Microsoft-Windows-WHEA-Logger", 1, t0.AddHours(2))
        };

        var result = EventAggregator.Aggregate(events, Window, EventLogOptions.Default);
        var whea = result.Single(s => s.Category == EventCategory.Whea);

        Assert.Equal(2, whea.EventIds.Count);
        var id18 = whea.EventIds.Single(g => g.EventId == 18);
        var id1 = whea.EventIds.Single(g => g.EventId == 1);
        Assert.Equal(2, id18.Count);
        Assert.Equal(DiagnosticSeverity.Suspicious, id18.Severity);
        Assert.Equal(1, id1.Count);
        Assert.Equal(DiagnosticSeverity.Critical, id1.Severity);
        Assert.Equal(DiagnosticSeverity.Critical, whea.MaxSeverity);
    }

    [Fact]
    public void Components_AreGroupedWithCounts()
    {
        var events = new[]
        {
            C(EventCategory.Disk, DiagnosticSeverity.Warning, "disk", 11),
            C(EventCategory.Disk, DiagnosticSeverity.Warning, "disk", 7),
            C(EventCategory.Disk, DiagnosticSeverity.Warning, "Microsoft-Windows-StorPort", 153)
        };

        var result = EventAggregator.Aggregate(events, Window, EventLogOptions.Default);
        var disk = result.Single(s => s.Category == EventCategory.Disk);

        Assert.Equal(2, disk.Components.Count);
        Assert.Equal(2, disk.Components.Single(c => c.Component == "disk").Count);
        Assert.Equal(1, disk.Components.Single(c => c.Component == "Microsoft-Windows-StorPort").Count);
    }

    [Fact]
    public void FirstAndLast_UseMinAndMaxTimestamps()
    {
        var early = DateTime.UtcNow.AddDays(-10);
        var late = DateTime.UtcNow.AddHours(-1);
        var events = new[]
        {
            C(EventCategory.Whea, DiagnosticSeverity.Suspicious, "Microsoft-Windows-WHEA-Logger", 18, late),
            C(EventCategory.Whea, DiagnosticSeverity.Suspicious, "Microsoft-Windows-WHEA-Logger", 19, early),
            C(EventCategory.Whea, DiagnosticSeverity.Suspicious, "Microsoft-Windows-WHEA-Logger", 18, early.AddHours(1))
        };

        var result = EventAggregator.Aggregate(events, Window, EventLogOptions.Default);
        var whea = result.Single(s => s.Category == EventCategory.Whea);

        Assert.Equal(early, whea.First);
        Assert.Equal(late, whea.Last);
    }

    [Fact]
    public void NullTimestamps_AreCountedButExcludedFromFirstLast()
    {
        var withTime = DateTime.UtcNow.AddDays(-1);
        var events = new[]
        {
            new ClassifiedEvent
            {
                Record = new EventLogRecord { Channel = "System", Provider = "Microsoft-Windows-WHEA-Logger", EventId = 18, TimeCreated = null },
                Category = EventCategory.Whea,
                Severity = DiagnosticSeverity.Suspicious,
                Component = "WHEA"
            },
            C(EventCategory.Whea, DiagnosticSeverity.Suspicious, "Microsoft-Windows-WHEA-Logger", 18, withTime)
        };

        var result = EventAggregator.Aggregate(events, Window, EventLogOptions.Default);
        var whea = result.Single(s => s.Category == EventCategory.Whea);

        Assert.Equal(2, whea.Count);
        Assert.Equal(withTime, whea.First);
        Assert.Equal(withTime, whea.Last);
    }

    [Fact]
    public void Frequency_UsesWindowDuration()
    {
        var events = new[]
        {
            C(EventCategory.Disk, DiagnosticSeverity.Warning, "disk", 11),
            C(EventCategory.Disk, DiagnosticSeverity.Warning, "disk", 11),
            C(EventCategory.Disk, DiagnosticSeverity.Warning, "disk", 11),
            C(EventCategory.Disk, DiagnosticSeverity.Warning, "disk", 11)
        };

        var result = EventAggregator.Aggregate(events, Window, EventLogOptions.Default);
        var disk = result.Single(s => s.Category == EventCategory.Disk);

        Assert.Equal(4.0 / 14.0, disk.FrequencyPerDay, 6);
    }

    [Fact]
    public void MaxSeverity_IsWorstEventSeverity()
    {
        var events = new[]
        {
            C(EventCategory.ServiceFailure, DiagnosticSeverity.Info, "Service Control Manager", 7036),
            C(EventCategory.ServiceFailure, DiagnosticSeverity.Warning, "Service Control Manager", 7034)
        };

        var result = EventAggregator.Aggregate(events, Window, EventLogOptions.Default);
        var services = result.Single(s => s.Category == EventCategory.ServiceFailure);

        Assert.Equal(DiagnosticSeverity.Warning, services.MaxSeverity);
    }

    [Fact]
    public void ConcerningCount_CountsOnlySuspiciousOrWorse()
    {
        var events = new[]
        {
            C(EventCategory.ServiceFailure, DiagnosticSeverity.Info, "Service Control Manager", 7040),
            C(EventCategory.ServiceFailure, DiagnosticSeverity.Info, "Service Control Manager", 7040),
            C(EventCategory.ServiceFailure, DiagnosticSeverity.Info, "Service Control Manager", 7040),
            C(EventCategory.ServiceFailure, DiagnosticSeverity.Warning, "Service Control Manager", 7031),
            C(EventCategory.ServiceFailure, DiagnosticSeverity.Warning, "Service Control Manager", 7031)
        };

        var result = EventAggregator.Aggregate(events, Window, EventLogOptions.Default);
        var services = result.Single(s => s.Category == EventCategory.ServiceFailure);

        Assert.Equal(5, services.Count);
        Assert.Equal(2, services.ConcerningCount);
    }
}