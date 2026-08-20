namespace PCDiag.Net.Tcp;

/// <summary>A single TCP connection/endpoint as reported by the OS.</summary>
public sealed record TcpConnectionRecord(
    TcpConnectionState State,
    string LocalAddress,
    int LocalPort,
    string RemoteAddress,
    int RemotePort,
    int OwningProcess);