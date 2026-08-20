namespace PCDiag.Net.Tcp;

/// <summary>
/// Abstraction over reading the current TCP connection table so checks can be tested
/// with fakes. Implementations must never throw.
/// </summary>
public interface ITcpConnectionSource
{
    IReadOnlyList<TcpConnectionRecord> GetConnections();
}