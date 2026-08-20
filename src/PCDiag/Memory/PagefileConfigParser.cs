namespace PCDiag.Memory;

/// <summary>
/// Pure parser for the registry PagingFiles value. Each entry is "path" or
/// "path min max". A bare path (no sizes) or "path 0 0" means Windows manages the
/// size automatically. Whitespace-only or empty input means no pagefile is configured.
/// </summary>
public static class PagefileConfigParser
{
    /// <summary>Parse raw registry entries into a <see cref="PagefileConfig"/>.</summary>
    public static PagefileConfig Parse(IReadOnlyList<string>? rawEntries)
    {
        if (rawEntries is null)
            return new PagefileConfig { IsSystemManaged = false, Entries = Array.Empty<string>(), Available = false };

        var entries = rawEntries
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .ToList();

        var hasCustomSize = entries.Any(e =>
        {
            var parts = e.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 3 && parts.Skip(1).Any(p => p != "0");
        });

        return new PagefileConfig
        {
            IsSystemManaged = !hasCustomSize && entries.Count > 0,
            Entries = entries,
            Available = true
        };
    }
}