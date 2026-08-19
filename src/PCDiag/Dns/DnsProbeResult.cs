namespace PCDiag.Dns;

/// <summary>The outcome of a single DNS probe.</summary>
public enum DnsProbeOutcome
{
    /// <summary>A well-formed response with a matching ID and RCODE 0 was received.</summary>
    Success,

    /// <summary>A response was received but was malformed, had a mismatched ID, or a non-zero RCODE.</summary>
    Failed,

    /// <summary>No response was received within the probe timeout.</summary>
    TimedOut
}

/// <summary>The result of a single DNS probe against one resolver.</summary>
public sealed record DnsProbeResult
{
    public DnsProbeOutcome Outcome { get; init; }

    /// <summary>Round-trip time in milliseconds for received responses (Success/Failed); 0 for timeouts.</summary>
    public long RoundTripMs { get; init; }

    /// <summary>The response RCODE for received responses; -1 for timeouts.</summary>
    public int RCode { get; init; }

    /// <summary>Human-readable reason for failures, when available.</summary>
    public string? Error { get; init; }
}