using System.Management;

namespace PCDiag.Infrastructure;

/// <summary>
/// Safe wrapper around WMI/CIM queries.
/// Never throws: permission limitations, missing classes, and malformed rows
/// are treated as empty results so providers can degrade gracefully.
/// </summary>
public static class WmiQuery
{
    /// <summary>
    /// Run a WQL query and return its rows. Returns an empty collection on any failure.
    /// </summary>
    public static IReadOnlyList<ManagementBaseObject> Query(string wql, string scope = "root\\cimv2")
    {
        try
        {
            var searcher = new ManagementObjectSearcher(scope, wql);
            var results = new List<ManagementBaseObject>();
            foreach (ManagementBaseObject obj in searcher.Get())
            {
                results.Add(obj);
            }
            return results;
        }
        catch
        {
            return Array.Empty<ManagementBaseObject>();
        }
    }

    /// <summary>Read a string property, returning null when absent or not a string.</summary>
    public static string? GetString(ManagementBaseObject obj, string property)
    {
        try
        {
            var value = obj[property];
            return value switch
            {
                null => null,
                string s => string.IsNullOrWhiteSpace(s) ? null : s.Trim(),
                _ => value.ToString()
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Read an integer property, returning null when absent or non-numeric.</summary>
    public static int? GetInt32(ManagementBaseObject obj, string property)
    {
        try
        {
            var value = obj[property];
            return value switch
            {
                null => null,
                ushort us => us,
                uint ui => (int)ui,
                int i => i,
                short s => s,
                long l => (int)l,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Read a 64-bit property, returning null when absent or non-numeric.</summary>
    public static long? GetInt64(ManagementBaseObject obj, string property)
    {
        try
        {
            var value = obj[property];
            return value switch
            {
                null => null,
                ulong ul => (long)ul,
                long l => l,
                uint ui => ui,
                int i => i,
                ushort us => us,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Read a boolean property, returning null when absent or non-boolean.</summary>
    public static bool? GetBool(ManagementBaseObject obj, string property)
    {
        try
        {
            var value = obj[property];
            return value switch
            {
                null => null,
                bool b => b,
                ushort us => us != 0,
                uint ui => ui != 0,
                int i => i != 0,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Read a CIM datetime property (e.g. 20260819012345.000000+000) as UTC DateTime.</summary>
    public static DateTime? GetDateTime(ManagementBaseObject obj, string property)
    {
        try
        {
            var value = obj[property];
            if (value is null)
                return null;

            if (value is DateTime dt)
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);

            var text = value.ToString();
            if (string.IsNullOrWhiteSpace(text))
                return null;

            if (ManagementDateTimeConverter.ToDateTime(text) is DateTime parsed)
                return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);

            return null;
        }
        catch
        {
            return null;
        }
    }
}