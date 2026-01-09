namespace AppHider.Models;

public class RDPConnection
{
    public int Id { get; set; }
    public RDPConnectionType Type { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ClientAddress { get; set; } = string.Empty;
    public DateTime ConnectedTime { get; set; }
    public WTSConnectState State { get; set; }
    public int ProcessId { get; set; }  // For client connections
    public int SessionId { get; set; }  // For session connections
}

public enum RDPConnectionType
{
    IncomingSession,    // 作为服务端的连接
    OutgoingClient      // 作为客户端的连接
}

public enum WTSConnectState
{
    Active,
    Connected,
    ConnectQuery,
    Shadow,
    Disconnected,
    Idle,
    Listen,
    Reset,
    Down,
    Init
}