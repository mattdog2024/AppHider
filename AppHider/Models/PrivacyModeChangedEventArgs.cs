namespace AppHider.Models;

public class PrivacyModeChangedEventArgs : EventArgs
{
    public bool IsActive { get; set; }
    public PrivacyModeState State { get; set; }
}
