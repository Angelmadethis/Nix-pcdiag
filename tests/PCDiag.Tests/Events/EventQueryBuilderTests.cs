using PCDiag.Events;

namespace PCDiag.Tests.Events;

public class EventQueryBuilderTests
{
    [Fact]
    public void EmptyFilter_ReturnsWholeChannel()
    {
        var filter = new EventChannelFilter { Channel = "System" };
        Assert.Equal("*", EventQueryBuilder.Build(filter));
    }

    [Fact]
    public void ProvidersOnly_BuildsProviderPredicate()
    {
        var filter = new EventChannelFilter
        {
            Channel = "System",
            Providers = new[] { "disk", "Ntfs" }
        };

        var query = EventQueryBuilder.Build(filter);

        Assert.Equal("*[System[Provider[@Name='disk'] or Provider[@Name='Ntfs']]]", query);
    }

    [Fact]
    public void EventIdsOnly_BuildsIdPredicate()
    {
        var filter = new EventChannelFilter
        {
            Channel = "System",
            EventIds = new[] { 11, 51, 11 }
        };

        var query = EventQueryBuilder.Build(filter);

        Assert.Equal("*[System[EventID=11 or EventID=51]]", query);
    }

    [Fact]
    public void ProvidersAndIds_AreCombined()
    {
        var filter = new EventChannelFilter
        {
            Channel = "System",
            Providers = new[] { "disk" },
            EventIds = new[] { 4101 }
        };

        var query = EventQueryBuilder.Build(filter);

        Assert.Equal("*[System[Provider[@Name='disk'] or EventID=4101]]", query);
    }

    [Fact]
    public void ProviderNameWithQuote_IsEscaped()
    {
        var filter = new EventChannelFilter
        {
            Channel = "System",
            Providers = new[] { "O'Brien's Driver" }
        };

        var query = EventQueryBuilder.Build(filter);

        Assert.Equal("*[System[Provider[@Name='O''Brien''s Driver']]]", query);
    }

    [Fact]
    public void ManyProviders_AreChunkedWithinBudget()
    {
        var filter = new EventChannelFilter
        {
            Channel = "System",
            Providers = Enumerable.Range(1, 30).Select(i => $"Provider{i}").ToArray(),
            EventIds = new[] { 4101 }
        };

        var queries = EventQueryBuilder.BuildChunked(filter, maxProvidersPerQuery: 12);

        Assert.Equal(3, queries.Count);
        Assert.All(queries, q => Assert.Contains("EventID=4101", q));
        Assert.Contains("Provider1", queries[0]);
        Assert.Contains("Provider12", queries[0]);
        Assert.DoesNotContain("Provider13", queries[0]);
        Assert.Contains("Provider30", queries[2]);
    }

    [Fact]
    public void NoProviders_ReturnsSingleQuery()
    {
        var filter = new EventChannelFilter { Channel = "System", EventIds = new[] { 11, 51 } };

        var queries = EventQueryBuilder.BuildChunked(filter);

        var query = Assert.Single(queries);
        Assert.Equal("*[System[EventID=11 or EventID=51]]", query);
    }
}