using System.Windows.Input;

namespace AppHider.Models;

public class HotkeyPressedEventArgs : EventArgs
{
    public Key Key { get; set; }
    public ModifierKeys Modifiers { get; set; }
}
