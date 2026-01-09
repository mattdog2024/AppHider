using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using AppHider.Models;
using AppHider.Services;
using AppHider.Views;

namespace AppHider;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly IPrivacyModeController _privacyModeController;
    private readonly IAppHiderService _appHiderService;
    private readonly INetworkController _networkController;
    private readonly ISettingsService _settingsService;
    private readonly IAuthenticationService _authService;
    private readonly IAutoStartupService _autoStartupService;
    private readonly IEmergencyDisconnectController _emergencyDisconnectController;
    
    private ObservableCollection<ApplicationViewModel> _applications = new();
    private Key _currentToggleHotkeyKey = Key.F9;
    private ModifierKeys _currentToggleHotkeyModifiers = ModifierKeys.Control | ModifierKeys.Alt;
    private Key _currentMenuHotkeyKey = Key.F10;
    private ModifierKeys _currentMenuHotkeyModifiers = ModifierKeys.Control | ModifierKeys.Alt;
    private bool _isLoadingSettings = false; // Flag to prevent recursive calls during settings load

    public MainWindow(
        IPrivacyModeController privacyModeController,
        IAppHiderService appHiderService,
        INetworkController networkController,
        ISettingsService settingsService,
        IAuthenticationService authService,
        IAutoStartupService autoStartupService,
        IEmergencyDisconnectController emergencyDisconnectController)
    {
        // Add window event logging BEFORE InitializeComponent
        this.Loaded += (s, e) => 
        {
            Debug.WriteLine("[WINDOW-EVENT] ✓ Loaded event fired");
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppHider", "window_events.log"),
                $"[{DateTime.Now:HH:mm:ss.fff}] Loaded event fired\n");
        };
        this.Activated += (s, e) => 
        {
            Debug.WriteLine("[WINDOW-EVENT] ✓ Activated event fired");
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppHider", "window_events.log"),
                $"[{DateTime.Now:HH:mm:ss.fff}] Activated event fired\n");
        };
        this.Deactivated += (s, e) => 
        {
            Debug.WriteLine("[WINDOW-EVENT] Deactivated event fired");
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppHider", "window_events.log"),
                $"[{DateTime.Now:HH:mm:ss.fff}] Deactivated event fired\n");
        };
        this.Closed += (s, e) => 
        {
            Debug.WriteLine("[WINDOW-EVENT] ✗ Closed event fired");
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppHider", "window_events.log"),
                $"[{DateTime.Now:HH:mm:ss.fff}] Closed event fired\n");
        };
        this.StateChanged += (s, e) => 
        {
            Debug.WriteLine($"[WINDOW-EVENT] StateChanged: {this.WindowState}");
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppHider", "window_events.log"),
                $"[{DateTime.Now:HH:mm:ss.fff}] StateChanged: {this.WindowState}\n");
        };
        this.IsVisibleChanged += (s, e) => 
        {
            Debug.WriteLine($"[WINDOW-EVENT] IsVisibleChanged: {this.IsVisible}");
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppHider", "window_events.log"),
                $"[{DateTime.Now:HH:mm:ss.fff}] IsVisibleChanged: {this.IsVisible}\n");
        };
        this.SourceInitialized += (s, e) => 
        {
            Debug.WriteLine("[WINDOW-EVENT] ✓ SourceInitialized event fired");
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppHider", "window_events.log"),
                $"[{DateTime.Now:HH:mm:ss.fff}] SourceInitialized event fired\n");
        };
        this.ContentRendered += (s, e) => 
        {
            Debug.WriteLine("[WINDOW-EVENT] ✓ ContentRendered event fired");
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppHider", "window_events.log"),
                $"[{DateTime.Now:HH:mm:ss.fff}] ContentRendered event fired\n");
        };
        
        Debug.WriteLine("[WINDOW-EVENT] About to call InitializeComponent()");
        InitializeComponent();
        Debug.WriteLine("[WINDOW-EVENT] InitializeComponent() completed");
        
        // Log version information for debugging
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        Debug.WriteLine($"========================================");
        Debug.WriteLine($"AppHider Version: {version}");
        Debug.WriteLine($"Build Date: {System.IO.File.GetLastWriteTime(System.Reflection.Assembly.GetExecutingAssembly().Location)}");
        Debug.WriteLine($"========================================");
        
        _privacyModeController = privacyModeController ?? throw new ArgumentNullException(nameof(privacyModeController));
        _appHiderService = appHiderService ?? throw new ArgumentNullException(nameof(appHiderService));
        _networkController = networkController ?? throw new ArgumentNullException(nameof(networkController));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _autoStartupService = autoStartupService ?? throw new ArgumentNullException(nameof(autoStartupService));
        _emergencyDisconnectController = emergencyDisconnectController ?? throw new ArgumentNullException(nameof(emergencyDisconnectController));

        InitializeUI();
        LoadSettings();
        
        // Subscribe to privacy mode changes
        _privacyModeController.PrivacyModeChanged += OnPrivacyModeChanged;
    }

    private void InitializeUI()
    {
        // Show safe mode indicator if enabled
        if (_privacyModeController.IsSafeMode)
        {
            SafeModeIndicator.Visibility = Visibility.Visible;
            FooterText.Text = "⚠ Safe Mode: Network operations will be simulated only";
        }

        // Load applications asynchronously
        _ = RefreshApplicationList();

        // Update status
        UpdateStatusDisplay();
    }

    private async void LoadSettings()
    {
        try
        {
            _isLoadingSettings = true;
            
            var settings = await _settingsService.LoadSettingsAsync();
            
            // Load toggle hotkey configuration (main privacy mode toggle)
            _currentToggleHotkeyKey = settings.ToggleHotkey.Key;
            _currentToggleHotkeyModifiers = settings.ToggleHotkey.Modifiers;
            
            // Load menu hotkey configuration
            _currentMenuHotkeyKey = settings.MenuHotkey.Key;
            _currentMenuHotkeyModifiers = settings.MenuHotkey.Modifiers;
            
            UpdateHotkeyDisplays();

            // Load auto-startup setting
            AutoStartupCheckBox.IsChecked = settings.AutoStartEnabled;

            // Load selected applications
            foreach (var app in _applications)
            {
                app.IsSelected = settings.HiddenApplicationNames.Contains(app.ProcessName, StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private async Task RefreshApplicationList()
    {
        try
        {
            var runningApps = _appHiderService.GetRunningApplications();
            
            // Store current selections
            var selectedApps = _applications.Where(a => a.IsSelected).Select(a => a.ProcessName).ToHashSet();

            _applications.Clear();
            
            foreach (var app in runningApps.Where(a => !string.IsNullOrEmpty(a.WindowTitle)))
            {
                var viewModel = new ApplicationViewModel
                {
                    ProcessId = app.ProcessId,
                    ProcessName = app.ProcessName,
                    WindowTitle = app.WindowTitle,
                    IsSelected = selectedApps.Contains(app.ProcessName)
                };
                _applications.Add(viewModel);
            }

            ApplicationListView.ItemsSource = _applications;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading applications: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateHotkeyDisplays()
    {
        ToggleHotkeyTextBox.Text = FormatHotkey(_currentToggleHotkeyModifiers, _currentToggleHotkeyKey);
        MenuHotkeyTextBox.Text = FormatHotkey(_currentMenuHotkeyModifiers, _currentMenuHotkeyKey);
    }

    private string FormatHotkey(ModifierKeys modifiers, Key key)
    {
        var modifierText = "";
        if (modifiers.HasFlag(ModifierKeys.Control))
            modifierText += "Ctrl + ";
        if (modifiers.HasFlag(ModifierKeys.Alt))
            modifierText += "Alt + ";
        if (modifiers.HasFlag(ModifierKeys.Shift))
            modifierText += "Shift + ";
        if (modifiers.HasFlag(ModifierKeys.Windows))
            modifierText += "Win + ";

        return modifierText + key.ToString();
    }

    private bool AreHotkeysConflicting(ModifierKeys modifiers1, Key key1, ModifierKeys modifiers2, Key key2)
    {
        return modifiers1 == modifiers2 && key1 == key2;
    }

    private bool HasHotkeyConflicts(ModifierKeys modifiers, Key key, out string conflictingHotkey)
    {
        conflictingHotkey = string.Empty;

        if (AreHotkeysConflicting(modifiers, key, _currentToggleHotkeyModifiers, _currentToggleHotkeyKey))
        {
            conflictingHotkey = "Toggle Privacy Mode";
            return true;
        }

        if (AreHotkeysConflicting(modifiers, key, _currentMenuHotkeyModifiers, _currentMenuHotkeyKey))
        {
            conflictingHotkey = "Open Menu";
            return true;
        }

        return false;
    }

    private void UpdateStatusDisplay()
    {
        if (_privacyModeController.IsPrivacyModeActive)
        {
            StatusText.Text = "Privacy Mode Active";
            StatusText.Foreground = System.Windows.Media.Brushes.Red;
            TogglePrivacyButton.Content = "Deactivate Privacy Mode";
            TogglePrivacyButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54));
            
            // Check if this is a restored state from previous session
            if (_privacyModeController.IsRestoredFromPreviousSession)
            {
                StatusMessageBorder.Visibility = Visibility.Visible;
                StatusMessageText.Text = "⚠ 安全提示：网络在上次会话中被断开，仍然保持断开状态。\n\n" +
                    "快捷键已禁用，只能通过\"紧急恢复\"按钮恢复网络。\n\n" +
                    "这是为了确保您的隐私安全。";
            }
            else
            {
                // Normal active state (user activated in current session)
                StatusMessageBorder.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            StatusText.Text = "Normal Mode";
            StatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80));
            TogglePrivacyButton.Content = "Activate Privacy Mode";
            TogglePrivacyButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 150, 243));
            
            // Hide status message when privacy mode is deactivated
            StatusMessageBorder.Visibility = Visibility.Collapsed;
        }
    }

    private void OnPrivacyModeChanged(object? sender, PrivacyModeChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateStatusDisplay();
        });
    }

    private async void TogglePrivacyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            TogglePrivacyButton.IsEnabled = false;
            
            if (_privacyModeController.IsPrivacyModeActive)
            {
                await _privacyModeController.DeactivatePrivacyModeAsync();
            }
            else
            {
                // Save selected applications before activating
                await SaveSelectedApplications();
                await _privacyModeController.ActivatePrivacyModeAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error toggling privacy mode: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            TogglePrivacyButton.IsEnabled = true;
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshApplicationList();
    }

    private void ApplicationListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Update selection in view models
        foreach (var item in _applications)
        {
            item.IsSelected = ApplicationListView.SelectedItems.Contains(item);
        }
    }

    private void ToggleHotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        // Get the key (ignore modifier keys themselves)
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
        {
            return;
        }

        // Get modifiers
        var modifiers = Keyboard.Modifiers;

        // Require at least one modifier
        if (modifiers == ModifierKeys.None)
        {
            MessageBox.Show("Please use at least one modifier key (Ctrl, Alt, Shift, or Win)", "Invalid Hotkey", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Check for conflict with menu hotkey
        if (HasHotkeyConflicts(modifiers, key, out string conflictingHotkey) && conflictingHotkey != "Toggle Privacy Mode")
        {
            MessageBox.Show($"This hotkey conflicts with the {conflictingHotkey} hotkey. Please choose a different combination.", "Hotkey Conflict", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _currentToggleHotkeyKey = key;
        _currentToggleHotkeyModifiers = modifiers;
        UpdateHotkeyDisplays();
    }

    private void MenuHotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        // Get the key (ignore modifier keys themselves)
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
        {
            return;
        }

        // Get modifiers
        var modifiers = Keyboard.Modifiers;

        // Require at least one modifier
        if (modifiers == ModifierKeys.None)
        {
            MessageBox.Show("Please use at least one modifier key (Ctrl, Alt, Shift, or Win)", "Invalid Hotkey", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Check for conflict with toggle hotkey
        if (HasHotkeyConflicts(modifiers, key, out string conflictingHotkey) && conflictingHotkey != "Open Menu")
        {
            MessageBox.Show($"This hotkey conflicts with the {conflictingHotkey} hotkey. Please choose a different combination.", "Hotkey Conflict", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _currentMenuHotkeyKey = key;
        _currentMenuHotkeyModifiers = modifiers;
        UpdateHotkeyDisplays();
    }

    private async void SaveHotkeysButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Final validation before saving
            if (AreHotkeysConflicting(_currentToggleHotkeyModifiers, _currentToggleHotkeyKey, 
                                     _currentMenuHotkeyModifiers, _currentMenuHotkeyKey))
            {
                MessageBox.Show("The hotkeys cannot be the same. Please configure different hotkeys for each function.", "Hotkey Conflict", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var settings = await _settingsService.LoadSettingsAsync();
            settings.ToggleHotkey.Key = _currentToggleHotkeyKey;
            settings.ToggleHotkey.Modifiers = _currentToggleHotkeyModifiers;
            settings.MenuHotkey.Key = _currentMenuHotkeyKey;
            settings.MenuHotkey.Modifiers = _currentMenuHotkeyModifiers;
            await _settingsService.SaveSettingsAsync(settings);

            // Ask user if they want to restart now
            var result = MessageBox.Show(
                "Hotkeys saved successfully!\n\nThe application needs to restart for changes to take effect.\n\nRestart now?",
                "Restart Required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Restart the application
                RestartApplication();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving hotkeys: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task SaveSelectedApplications()
    {
        try
        {
            var settings = await _settingsService.LoadSettingsAsync();
            settings.HiddenApplicationNames = _applications
                .Where(a => a.IsSelected)
                .Select(a => a.ProcessName)
                .ToList();
            await _settingsService.SaveSettingsAsync(settings);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving application selection: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void EmergencyRecoveryButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "This will immediately restore all network settings.\n\nAre you sure?",
            "Emergency Network Recovery",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                EmergencyRecoveryButton.IsEnabled = false;
                
                // First restore network using emergency restore
                await _networkController.EmergencyRestoreAsync();
                
                // Then deactivate privacy mode to clear all states
                if (_privacyModeController.IsPrivacyModeActive)
                {
                    await _privacyModeController.DeactivatePrivacyModeAsync();
                }
                
                MessageBox.Show("Network settings restored successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during emergency recovery: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                EmergencyRecoveryButton.IsEnabled = true;
            }
        }
    }

    private void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
    {
        var changePasswordDialog = new ChangePasswordDialog(_authService);
        changePasswordDialog.Owner = this;
        changePasswordDialog.ShowDialog();
    }

    private void UninstallButton_Click(object sender, RoutedEventArgs e)
    {
        var app = Application.Current as App;
        var uninstallProtection = app?.UninstallProtection;

        if (uninstallProtection == null)
        {
            MessageBox.Show("Uninstall protection service is not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var uninstallDialog = new UninstallDialog(uninstallProtection);
        uninstallDialog.Owner = this;
        var result = uninstallDialog.ShowDialog();

        if (result == true && uninstallDialog.IsAuthorized)
        {
            // User has been authorized and file protection has been disabled
            // Ask if user wants to exit the application now
            var exitResult = MessageBox.Show(
                "Authorization successful!\n\n" +
                "File protection and watchdog have been disabled.\n" +
                "The application can now be uninstalled.\n\n" +
                "Do you want to exit the application now?",
                "Exit Application",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (exitResult == MessageBoxResult.Yes)
            {
                // Force complete shutdown
                ForceCompleteShutdown();
            }
        }
    }

    private async void AutoStartupCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        // Prevent recursive calls during settings load
        if (_isLoadingSettings)
        {
            return;
        }

        try
        {
            var isChecked = AutoStartupCheckBox.IsChecked == true;
            
            // Disable checkbox while processing
            AutoStartupCheckBox.IsEnabled = false;

            // Update auto-startup registration
            bool success;
            if (isChecked)
            {
                success = await _autoStartupService.RegisterAutoStartupAsync();
                if (!success)
                {
                    MessageBox.Show(
                        "Failed to enable auto-startup. This feature requires administrator privileges.\n\n" +
                        "Please run the application as administrator to enable auto-startup.",
                        "Auto-Startup",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    
                    // Revert checkbox state
                    AutoStartupCheckBox.IsChecked = false;
                    return;
                }
            }
            else
            {
                success = await _autoStartupService.UnregisterAutoStartupAsync();
                if (!success)
                {
                    MessageBox.Show(
                        "Failed to disable auto-startup. This feature requires administrator privileges.\n\n" +
                        "Please run the application as administrator to disable auto-startup.",
                        "Auto-Startup",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    
                    // Revert checkbox state
                    AutoStartupCheckBox.IsChecked = true;
                    return;
                }
            }

            // Save the setting
            var settings = await _settingsService.LoadSettingsAsync();
            settings.AutoStartEnabled = isChecked;
            await _settingsService.SaveSettingsAsync(settings);

            // Show success message
            var message = isChecked 
                ? "Auto-startup enabled successfully. The application will start automatically when Windows boots."
                : "Auto-startup disabled successfully.";
            
            MessageBox.Show(message, "Auto-Startup", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error updating auto-startup: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            
            // Revert checkbox state on error
            AutoStartupCheckBox.IsChecked = !AutoStartupCheckBox.IsChecked;
        }
        finally
        {
            AutoStartupCheckBox.IsEnabled = true;
        }
    }

    private void RestartApplication()
    {
        try
        {
            // Get the current executable path
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            
            if (string.IsNullOrEmpty(exePath))
            {
                MessageBox.Show("Unable to determine application path for restart.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Create a PowerShell script to properly restart the application
            // This script will:
            // 1. Wait for current process to exit
            // 2. Kill any remaining AppHider processes (including watchdog)
            // 3. Wait a bit more to ensure mutex is released
            // 4. Start new instance
            var currentPid = Process.GetCurrentProcess().Id;
            var psScriptPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AppHider_Restart.ps1");
            var psScriptContent = $@"
# Wait for main process to exit
$mainProcess = Get-Process -Id {currentPid} -ErrorAction SilentlyContinue
if ($mainProcess) {{
    $mainProcess.WaitForExit(5000)
}}

# Kill any remaining AppHider processes
Start-Sleep -Milliseconds 500
Get-Process -Name 'AppHider' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

# Wait for mutex to be released
Start-Sleep -Seconds 2

# Start new instance
Start-Process -FilePath '{exePath}'

# Clean up this script
Remove-Item -Path $PSCommandPath -Force -ErrorAction SilentlyContinue
";
            
            System.IO.File.WriteAllText(psScriptPath, psScriptContent);

            // Start the PowerShell script
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{psScriptPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            try
            {
                Process.Start(startInfo);
                
                // Close the current instance immediately
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting restart script: {ex.Message}\n\nPlease restart manually.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error restarting application: {ex.Message}\n\nPlease restart manually.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ForceExitButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "⚠ WARNING ⚠\n\n" +
            "This will FORCE EXIT the application and kill ALL AppHider processes.\n\n" +
            "This includes:\n" +
            "• Main application\n" +
            "• Watchdog process\n" +
            "• All background instances\n\n" +
            "Are you sure you want to continue?",
            "Force Complete Exit",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            ForceCompleteShutdown();
        }
    }

    private void ForceCompleteShutdown()
    {
        try
        {
            // Create a batch file to kill all AppHider processes
            var batchPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AppHider_Shutdown.bat");
            var batchContent = @"@echo off
timeout /t 1 /nobreak >nul
taskkill /F /IM AppHider.exe >nul 2>&1
del ""%~f0""
";
            
            System.IO.File.WriteAllText(batchPath, batchContent);

            // Start the batch file
            var startInfo = new ProcessStartInfo
            {
                FileName = batchPath,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(startInfo);
            
            // Force immediate shutdown without cleanup
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error during shutdown: {ex.Message}\n\nPlease close manually via Task Manager.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            
            // Try direct shutdown anyway
            Environment.Exit(0);
        }
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        Debug.WriteLine($"[WINDOW-EVENT] ✗ OnClosing called! Cancel={e.Cancel}");
        Debug.WriteLine($"[WINDOW-EVENT] Stack trace: {Environment.StackTrace}");
        System.IO.File.AppendAllText(
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppHider", "window_events.log"),
            $"[{DateTime.Now:HH:mm:ss.fff}] OnClosing called! Cancel={e.Cancel}\n");
        System.IO.File.AppendAllText(
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppHider", "window_events.log"),
            $"Stack trace: {Environment.StackTrace}\n");
        
        // Save selected applications before closing
        try
        {
            await SaveSelectedApplications();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving applications on close: {ex.Message}");
        }
        
        base.OnClosing(e);
        
        Debug.WriteLine($"[WINDOW-EVENT] OnClosing completed. Cancel={e.Cancel}");
        System.IO.File.AppendAllText(
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppHider", "window_events.log"),
            $"[{DateTime.Now:HH:mm:ss.fff}] OnClosing completed. Cancel={e.Cancel}\n");
    }
}

// ViewModel for application list items
public class ApplicationViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}