using System.Windows.Input;
using AppHider.Models;

namespace AppHider.Services;

public interface IHotkeyManager
{
    void RegisterHotkey(Key key, ModifierKeys modifiers, Action callback);
    void UnregisterHotkey(Key key, ModifierKeys modifiers);
    void RegisterLockScreenHook(Action callback);
    event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;
}
