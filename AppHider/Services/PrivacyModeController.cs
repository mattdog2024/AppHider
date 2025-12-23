using AppHider.Models;
using FL = AppHider.Utils.FileLogger;

namespace AppHider.Services;

public class PrivacyModeController : IPrivacyModeController
{
    private readonly IAppHiderService _appHiderService;
    private readonly INetworkController _networkController;
    private readonly ISettingsService _settingsService;
    private readonly object _lockObject = new();
    
    private PrivacyModeState _currentState = PrivacyModeState.Normal;
    private List<int> _hiddenProcessIds = new();
    private bool _isRestoredFromPreviousSession = false;

    public bool IsPrivacyModeActive => _currentState == PrivacyModeState.Active;
    
    public bool IsSafeMode { get; private set; }
    
    public bool IsRestoredFromPreviousSession => _isRestoredFromPreviousSession;

    public event EventHandler<PrivacyModeChangedEventArgs>? PrivacyModeChanged;

    public PrivacyModeController(
        IAppHiderService appHiderService,
        INetworkController networkController,
        ISettingsService settingsService)
    {
        _appHiderService = appHiderService ?? throw new ArgumentNullException(nameof(appHiderService));
        _networkController = networkController ?? throw new ArgumentNullException(nameof(networkController));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        
        // Initialize safe mode from application startup detection
        IsSafeMode = App.IsSafeModeEnabled;
    }

    /// <summary>
    /// Check if privacy mode was active before shutdown and restore network disabled state
    /// SECURITY: Do NOT auto-restore network - user must manually restore for safety
    /// IMPORTANT: After restart, only emergency recovery can restore network, not toggle hotkey
    /// </summary>
    public async Task RestoreStateOnStartupAsync()
    {
        try
        {
            var settings = await _settingsService.LoadSettingsAsync();
            
            // If privacy mode was active when the app was closed/crashed
            if (settings.IsPrivacyModeActive)
            {
                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine("Privacy mode was active before shutdown");
                System.Diagnostics.Debug.WriteLine("SECURITY: Network will remain disabled until user manually restores");
                System.Diagnostics.Debug.WriteLine("SECURITY: Toggle hotkey is DISABLED - only emergency recovery can unlock");
                System.Diagnostics.Debug.WriteLine("========================================");
                
                // Restore the privacy mode state (but don't restore network)
                lock (_lockObject)
                {
                    _currentState = PrivacyModeState.Active;
                    _isRestoredFromPreviousSession = true;  // Mark as restored from previous session
                }
                
                // Clear the hidden application list (apps don't exist after restart)
                settings.HiddenApplicationNames.Clear();
                await _settingsService.SaveSettingsAsync(settings);
                
                System.Diagnostics.Debug.WriteLine("Hidden application list cleared (apps don't exist after restart)");
                
                // Notify UI that privacy mode is still active
                OnPrivacyModeChanged(true, PrivacyModeState.Active);
                
                System.Diagnostics.Debug.WriteLine("Privacy mode state restored. Network remains disabled for security.");
                System.Diagnostics.Debug.WriteLine("User must use Emergency Recovery to restore network.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in RestoreStateOnStartupAsync: {ex.Message}");
            // Don't throw - this is a recovery operation
        }
    }

    public async Task ActivatePrivacyModeAsync()
    {
        FL.Log("[PRIVACY] ========================================");
        FL.Log("[PRIVACY] ActivatePrivacyModeAsync called");
        System.Diagnostics.Debug.WriteLine("========================================");
        System.Diagnostics.Debug.WriteLine("ActivatePrivacyModeAsync called");
        
        lock (_lockObject)
        {
            FL.Log($"[PRIVACY] Current state: {_currentState}");
            System.Diagnostics.Debug.WriteLine($"Current state: {_currentState}");
            
            if (_currentState == PrivacyModeState.Active || _currentState == PrivacyModeState.Activating)
            {
                // Already active or activating
                FL.Log("[PRIVACY] Already active or activating, returning");
                System.Diagnostics.Debug.WriteLine("Already active or activating, returning");
                return;
            }

            _currentState = PrivacyModeState.Activating;
            _isRestoredFromPreviousSession = false;  // Clear the flag - this is a user-initiated activation
            FL.Log("[PRIVACY] State changed to: Activating");
            FL.Log("[PRIVACY] Cleared IsRestoredFromPreviousSession flag - user-initiated activation");
            System.Diagnostics.Debug.WriteLine("State changed to: Activating");
            System.Diagnostics.Debug.WriteLine("Cleared IsRestoredFromPreviousSession flag - user-initiated activation");
        }

        try
        {
            // Notify state change to Activating
            OnPrivacyModeChanged(false, PrivacyModeState.Activating);

            // Load settings to get the list of applications to hide
            FL.Log("[PRIVACY] Loading settings...");
            System.Diagnostics.Debug.WriteLine("Loading settings...");
            var settings = await _settingsService.LoadSettingsAsync();
            FL.Log($"[PRIVACY] Hidden applications in settings: [{string.Join(", ", settings.HiddenApplicationNames)}]");
            FL.Log($"[PRIVACY] Hidden applications count: {settings.HiddenApplicationNames.Count}");
            System.Diagnostics.Debug.WriteLine($"Hidden applications: {string.Join(", ", settings.HiddenApplicationNames)}");

            // Get running applications
            FL.Log("[PRIVACY] Getting running applications...");
            System.Diagnostics.Debug.WriteLine("Getting running applications...");
            var runningApps = _appHiderService.GetRunningApplications();
            FL.Log($"[PRIVACY] Found {runningApps.Count} running applications");
            System.Diagnostics.Debug.WriteLine($"Found {runningApps.Count} running applications");
            
            // Log all running applications for debugging
            foreach (var app in runningApps)
            {
                FL.Log($"[PRIVACY] Running app: ProcessName='{app.ProcessName}', WindowTitle='{app.WindowTitle}', PID={app.ProcessId}");
            }

            // Find processes that match the hidden application names
            FL.Log("[PRIVACY] Matching processes to hide...");
            var processesToHide = new List<int>();
            foreach (var app in runningApps)
            {
                bool isMatch = settings.HiddenApplicationNames.Contains(app.ProcessName, StringComparer.OrdinalIgnoreCase);
                FL.Log($"[PRIVACY] Checking '{app.ProcessName}' against hidden list: {(isMatch ? "MATCH" : "no match")}");
                if (isMatch)
                {
                    processesToHide.Add(app.ProcessId);
                    FL.Log($"[PRIVACY] Will hide: ProcessName='{app.ProcessName}', PID={app.ProcessId}");
                }
            }

            FL.Log($"[PRIVACY] Total processes to hide: {processesToHide.Count}");
            System.Diagnostics.Debug.WriteLine($"Processes to hide: {processesToHide.Count}");
            
            // Store the process IDs for later restoration
            _hiddenProcessIds = processesToHide;

            // Hide the applications
            if (processesToHide.Any())
            {
                FL.Log("[PRIVACY] Hiding applications...");
                System.Diagnostics.Debug.WriteLine("Hiding applications...");
                await _appHiderService.HideApplicationsAsync(processesToHide);
                FL.Log("[PRIVACY] Applications hidden successfully");
                System.Diagnostics.Debug.WriteLine("Applications hidden successfully");
            }
            else
            {
                FL.Log("[PRIVACY] No applications to hide (none are currently running or no match found)");
                System.Diagnostics.Debug.WriteLine("No applications to hide (none are currently running)");
            }

            // Disable network
            FL.Log("[PRIVACY] Disabling network...");
            System.Diagnostics.Debug.WriteLine("Disabling network...");
            await _networkController.DisableNetworkAsync();
            FL.Log("[PRIVACY] Network disabled successfully");
            System.Diagnostics.Debug.WriteLine("Network disabled successfully");

            // Update state to Active
            lock (_lockObject)
            {
                _currentState = PrivacyModeState.Active;
                FL.Log("[PRIVACY] State changed to: Active");
                System.Diagnostics.Debug.WriteLine("State changed to: Active");
            }

            // Save privacy mode state to settings
            FL.Log("[PRIVACY] Saving privacy mode state...");
            System.Diagnostics.Debug.WriteLine("Saving privacy mode state...");
            settings.IsPrivacyModeActive = true;
            await _settingsService.SaveSettingsAsync(settings);
            FL.Log("[PRIVACY] Privacy mode state saved");
            System.Diagnostics.Debug.WriteLine("Privacy mode state saved");

            // Notify state change to Active
            OnPrivacyModeChanged(true, PrivacyModeState.Active);
            
            FL.Log("[PRIVACY] ========================================");
            FL.Log("[PRIVACY] Privacy mode activated successfully");
            FL.Log("[PRIVACY] ========================================");
            System.Diagnostics.Debug.WriteLine("========================================");
            System.Diagnostics.Debug.WriteLine("Privacy mode activated successfully");
            System.Diagnostics.Debug.WriteLine("========================================");
        }
        catch (Exception ex)
        {
            FL.Log("[PRIVACY] ========================================");
            FL.Log($"[PRIVACY] ERROR in ActivatePrivacyModeAsync: {ex.Message}");
            FL.Log($"[PRIVACY] Stack trace: {ex.StackTrace}");
            FL.Log("[PRIVACY] ========================================");
            System.Diagnostics.Debug.WriteLine("========================================");
            System.Diagnostics.Debug.WriteLine($"ERROR in ActivatePrivacyModeAsync: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            System.Diagnostics.Debug.WriteLine("========================================");
            
            // Rollback to Normal state on error
            lock (_lockObject)
            {
                _currentState = PrivacyModeState.Normal;
            }

            OnPrivacyModeChanged(false, PrivacyModeState.Normal);
            
            throw new InvalidOperationException("Failed to activate privacy mode", ex);
        }
    }

    public async Task DeactivatePrivacyModeAsync()
    {
        lock (_lockObject)
        {
            if (_currentState == PrivacyModeState.Normal || _currentState == PrivacyModeState.Deactivating)
            {
                // Already normal or deactivating
                return;
            }

            _currentState = PrivacyModeState.Deactivating;
            _isRestoredFromPreviousSession = false;  // Clear the flag when deactivating
        }

        try
        {
            System.Diagnostics.Debug.WriteLine("DeactivatePrivacyModeAsync called");
            System.Diagnostics.Debug.WriteLine("Cleared IsRestoredFromPreviousSession flag");
            
            // Notify state change to Deactivating
            OnPrivacyModeChanged(false, PrivacyModeState.Deactivating);

            // Restore network first (more critical)
            await _networkController.RestoreNetworkAsync();

            // Show the hidden applications
            if (_hiddenProcessIds.Any())
            {
                await _appHiderService.ShowApplicationsAsync(_hiddenProcessIds);
                _hiddenProcessIds.Clear();
            }

            // Update state to Normal
            lock (_lockObject)
            {
                _currentState = PrivacyModeState.Normal;
            }

            // Save privacy mode state to settings
            var settings = await _settingsService.LoadSettingsAsync();
            settings.IsPrivacyModeActive = false;
            await _settingsService.SaveSettingsAsync(settings);

            // Notify state change to Normal
            OnPrivacyModeChanged(false, PrivacyModeState.Normal);
        }
        catch (Exception ex)
        {
            // Try to restore to a safe state
            lock (_lockObject)
            {
                _currentState = PrivacyModeState.Normal;
            }

            OnPrivacyModeChanged(false, PrivacyModeState.Normal);
            
            throw new InvalidOperationException("Failed to deactivate privacy mode", ex);
        }
    }

    public async Task TogglePrivacyModeAsync()
    {
        try
        {
            bool isCurrentlyActive;
            bool isRestored;
            
            lock (_lockObject)
            {
                isCurrentlyActive = _currentState == PrivacyModeState.Active;
                isRestored = _isRestoredFromPreviousSession;
            }

            FL.Log($"[PRIVACY] Toggle privacy mode called. Current state: {(isCurrentlyActive ? "Active" : "Normal")}");
            FL.Log($"[PRIVACY] IsRestoredFromPreviousSession: {isRestored}");
            System.Diagnostics.Debug.WriteLine($"Toggle privacy mode called. Current state: {(isCurrentlyActive ? "Active" : "Normal")}");
            System.Diagnostics.Debug.WriteLine($"IsRestoredFromPreviousSession: {isRestored}");

            // SECURITY: If this is a restored state from previous session, don't allow toggle
            if (isRestored && isCurrentlyActive)
            {
                FL.Log("[PRIVACY] ========================================");
                FL.Log("[PRIVACY] SECURITY: Toggle blocked - state was restored from previous session");
                FL.Log("[PRIVACY] User must use Emergency Recovery to restore network");
                FL.Log("[PRIVACY] ========================================");
                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine("SECURITY: Toggle blocked - state was restored from previous session");
                System.Diagnostics.Debug.WriteLine("User must use Emergency Recovery to restore network");
                System.Diagnostics.Debug.WriteLine("========================================");
                
                System.Windows.MessageBox.Show(
                    "检测到网络是在上次会话中断开的。\n\n" +
                    "为了安全，快捷键无法恢复网络。\n\n" +
                    "请使用\"紧急恢复\"按钮来恢复网络连接。",
                    "安全提示",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                
                return;
            }

            if (isCurrentlyActive)
            {
                FL.Log("[PRIVACY] Deactivating privacy mode...");
                System.Diagnostics.Debug.WriteLine("Deactivating privacy mode...");
                await DeactivatePrivacyModeAsync();
            }
            else
            {
                FL.Log("[PRIVACY] Activating privacy mode...");
                System.Diagnostics.Debug.WriteLine("Activating privacy mode...");
                await ActivatePrivacyModeAsync();
            }
            
            FL.Log("[PRIVACY] Toggle privacy mode completed successfully.");
            System.Diagnostics.Debug.WriteLine("Toggle privacy mode completed successfully.");
        }
        catch (Exception ex)
        {
            FL.Log($"[PRIVACY] ERROR in TogglePrivacyModeAsync: {ex.Message}");
            FL.Log($"[PRIVACY] Stack trace: {ex.StackTrace}");
            System.Diagnostics.Debug.WriteLine($"ERROR in TogglePrivacyModeAsync: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            
            // Reset state to Normal on error
            lock (_lockObject)
            {
                _currentState = PrivacyModeState.Normal;
            }
            
            // Show error to user
            System.Windows.MessageBox.Show(
                $"切换隐私模式时出错：{ex.Message}\n\n" +
                "如果网络无法连接，请点击\"紧急恢复\"按钮。",
                "错误",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void OnPrivacyModeChanged(bool isActive, PrivacyModeState state)
    {
        PrivacyModeChanged?.Invoke(this, new PrivacyModeChangedEventArgs
        {
            IsActive = isActive,
            State = state
        });
    }
}
