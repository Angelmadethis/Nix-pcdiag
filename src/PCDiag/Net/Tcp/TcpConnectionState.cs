namespace PCDiag.Net.Tcp;

/// <summary>
/// TCP connection states as reported by <c>MSFT_NetTCPConnection</c>. The numeric
/// values follow the MIB/TCP state scheme used by Windows, plus the provider-specific
/// <see cref="Bound"/> state. Values outside the known set map to <see cref="Unknown"/>.
/// </summary>
public enum TcpConnectionState
{
    Unknown = 0,
    Closed = 1,
    Listen = 2,
    SynSent = 3,
    SynReceived = 4,
    Established = 5,
    FinWait1 = 6,
    FinWait2 = 7,
    CloseWait = 8,
    Closing = 9,
    LastAck = 10,
    TimeWait = 11,
    DeleteTcb = 12,
    Bound = 100
}

public static class TcpConnectionStateExtensions
{
    /// <summary>Map a raw MIB/TCP state value to the enum. Pure and unit-testable.</summary>
    public static TcpConnectionState FromMibState(int state)
        => Enum.IsDefined(typeof(TcpConnectionState), state)
            ? (TcpConnectionState)state
            : TcpConnectionState.Unknown;
}