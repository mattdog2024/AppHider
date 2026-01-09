namespace AppHider.Models;

public class SessionInfo
{
    public int SessionId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ClientAddress { get; set; } = string.Empty;
    public WTSConnectState State { get; set; }
    public DateTime ConnectedTime { get; set; }
    public string WinStationName { get; set; } = string.Empty;
}