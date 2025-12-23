using AppHider.Models;

namespace AppHider.Services;

public interface INetworkController
{
    bool IsSafeMode { get; set; }
    Task<NetworkState> GetCurrentStateAsync();
    Task DisableNetworkAsync();
    Task RestoreNetworkAsync();
    Task EmergencyRestoreAsync();
    Task SaveOriginalSettingsAsync();
}
