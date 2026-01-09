using System.Windows.Input;
using AppHider.Models;

namespace AppHider.Services;

public interface IHotkeyManager
{
    void RegisterHotkey(Key key, ModifierKeys modifiers, Action callback);
    void UnregisterHotkey(Key key, ModifierKeys modifiers);
    void RegisterLockScreenHook(Action callback);
    
    // Emergency disconnect hotkey management
    bool RegisterEmergencyDisconnectHotkey(HotkeyConfig hotkeyConfig, Action callback);
    bool UnregisterEmergencyDisconnectHotkey();
    
    // Hotkey validation
    bool IsHotkeyAvailable(Key key, ModifierKeys modifiers);
    bool ValidateHotkeyConfig(HotkeyConfig hotkeyConfig, out string? errorMessage);
    
    // Get currently registered hotkeys for conflict detection
    IReadOnlyDictionary<string, HotkeyConfig> GetRegisteredHotkeys();
    
    event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;
}
