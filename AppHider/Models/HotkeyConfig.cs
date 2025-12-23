using System.Windows.Input;

namespace AppHider.Models;

public class HotkeyConfig
{
    public Key Key { get; set; } = Key.F9;
    public ModifierKeys Modifiers { get; set; } = ModifierKeys.Control | ModifierKeys.Alt;
}
