using AppHider.Models;

namespace AppHider.Services;

public interface IPrivacyModeController
{
    bool IsPrivacyModeActive { get; }
    bool IsSafeMode { get; }
    bool IsRestoredFromPreviousSession { get; }
    Task ActivatePrivacyModeAsync();
    Task DeactivatePrivacyModeAsync();
    Task TogglePrivacyModeAsync();
    Task RestoreStateOnStartupAsync();
    event EventHandler<PrivacyModeChangedEventArgs>? PrivacyModeChanged;
}
