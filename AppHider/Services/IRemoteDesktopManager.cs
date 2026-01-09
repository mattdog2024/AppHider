using AppHider.Models;

namespace AppHider.Services;

public interface IRemoteDesktopManager
{
    bool IsSafeMode { get; set; }
    Task<List<RDPConnection>> GetActiveConnectionsAsync();
    Task<bool> TerminateAllConnectionsAsync();
    Task<bool> TerminateSessionConnectionsAsync();
    Task<bool> TerminateClientConnectionsAsync();
    event EventHandler<RDPConnectionEventArgs>? ConnectionDetected;
    event EventHandler<RDPConnectionEventArgs>? ConnectionTerminated;
}