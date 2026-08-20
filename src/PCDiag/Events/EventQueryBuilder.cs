using System.Text;

namespace PCDiag.Events;

/// <summary>
/// Pure builder that turns an <see cref="EventChannelFilter"/> into an XPath query
/// string for <c>System.Diagnostics.Eventing.Reader.EventLogQuery</c>. The OS
/// applies the filter natively, so the reader only ever retrieves events that
/// match an inspected category. Isolated and unit-tested.
/// </summary>
public static class EventQueryBuilder
{
    /// <summary>
    /// Build an XPath filter for a channel. With no providers and no event IDs the
    /// whole channel is matched (used for operational channels whose providers are
    /// already category-relevant).
    /// </summary>
    public static string Build(EventChannelFilter filter)
    {
        var conditions = new List<string>();

        if (filter.Providers.Count > 0)
        {
            var names = filter.Providers.Select(p => $"Provider[@Name='{Escape(p)}']");
            conditions.Add(string.Join(" or ", names));
        }

        if (filter.EventIds.Count > 0)
        {
            var ids = filter.EventIds.Select(id => $"EventID={id}").Distinct();
            conditions.Add(string.Join(" or ", ids));
        }

        if (conditions.Count == 0)
            return "*";

        return $"*[System[{string.Join(" or ", conditions)}]]";
    }

    /// <summary>
    /// The Windows Event Log XPath engine rejects queries with too many OR terms
    /// ("This operator is unsupported by this implementation of the filter"),
    /// so providers are chunked into multiple smaller queries, each under the
    /// proven-safe term budget. The event IDs are ORed into every provider chunk.
    /// </summary>
    public static IReadOnlyList<string> BuildChunked(EventChannelFilter filter, int maxProvidersPerQuery = 12)
    {
        if (filter.Providers.Count == 0)
            return new[] { Build(filter) };

        var queries = new List<string>();
        foreach (var chunk in filter.Providers.Chunk(maxProvidersPerQuery))
        {
            queries.Add(Build(filter with { Providers = chunk.ToArray() }));
        }

        return queries;
    }

    private static string Escape(string value)
        => value.Replace("'", "''");
}