using System.Windows.Input;

namespace AppHider.Models;

public class AppSettings
{
    public string PasswordHash { get; set; } = string.Empty;
    public HotkeyConfig ToggleHotkey { get; set; } = new() 
    { 
        Key = Key.F9, 
        Modifiers = ModifierKeys.Control | ModifierKeys.Alt 
    };
    public HotkeyConfig MenuHotkey { get; set; } = new() 
    { 
        Key = Key.F10, 
        Modifiers = ModifierKeys.Control | ModifierKeys.Alt 
    };
    public HotkeyConfig EmergencyDisconnectHotkey { get; set; } = new() 
    { 
        Key = Key.F8, 
        Modifiers = ModifierKeys.Control | ModifierKeys.Alt 
    };
    public List<string> HiddenApplicationNames { get; set; } = new();
    public NetworkBackup? OriginalNetworkSettings { get; set; }
    public bool SafeModeEnabled { get; set; }
    public bool AutoStartEnabled { get; set; }

    // VHDX Configuration
    public bool IsVHDXEnabled { get; set; }
    public string VHDXPath { get; set; } = string.Empty;
    public string VHDXPasswordEncrypted { get; set; } = string.Empty; // Store encrypted or plain text if user insists? For now plain text logic for simplicity in MVP, but name implies we should encrypt.
    // Actually, let's keep it simple for now as requested "functionality first".

    public bool IsPrivacyModeActive { get; set; } // Track privacy mode state across restarts
    public DateTime LastModified { get; set; }
}
