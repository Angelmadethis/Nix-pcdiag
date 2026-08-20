namespace PCDiag.Net.Tcp;

/// <summary>Receive-window auto-tuning state as reported by <c>MSFT_NetTCPSetting</c>.</summary>
public enum TcpAutotuningLevel
{
    Unknown = 0,
    Normal = 1,
    Experimental = 2,
    Restricted = 3,
    HighlyRestricted = 4,
    Disabled = 5
}

/// <summary>
/// Read-only view of TCP configuration: registry values under
/// <c>Tcpip\Parameters</c> (absent = Windows default) and the receive-window
/// auto-tuning / dynamic port range from <c>MSFT_NetTCPSetting</c>. Never written.
/// </summary>
public sealed record TcpConfiguration
{
    /// <summary>TcpTimedWaitDelay (seconds) or null when unset (default).</summary>
    public int? TcpTimedWaitDelay { get; init; }

    /// <summary>TcpNumConnections or null when unset (default).</summary>
    public int? TcpNumConnections { get; init; }

    /// <summary>TcpMaxDataRetransmissions or null when unset (default).</summary>
    public int? TcpMaxDataRetransmissions { get; init; }

    /// <summary>MaxUserPort or null when unset (default).</summary>
    public int? MaxUserPort { get; init; }

    /// <summary>GlobalMaxTcpWindowSize or null when unset (default).</summary>
    public int? GlobalMaxTcpWindowSize { get; init; }

    /// <summary>TcpWindowSize (any interface) or null when unset on every interface.</summary>
    public int? TcpWindowSize { get; init; }

    /// <summary>Start of the dynamic (ephemeral) port range, or null when unknown.</summary>
    public int? DynamicPortStart { get; init; }

    /// <summary>Number of dynamic ports, or null when unknown.</summary>
    public int? DynamicPortCount { get; init; }

    /// <summary>Effective receive-window auto-tuning level.</summary>
    public TcpAutotuningLevel AutotuningLevel { get; init; }

    /// <summary>Group-policy auto-tuning override, when one is in effect.</summary>
    public TcpAutotuningLevel AutotuningGroupPolicy { get; init; }
}

/// <summary>Abstraction over reading TCP configuration so checks can be tested with fakes.</summary>
public interface ITcpConfigSource
{
    TcpConfiguration GetConfig();
}