namespace PCDiag.Infrastructure;

/// <summary>Shared formatting helpers for evidence values.</summary>
public static class Format
{
    /// <summary>Format a byte count as a human-readable size ("15.7 GB"); null renders as "unavailable".</summary>
    public static string Bytes(long? bytes, int decimals = 1)
    {
        if (bytes is not long b || b < 0)
            return "unavailable";

        double d = b;
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        while (d >= 1024 && i < units.Length - 1)
        {
            d /= 1024;
            i++;
        }

        return $"{d.ToString("F" + decimals)} {units[i]}";
    }

    /// <summary>Format a fraction (0..1) as a percentage ("38%").</summary>
    public static string Percent(double fraction, int decimals = 0)
        => $"{(fraction * 100).ToString("F" + decimals)}%";

    /// <summary>Format a seconds value as milliseconds, when sensible, or seconds otherwise.</summary>
    public static string Latency(double? seconds)
    {
        if (seconds is not double s || s < 0)
            return "unavailable";
        if (s < 1)
            return $"{(s * 1000):F1} ms";
        return $"{s:F2} s";
    }
}