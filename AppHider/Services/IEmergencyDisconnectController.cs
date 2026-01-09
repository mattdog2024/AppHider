using AppHider.Models;

namespace AppHider.Services;

public interface IEmergencyDisconnectController
{
    /// <summary>
    /// Gets or sets safe mode flag for testing without affecting real connections or network
    /// </summary>
    bool IsSafeMode { get; set; }
    
    Task<EmergencyDisconnectResult> ExecuteEmergencyDisconnectAsync();
    Task<EmergencyDisconnectResult> ExecuteRemoteDesktopDisconnectOnlyAsync();
    Task<bool> RegisterEmergencyHotkeyAsync(HotkeyConfig hotkey);
    Task<bool> UnregisterEmergencyHotkeyAsync();
    void SetHotkeyManager(IHotkeyManager hotkeyManager);
    event EventHandler<EmergencyDisconnectEventArgs>? EmergencyDisconnectTriggered;
    event EventHandler<EmergencyDisconnectEventArgs>? EmergencyDisconnectCompleted;
}