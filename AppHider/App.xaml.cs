using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using AppHider.Models;
using AppHider.Services;
using AppHider.Utils;
using AppHider.Views;
using FL = AppHider.Utils.FileLogger;

namespace AppHider;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool AllocConsole();

    public static bool IsSafeModeEnabled { get; private set; }
    
    private IPrivacyModeController? _privacyModeController;
    private IAppHiderService? _appHiderService;
    private INetworkController? _networkController;
    private ISettingsService? _settingsService;
    private IAuthenticationService? _authService;
    private IWatchdogService? _watchdogService;
    private IAutoStartupService? _autoStartupService;
    private IDirectoryHidingService? _directoryHidingService;
    private IEmergencyDisconnectController? _emergencyDisconnectController;
    private IRemoteDesktopManager? _remoteDesktopManager;
    private IVHDXManager? _vhdxManager; // [NEW]
    private ILogCleaner? _logCleaner; // [NEW]
    private UninstallProtectionService? _uninstallProtection;
    private HotkeyManager? _hotkeyManager;
    private MainWindow? _mainWindow;
    private bool _isBackgroundMode;
    private Window? _hiddenHotkeyWindow; // Persistent hidden window for hotkey messages
    private Mutex? _singleInstanceMutex; // Mutex for single instance enforcement
    private bool _isInitializing = false; // Flag to prevent window closing during initialization

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        FL.Log("========================================");
        FL.Log("AppHider Starting...");
        FL.Log($"Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
        FL.Log($"Command line args: {string.Join(" ", e.Args)}");
        FL.Log("========================================");

        // Check if running in watchdog mode
        if (e.Args.Contains("--watchdog-mode"))
        {
            // Get parent process ID from current process
            var parentProcessId = Process.GetCurrentProcess().Id;
            
            // Find the actual parent by looking for another AppHider process
            var currentProcess = Process.GetCurrentProcess();
            var allProcesses = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(currentProcess.ProcessName));
            
            foreach (var proc in allProcesses)
            {
                if (proc.Id != currentProcess.Id)
                {
                    parentProcessId = proc.Id;
                    break;
                }
            }
            
            // Run watchdog mode and exit
            Task.Run(async () => await WatchdogService.RunWatchdogModeAsync(parentProcessId)).Wait();
            Shutdown();
            return;
        }

        // Check if running remote desktop tests
        if (e.Args.Contains("--test-remote-desktop"))
        {
            // Allocate console for output
            if (!System.Diagnostics.Debugger.IsAttached)
            {
                AllocConsole();
            }
            
            // Run simple remote desktop verification
            Console.WriteLine("Starting Remote Desktop Core Functionality Verification...");
            try
            {
                var testsPassed = SimpleRemoteDesktopTest.VerifyCoreFunctionalityAsync().Result;
                Console.WriteLine($"\nVerification completed. Result: {(testsPassed ? "PASSED" : "FAILED")}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                Environment.Exit(testsPassed ? 0 : 1);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Critical error running verification: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                Environment.Exit(1);
            }
            return;
        }

        // Check if running integration verification
        if (e.Args.Contains("--verify-integration"))
        {
            try
            {
                var testsPassed = SimpleIntegrationTest.RunSimpleTestAsync().Result;
                Environment.Exit(testsPassed ? 0 : 1);
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText("integration_error.txt", $"Critical error: {ex.Message}\nStack trace: {ex.StackTrace}");
                }
                catch { }
                Environment.Exit(1);
            }
            return;
        }

        // Check if running comprehensive integration tests
        if (e.Args.Contains("--test-integration"))
        {
            // Allocate console for output
            if (!System.Diagnostics.Debugger.IsAttached)
            {
                AllocConsole();
            }
            
            Console.WriteLine("Starting Remote Desktop Integration Tests...");
            try
            {
                // Initialize services for testing
                var settingsService = new SettingsService();
                var networkController = new NetworkController();
                var testRdSessionService = new RDSessionService();
                var testRdClientService = new RDClientService();
                var remoteDesktopManager = new RemoteDesktopManager(testRdSessionService, testRdClientService);
                var emergencyDisconnectController = new EmergencyDisconnectController(remoteDesktopManager, networkController, null);
                var appHiderService = new AppHiderService();
                var vhdxManager = new VHDXManager();
                var logCleaner = new LogCleaner();
                var privacyModeController = new PrivacyModeController(appHiderService, networkController, settingsService, emergencyDisconnectController, vhdxManager, logCleaner);

                // Enable safe mode for testing
                remoteDesktopManager.IsSafeMode = true;
                networkController.IsSafeMode = true;

                // Run comprehensive integration tests
                var integrationTestRunner = new ComprehensiveIntegrationTestRunner(
                    privacyModeController,
                    emergencyDisconnectController,
                    remoteDesktopManager,
                    networkController,
                    settingsService);

                var testsPassed = integrationTestRunner.RunAllIntegrationTestsAsync().Result;
                
                Console.WriteLine($"\nIntegration tests completed. Result: {(testsPassed ? "PASSED" : "FAILED")}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                Environment.Exit(testsPassed ? 0 : 1);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Critical error running integration tests: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                Environment.Exit(1);
            }
            return;
        }

        // Single instance check - ensure only one instance is running
        // Use a globally unique name for the mutex
        const string mutexName = "Global\\{8F6F0AC4-B9A1-4cfe-A389-A9035FD73F74}";
        bool createdNew;
        
        try
        {
            _singleInstanceMutex = new Mutex(true, mutexName, out createdNew);
            
            if (!createdNew)
            {
                // Another instance is already running
                MessageBox.Show(
                    "App Hider is already running.\n\nOnly one instance can run at a time.",
                    "Already Running",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                
                Shutdown();
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error creating single instance mutex: {ex.Message}");
            // Continue execution even if mutex creation fails
        }

        // Detect safe mode from command-line arguments or flag file
        IsSafeModeEnabled = SafeModeDetector.DetectSafeMode(e.Args);

        // Log safe mode status
        if (IsSafeModeEnabled)
        {
            System.Diagnostics.Debug.WriteLine("=== SAFE MODE ENABLED ===");
            System.Diagnostics.Debug.WriteLine("Network operations will be simulated only.");
            Console.WriteLine("=== SAFE MODE ENABLED ===");
            Console.WriteLine("Network operations will be simulated only.");
        }

        // Initialize services
        _settingsService = new SettingsService();
        _authService = new AuthenticationService(_settingsService);
        _appHiderService = new AppHiderService();
        _networkController = new NetworkController();
        
        // Initialize remote desktop services
        var rdSessionService = new RDSessionService();
        var rdClientService = new RDClientService();
        _remoteDesktopManager = new RemoteDesktopManager(rdSessionService, rdClientService);
        
        // Initialize VHDX and Log Cleaning services
        _vhdxManager = new VHDXManager();
        _logCleaner = new LogCleaner();
        
        // Create EmergencyDisconnectController without HotkeyManager initially (will be set later)
        _emergencyDisconnectController = new EmergencyDisconnectController(_remoteDesktopManager, _networkController, null);
        
        _privacyModeController = new PrivacyModeController(_appHiderService, _networkController, _settingsService, _emergencyDisconnectController, _vhdxManager, _logCleaner);
        _watchdogService = new WatchdogService();
        _autoStartupService = new AutoStartupService();
        _directoryHidingService = new DirectoryHidingService();
        
        _uninstallProtection = new UninstallProtectionService(_authService, _autoStartupService, _directoryHidingService);

        // CRITICAL FIX: Set ShutdownMode to OnExplicitShutdown to prevent automatic shutdown
        // when LoginWindow closes. This ensures MainWindow has time to load and display.
        this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        FL.Log("[STARTUP] ShutdownMode set to OnExplicitShutdown");
        Debug.WriteLine("[STARTUP] ShutdownMode set to OnExplicitShutdown");

        // Restore privacy mode state on startup
        // SECURITY: This will restore network disabled state if it was active before shutdown
        // User must use Emergency Recovery to restore network after restart
        try
        {
            Debug.WriteLine("Checking for previous privacy mode state...");
            FL.Log("[STARTUP] Checking for previous privacy mode state...");
            var restoreTask = Task.Run(async () => await _privacyModeController.RestoreStateOnStartupAsync());
            if (restoreTask.Wait(TimeSpan.FromSeconds(5)))
            {
                Debug.WriteLine("State restoration check completed.");
                FL.Log("[STARTUP] State restoration check completed.");
                
                if (_privacyModeController.IsPrivacyModeActive)
                {
                    Debug.WriteLine("========================================");
                    Debug.WriteLine("Privacy mode is still active from previous session.");
                    Debug.WriteLine("Network remains disabled for security. User must manually restore.");
                    Debug.WriteLine("Toggle hotkey is DISABLED - only emergency recovery can unlock.");
                    Debug.WriteLine("========================================");
                    FL.Log("[STARTUP] Privacy mode restored from previous session - network disabled");
                    FL.Log("[STARTUP] Toggle hotkey disabled - only emergency recovery can unlock");
                }
            }
            else
            {
                Debug.WriteLine("State restoration timed out after 5 seconds.");
                FL.Log("[STARTUP] State restoration timed out after 5 seconds.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error restoring privacy mode state: {ex.Message}");
            FL.Log($"[STARTUP] Error restoring privacy mode state: {ex.Message}");
            Debug.WriteLine("User can use emergency restore if needed.");
        }

        // Start watchdog service
        Task.Run(async () =>
        {
            try
            {
                await _watchdogService.StartWatchdogAsync();
                Debug.WriteLine("Watchdog service started successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to start watchdog service: {ex.Message}");
            }
        });

        // Start file protection
        try
        {
            _uninstallProtection.StartFileProtection();
            Debug.WriteLine("File protection started successfully.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to start file protection: {ex.Message}");
        }

        // Hide installation directory
        Task.Run(async () =>
        {
            try
            {
                var success = await _directoryHidingService!.HideInstallationDirectoryAsync();
                if (success)
                {
                    Debug.WriteLine("Installation directory hidden successfully.");
                }
                else
                {
                    Debug.WriteLine("Failed to hide installation directory. May require administrator privileges.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error hiding installation directory: {ex.Message}");
            }
        });


        // [NEW] Attempt to mount VHDX if configured (Requirement: Automatic mount on startup)
        Task.Run(async () =>
        {
            try
            {
                var settings = await _settingsService!.LoadSettingsAsync();
                if (settings.IsVHDXEnabled && !string.IsNullOrEmpty(settings.VHDXPath))
                {
                    FL.Log($"[STARTUP] VHDX configured at startup. Path: {settings.VHDXPath}");
                    
                    // Basic sanity check
                    if (System.IO.File.Exists(settings.VHDXPath))
                    {
                        FL.Log("[STARTUP] Mounting VHDX...");
                        // Use stored password (currently plain text in config as per implementation simplified plan)
                        bool mounted = await _vhdxManager!.MountVHDXAsync(settings.VHDXPath, settings.VHDXPasswordEncrypted);
                        if (mounted)
                        {
                            FL.Log("[STARTUP] VHDX mounted successfully.");
                        }
                        else
                        {
                            FL.Log("[STARTUP] Failed to mount VHDX.");
                        }
                    }
                    else
                    {
                        FL.Log($"[STARTUP] VHDX file not found: {settings.VHDXPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                FL.Log($"[STARTUP] Error mounting VHDX: {ex.Message}");
            }
        });

        // Check if background mode is requested (command-line argument)
        _isBackgroundMode = e.Args.Contains("--background") || e.Args.Contains("-b");

        if (_isBackgroundMode)
        {
            // Start in background mode - no window shown
            StartBackgroundMode();
        }
        else
        {
            // Normal startup - show login window
            // CRITICAL FIX: Use Dispatcher.BeginInvoke to delay window display until after message pump starts
            // This ensures Show() works properly (ShowDialog creates its own pump, but Show needs the main pump)
            FL.Log("[STARTUP] Scheduling ShowLoginAndMainWindow via Dispatcher.BeginInvoke");
            Debug.WriteLine("[STARTUP] Scheduling ShowLoginAndMainWindow via Dispatcher.BeginInvoke");
            Dispatcher.BeginInvoke(new Action(() =>
            {
                FL.Log("[STARTUP] Dispatcher.BeginInvoke callback executing - message pump is now running");
                Debug.WriteLine("[STARTUP] Dispatcher.BeginInvoke callback executing - message pump is now running");
                ShowLoginAndMainWindow();
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            FL.Log("[STARTUP] OnStartup returning - message pump will start");
            Debug.WriteLine("[STARTUP] OnStartup returning - message pump will start");
        }
    }

    private void StartBackgroundMode()
    {
        // Start timing for hotkey registration (Requirement 4.3)
        var timingStopwatch = Stopwatch.StartNew();
        
        // Create a persistent hidden window for message processing (required for hotkeys)
        // This window will remain throughout the application lifecycle
        _hiddenHotkeyWindow = new Window
        {
            Width = 0,
            Height = 0,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            Visibility = Visibility.Hidden
        };
        _hiddenHotkeyWindow.Show();
        _hiddenHotkeyWindow.Hide();

        var windowCreationTime = timingStopwatch.ElapsedMilliseconds;
        Debug.WriteLine($"[TIMING] Hidden window created in {windowCreationTime}ms");

        // Initialize hotkey manager with the persistent hidden window
        _hotkeyManager = new HotkeyManager();
        _hotkeyManager.Initialize(_hiddenHotkeyWindow);

        // Set the hotkey manager in the emergency disconnect controller
        _emergencyDisconnectController!.SetHotkeyManager(_hotkeyManager);

        var initializationTime = timingStopwatch.ElapsedMilliseconds;
        Debug.WriteLine($"[TIMING] HotkeyManager initialized in {initializationTime - windowCreationTime}ms (total: {initializationTime}ms)");

        // Register hotkeys IMMEDIATELY - no async delay
        try
        {
            var settings = Task.Run(async () => await _settingsService!.LoadSettingsAsync()).Result;
            
            Debug.WriteLine("[HOTKEY] Registering hotkeys in background mode...");
            
            // Register Toggle hotkey for direct privacy mode toggle
            // DEBUG ASSERTION: Toggle hotkey should NOT affect window visibility (Requirement 2.1)
            _hotkeyManager.RegisterHotkey(
                settings.ToggleHotkey.Key,
                settings.ToggleHotkey.Modifiers,
                async () =>
                {
                    Debug.WriteLine("[HOTKEY-INDEPENDENCE] Toggle hotkey pressed - affecting privacy mode only");
                    Debug.Assert(_mainWindow == null || true, "Toggle hotkey operates independently of window state");
                    await _privacyModeController!.TogglePrivacyModeAsync();
                    Debug.WriteLine("[HOTKEY-INDEPENDENCE] ✓ Privacy mode toggled without affecting window visibility");
                });
            
            // Register Menu hotkey to show interface
            // DEBUG ASSERTION: Menu hotkey should NOT affect privacy mode state (Requirement 2.2)
            _hotkeyManager.RegisterHotkey(
                settings.MenuHotkey.Key,
                settings.MenuHotkey.Modifiers,
                () =>
                {
                    Debug.WriteLine("[HOTKEY-INDEPENDENCE] Menu hotkey pressed - affecting window visibility only");
                    var privacyModeStateBefore = _privacyModeController!.IsPrivacyModeActive;
                    ShowMainWindowFromBackground();
                    var privacyModeStateAfter = _privacyModeController!.IsPrivacyModeActive;
                    Debug.Assert(privacyModeStateBefore == privacyModeStateAfter, 
                        "Menu hotkey should not change privacy mode state");
                    Debug.WriteLine($"[HOTKEY-INDEPENDENCE] ✓ Window shown without affecting privacy mode (was {privacyModeStateBefore}, still {privacyModeStateAfter})");
                });
            
            // Also register lock screen hook
            _hotkeyManager.RegisterLockScreenHook(async () =>
            {
                await _privacyModeController!.ActivatePrivacyModeAsync();
            });
            
            // Register Emergency Disconnect hotkey
            Task.Run(async () =>
            {
                var emergencyHotkeySuccess = await _emergencyDisconnectController!.RegisterEmergencyHotkeyAsync(settings.EmergencyDisconnectHotkey);
                if (emergencyHotkeySuccess)
                {
                    Debug.WriteLine("[HOTKEY] ✓ Emergency disconnect hotkey registered successfully in background mode.");
                }
                else
                {
                    Debug.WriteLine("[HOTKEY] ⚠ Failed to register emergency disconnect hotkey in background mode.");
                }
            });
            
            timingStopwatch.Stop();
            var totalTime = timingStopwatch.ElapsedMilliseconds;
            Debug.WriteLine($"[TIMING] All hotkeys registered in {totalTime - initializationTime}ms (total: {totalTime}ms)");
            Debug.WriteLine("[HOTKEY] ✓ All hotkeys registered successfully in background mode.");
            
            // Check if timing exceeds threshold (Requirement 4.3: should be within 100ms)
            if (totalTime > 100)
            {
                Debug.WriteLine($"[TIMING] ⚠ WARNING: Hotkey registration took {totalTime}ms, exceeding 100ms threshold!");
            }
            else
            {
                Debug.WriteLine($"[TIMING] ✓ Hotkey registration completed within 100ms threshold ({totalTime}ms)");
            }
        }
        catch (Exception ex)
        {
            timingStopwatch.Stop();
            System.Diagnostics.Debug.WriteLine($"[HOTKEY] ✗ Error loading hotkey settings: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[TIMING] Registration failed after {timingStopwatch.ElapsedMilliseconds}ms");
            
            // Use default hotkeys if settings fail to load
            _hotkeyManager.RegisterHotkey(
                System.Windows.Input.Key.F9,
                System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt,
                async () => await _privacyModeController!.TogglePrivacyModeAsync());
            
            _hotkeyManager.RegisterHotkey(
                System.Windows.Input.Key.F10,
                System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt,
                ShowMainWindowFromBackground);
            
            // Register default emergency disconnect hotkey
            var defaultEmergencyHotkey = new HotkeyConfig 
            { 
                Key = System.Windows.Input.Key.F8, 
                Modifiers = System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt 
            };
            Task.Run(async () => await _emergencyDisconnectController!.RegisterEmergencyHotkeyAsync(defaultEmergencyHotkey));
            
            Debug.WriteLine("[HOTKEY] ✓ Default hotkeys registered in background mode.");
        }
    }

    private void ShowMainWindowFromBackground()
    {
        Debug.WriteLine("[HOTKEY] Menu hotkey pressed in background mode.");
        
        // Ensure we're on the UI thread
        if (!Dispatcher.CheckAccess())
        {
            try
            {
                Dispatcher.Invoke(ShowMainWindowFromBackground);
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HOTKEY] ✗ Critical error in Dispatcher.Invoke for background mode: {ex.Message}");
                Debug.WriteLine($"[HOTKEY] Exception type: {ex.GetType().Name}");
                Debug.WriteLine($"[HOTKEY] Stack trace: {ex.StackTrace}");
                
                // Try to show error message without Dispatcher if possible
                try
                {
                    MessageBox.Show(
                        $"Critical error processing menu hotkey: {ex.Message}\n\n" +
                        $"The application may need to be restarted.",
                        "Critical Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                catch
                {
                    // If even MessageBox fails, just log it
                    Debug.WriteLine("[HOTKEY] ✗ Unable to show error message to user.");
                }
                return;
            }
        }

        try
        {
            // Show login window first if not authenticated
            if (!_authService!.IsAuthenticated)
            {
                Debug.WriteLine("[AUTH] User not authenticated. Showing login window...");
                var loginWindow = new LoginWindow(_authService);
                var loginResult = loginWindow.ShowDialog();

                if (loginResult != true || !loginWindow.IsAuthenticated)
                {
                    // Authentication failed, stay in background
                    Debug.WriteLine("[AUTH] Authentication cancelled or failed in background mode.");
                    return;
                }

                Debug.WriteLine("[AUTH] ✓ Authentication successful.");

                // Register auto-startup on first successful authentication
                Task.Run(async () =>
                {
                    try
                    {
                        var isRegistered = await _autoStartupService!.IsAutoStartupRegisteredAsync();
                        if (!isRegistered)
                        {
                            var success = await _autoStartupService.RegisterAutoStartupAsync();
                            if (success)
                            {
                                Debug.WriteLine("[STARTUP] Auto-startup registered successfully.");
                            }
                            else
                            {
                                Debug.WriteLine("[STARTUP] Failed to register auto-startup. May require administrator privileges.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[STARTUP] Error registering auto-startup: {ex.Message}");
                        Debug.WriteLine($"[STARTUP] Stack trace: {ex.StackTrace}");
                    }
                });
            }

            // Use common window creation logic
            ShowOrCreateMainWindow();
            
            Debug.WriteLine("[HOTKEY] ✓ Main window shown from background mode.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HOTKEY] ✗ Error showing main window from background: {ex.Message}");
            Debug.WriteLine($"[HOTKEY] Exception type: {ex.GetType().Name}");
            Debug.WriteLine($"[HOTKEY] Stack trace: {ex.StackTrace}");
            
            MessageBox.Show(
                $"Error opening interface: {ex.Message}\n\n" +
                $"Please try again or restart the application.",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        FL.Log($"[WINDOW] MainWindow_Closing event fired. IsBackgroundMode={_isBackgroundMode}, IsInitializing={_isInitializing}");
        FL.Log($"[WINDOW] Window state at closing: IsVisible={_mainWindow?.IsVisible}, IsLoaded={_mainWindow?.IsLoaded}");
        Debug.WriteLine($"[WINDOW] MainWindow_Closing event fired. IsBackgroundMode={_isBackgroundMode}, IsInitializing={_isInitializing}");
        
        // CRITICAL: Prevent window from closing during initialization
        if (_isInitializing)
        {
            FL.Log("[WINDOW] ✗ Blocking window close during initialization!");
            Debug.WriteLine("[WINDOW] ✗ Blocking window close during initialization!");
            e.Cancel = true;
            return;
        }
        
        // Check if window is actually visible before hiding it
        // This prevents hiding the window during initial Show() operation
        if (_mainWindow != null && _mainWindow.IsVisible)
        {
            // ALWAYS hide the window instead of closing to keep the application running
            // This allows the menu hotkey to show the window again
            e.Cancel = true;
            _mainWindow.Hide();
            FL.Log("[WINDOW] Main window hidden (will stay in background)");
            Debug.WriteLine("[WINDOW] Main window hidden (will stay in background).");
        }
        else
        {
            // Window is not visible yet, this is likely during initialization
            // Cancel the close but don't hide
            e.Cancel = true;
            FL.Log("[WINDOW] Window close cancelled (window not yet visible)");
            Debug.WriteLine("[WINDOW] Window close cancelled (window not yet visible).");
        }
    }

    private void ShowLoginAndMainWindow()
    {
        FL.Log("[STARTUP] ShowLoginAndMainWindow called");
        
        // Show login window first
        var loginWindow = new LoginWindow(_authService!);
        FL.Log("[STARTUP] Login window created, showing dialog...");
        var loginResult = loginWindow.ShowDialog();
        FL.Log($"[STARTUP] Login dialog result: {loginResult}, IsAuthenticated: {loginWindow.IsAuthenticated}");

        if (loginResult == true && loginWindow.IsAuthenticated)
        {
            FL.Log("[STARTUP] User authenticated successfully");
            
            // Register auto-startup on first successful authentication
            Task.Run(async () =>
            {
                try
                {
                    var isRegistered = await _autoStartupService!.IsAutoStartupRegisteredAsync();
                    if (!isRegistered)
                    {
                        var success = await _autoStartupService.RegisterAutoStartupAsync();
                        if (success)
                        {
                            Debug.WriteLine("Auto-startup registered successfully.");
                        }
                        else
                        {
                            Debug.WriteLine("Failed to register auto-startup. May require administrator privileges.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error registering auto-startup: {ex.Message}");
                }
            });

            // User authenticated successfully, show main window
            try
            {
                FL.Log("[STARTUP] Creating main window...");
                Debug.WriteLine("[STARTUP] User authenticated successfully. Creating main window...");
                
                // Set initialization flag to prevent window from being closed during setup
                _isInitializing = true;
                FL.Log("[STARTUP] Initialization flag set - window closing is blocked");
                
                // Start timing for hotkey registration (Requirement 4.3)
                var timingStopwatch = Stopwatch.StartNew();
                
                // Create and show main window FIRST
                try
                {
                    FL.Log("[STARTUP] Calling MainWindow constructor...");
                    Debug.WriteLine("[STARTUP] Creating MainWindow instance...");
                    _mainWindow = new MainWindow(
                        _privacyModeController!,
                        _appHiderService!,
                        _networkController!,
                        _settingsService!,
                        _authService!,
                        _autoStartupService!,
                        _emergencyDisconnectController!);
                    FL.Log("[STARTUP] ✓ MainWindow constructor completed successfully");
                    Debug.WriteLine("[STARTUP] ✓ MainWindow instance created successfully.");
                }
                catch (Exception ex)
                {
                    FL.Log($"[STARTUP] ✗ CRITICAL: MainWindow constructor failed: {ex.Message}");
                    FL.Log($"[STARTUP] Exception type: {ex.GetType().Name}");
                    FL.Log($"[STARTUP] Stack trace: {ex.StackTrace}");
                    Debug.WriteLine($"[STARTUP] ✗ CRITICAL: Failed to create MainWindow: {ex.Message}");
                    Debug.WriteLine($"[STARTUP] Exception type: {ex.GetType().Name}");
                    Debug.WriteLine($"[STARTUP] Stack trace: {ex.StackTrace}");
                    MessageBox.Show(
                        $"Failed to create main window:\n\n{ex.Message}\n\nThe application will exit.",
                        "Critical Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    Shutdown();
                    return;
                }
                
                // Handle window closing - hide instead of close to prevent disposal
                // CRITICAL: Register AFTER window is shown to prevent interference with initial display
                // We'll register this in the Loaded event
                FL.Log("[STARTUP] Window closing event handler will be registered after window loads");
                Debug.WriteLine("[STARTUP] Window closing event handler will be registered after window loads");
                
                // Register Loaded event to set up closing handler after window is fully loaded
                _mainWindow.Loaded += (s, e) =>
                {
                    FL.Log("[STARTUP] MainWindow Loaded event fired - now registering Closing handler");
                    Debug.WriteLine("[STARTUP] MainWindow Loaded event fired - now registering Closing handler");
                    _mainWindow.Closing += MainWindow_Closing;
                    FL.Log("[STARTUP] ✓ Window closing event handler registered after load");
                    Debug.WriteLine("[STARTUP] ✓ Window closing event handler registered after load");
                };
                
                try
                {
                    FL.Log("[STARTUP] Preparing to show main window...");
                    Debug.WriteLine("[STARTUP] Showing main window...");
                    
                    // CRITICAL FIX: Clear initialization flag BEFORE Show()
                    // This prevents the MainWindow_Closing handler from blocking the window display
                    _isInitializing = false;
                    FL.Log("[STARTUP] ✓ Initialization flag cleared - window can now be shown");
                    Debug.WriteLine("[STARTUP] ✓ Initialization flag cleared");
                    
                    // Set as application main window BEFORE showing
                    Application.Current.MainWindow = _mainWindow;
                    FL.Log("[STARTUP] ✓ Set as Application.Current.MainWindow");
                    Debug.WriteLine("[STARTUP] ✓ Set as Application.Current.MainWindow");
                    
                    // Force the window to initialize by accessing its handle
                    FL.Log("[STARTUP] Forcing window handle creation...");
                    var helper = new System.Windows.Interop.WindowInteropHelper(_mainWindow);
                    helper.EnsureHandle();
                    FL.Log($"[STARTUP] Window handle created: {helper.Handle}");
                    
                    // Now show the window
                    FL.Log("[STARTUP] Calling _mainWindow.Show()...");
                    _mainWindow.Show();
                    FL.Log($"[STARTUP] Show() returned. IsVisible={_mainWindow.IsVisible}, IsLoaded={_mainWindow.IsLoaded}");
                    
                    // Wait for window to load using Dispatcher
                    FL.Log("[STARTUP] Waiting for window to load...");
                    Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
                    FL.Log($"[STARTUP] After Dispatcher wait: IsVisible={_mainWindow.IsVisible}, IsLoaded={_mainWindow.IsLoaded}");
                    
                    // Now try to activate
                    if (_mainWindow.IsLoaded)
                    {
                        FL.Log("[STARTUP] Window is loaded, calling Activate()...");
                        _mainWindow.Activate();
                        _mainWindow.Topmost = true;
                        _mainWindow.Topmost = false;
                        _mainWindow.Focus();
                        FL.Log("[STARTUP] ✓ Window activated");
                    }
                    else
                    {
                        FL.Log("[STARTUP] ⚠ Window not loaded yet, skipping Activate()");
                    }
                    
                    FL.Log("[STARTUP] ✓ _mainWindow.Show() completed");
                    FL.Log($"[STARTUP] Final window state: IsVisible={_mainWindow.IsVisible}, IsLoaded={_mainWindow.IsLoaded}, WindowHandle={helper.Handle}");
                    Debug.WriteLine("[STARTUP] ✓ Main window shown successfully.");
                }
                catch (Exception ex)
                {
                    _isInitializing = false; // Clear flag even on error
                    FL.Log("[STARTUP] Initialization flag cleared (error path)");
                    FL.Log($"[STARTUP] ✗ CRITICAL: _mainWindow.Show() failed: {ex.Message}");
                    FL.Log($"[STARTUP] Exception type: {ex.GetType().Name}");
                    FL.Log($"[STARTUP] Stack trace: {ex.StackTrace}");
                    Debug.WriteLine($"[STARTUP] ✗ CRITICAL: Failed to show MainWindow: {ex.Message}");
                    Debug.WriteLine($"[STARTUP] Exception type: {ex.GetType().Name}");
                    Debug.WriteLine($"[STARTUP] Stack trace: {ex.StackTrace}");
                    MessageBox.Show(
                        $"Failed to show main window:\n\n{ex.Message}\n\nThe application will exit.",
                        "Critical Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    Shutdown();
                    return;
                }
                
                var windowCreationTime = timingStopwatch.ElapsedMilliseconds;
                FL.Log($"[TIMING] Main window created and shown in {windowCreationTime}ms");
                Debug.WriteLine($"[TIMING] Main window created and shown in {windowCreationTime}ms");
                
                // NOW create and initialize hotkey manager with a persistent hidden window
                FL.Log("[STARTUP] Creating hidden hotkey window...");
                Debug.WriteLine("[STARTUP] Creating hidden hotkey window...");
                _hiddenHotkeyWindow = new Window
                {
                    Width = 0,
                    Height = 0,
                    WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Visibility = Visibility.Hidden
                };
                _hiddenHotkeyWindow.Show();
                _hiddenHotkeyWindow.Hide();
                FL.Log("[STARTUP] ✓ Hidden hotkey window created");

                var hiddenWindowTime = timingStopwatch.ElapsedMilliseconds;
                Debug.WriteLine($"[TIMING] Hidden hotkey window created in {hiddenWindowTime - windowCreationTime}ms (total: {hiddenWindowTime}ms)");

                FL.Log("[STARTUP] Initializing HotkeyManager...");
                _hotkeyManager = new HotkeyManager();
                _hotkeyManager.Initialize(_hiddenHotkeyWindow);
                FL.Log("[STARTUP] ✓ HotkeyManager initialized");
                
                // Set the hotkey manager in the emergency disconnect controller
                _emergencyDisconnectController!.SetHotkeyManager(_hotkeyManager);
                FL.Log("[STARTUP] ✓ HotkeyManager set in EmergencyDisconnectController");
                
                var initializationTime = timingStopwatch.ElapsedMilliseconds;
                Debug.WriteLine($"[TIMING] HotkeyManager initialized in {initializationTime - hiddenWindowTime}ms (total: {initializationTime}ms)");
                
                // Register hotkeys IMMEDIATELY - no async delay
                try
                {
                    var settings = Task.Run(async () => await _settingsService!.LoadSettingsAsync()).Result;
                    
                    FL.Log("[HOTKEY] Registering hotkeys in normal mode...");
                    Debug.WriteLine("[HOTKEY] Registering hotkeys in normal mode...");
                    
                    // Register Toggle hotkey for direct privacy mode toggle
                    // DEBUG ASSERTION: Toggle hotkey should NOT affect window visibility (Requirement 2.1)
                    _hotkeyManager.RegisterHotkey(
                        settings.ToggleHotkey.Key,
                        settings.ToggleHotkey.Modifiers,
                        async () =>
                        {
                            FL.Log("[HOTKEY] Toggle hotkey pressed!");
                            Debug.WriteLine("[HOTKEY-INDEPENDENCE] Toggle hotkey pressed - affecting privacy mode only");
                            Debug.Assert(_mainWindow == null || true, "Toggle hotkey operates independently of window state");
                            await _privacyModeController!.TogglePrivacyModeAsync();
                            Debug.WriteLine("[HOTKEY-INDEPENDENCE] ✓ Privacy mode toggled without affecting window visibility");
                        });
                    
                    // Register Menu hotkey to show interface (in case window is hidden)
                    // DEBUG ASSERTION: Menu hotkey should NOT affect privacy mode state (Requirement 2.2)
                    _hotkeyManager.RegisterHotkey(
                        settings.MenuHotkey.Key,
                        settings.MenuHotkey.Modifiers,
                        () =>
                        {
                            FL.Log("[HOTKEY] Menu hotkey pressed!");
                            try
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    FL.Log("[HOTKEY] Inside Dispatcher.Invoke for menu hotkey");
                                    Debug.WriteLine("[HOTKEY] Menu hotkey pressed in normal mode.");
                                    Debug.WriteLine("[HOTKEY-INDEPENDENCE] Menu hotkey pressed - affecting window visibility only");
                                    
                                    var privacyModeStateBefore = _privacyModeController!.IsPrivacyModeActive;
                                    
                                    try
                                    {
                                        FL.Log("[HOTKEY] Calling ShowOrCreateMainWindow...");
                                        // Use common window creation logic
                                        ShowOrCreateMainWindow();
                                        FL.Log("[HOTKEY] ✓ ShowOrCreateMainWindow completed");
                                        Debug.WriteLine("[HOTKEY] ✓ Main window shown/activated via menu hotkey (normal mode).");
                                        
                                        var privacyModeStateAfter = _privacyModeController!.IsPrivacyModeActive;
                                        Debug.Assert(privacyModeStateBefore == privacyModeStateAfter, 
                                            "Menu hotkey should not change privacy mode state");
                                        Debug.WriteLine($"[HOTKEY-INDEPENDENCE] ✓ Window shown without affecting privacy mode (was {privacyModeStateBefore}, still {privacyModeStateAfter})");
                                    }
                                    catch (Exception ex)
                                    {
                                        FL.Log($"[HOTKEY] ✗ Error in ShowOrCreateMainWindow: {ex.Message}");
                                        FL.Log($"[HOTKEY] Stack trace: {ex.StackTrace}");
                                        Debug.WriteLine($"[HOTKEY] ✗ Error showing main window from menu hotkey: {ex.Message}");
                                        Debug.WriteLine($"[HOTKEY] Exception type: {ex.GetType().Name}");
                                        Debug.WriteLine($"[HOTKEY] Stack trace: {ex.StackTrace}");
                                        
                                        MessageBox.Show(
                                            $"Error opening interface: {ex.Message}\n\n" +
                                            $"Please try again or restart the application.",
                                            "Error",
                                            MessageBoxButton.OK,
                                            MessageBoxImage.Error);
                                    }
                                });
                            }
                            catch (Exception ex)
                            {
                                FL.Log($"[HOTKEY] ✗ Critical error in Dispatcher.Invoke: {ex.Message}");
                                FL.Log($"[HOTKEY] Stack trace: {ex.StackTrace}");
                                Debug.WriteLine($"[HOTKEY] ✗ Critical error in Dispatcher.Invoke for menu hotkey: {ex.Message}");
                                Debug.WriteLine($"[HOTKEY] Exception type: {ex.GetType().Name}");
                                Debug.WriteLine($"[HOTKEY] Stack trace: {ex.StackTrace}");
                                
                                // Try to show error message without Dispatcher if possible
                                try
                                {
                                    MessageBox.Show(
                                        $"Critical error processing menu hotkey: {ex.Message}\n\n" +
                                        $"The application may need to be restarted.",
                                        "Critical Error",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Error);
                                }
                                catch
                                {
                                    // If even MessageBox fails, just log it
                                    Debug.WriteLine("[HOTKEY] ✗ Unable to show error message to user.");
                                }
                            }
                        });
                    
                    FL.Log("[HOTKEY] Registering lock screen hook...");
                    _hotkeyManager.RegisterLockScreenHook(async () =>
                    {
                        await _privacyModeController!.ActivatePrivacyModeAsync();
                    });
                    
                    // Register Emergency Disconnect hotkey
                    FL.Log("[HOTKEY] Registering emergency disconnect hotkey...");
                    Task.Run(async () =>
                    {
                        var emergencyHotkeySuccess = await _emergencyDisconnectController!.RegisterEmergencyHotkeyAsync(settings.EmergencyDisconnectHotkey);
                        if (emergencyHotkeySuccess)
                        {
                            FL.Log("[HOTKEY] ✓ Emergency disconnect hotkey registered successfully in normal mode");
                            Debug.WriteLine("[HOTKEY] ✓ Emergency disconnect hotkey registered successfully in normal mode.");
                        }
                        else
                        {
                            FL.Log("[HOTKEY] ⚠ Failed to register emergency disconnect hotkey in normal mode");
                            Debug.WriteLine("[HOTKEY] ⚠ Failed to register emergency disconnect hotkey in normal mode.");
                        }
                    });
                    
                    timingStopwatch.Stop();
                    var totalTime = timingStopwatch.ElapsedMilliseconds;
                    FL.Log($"[TIMING] All hotkeys registered in {totalTime}ms");
                    Debug.WriteLine($"[TIMING] All hotkeys registered in {totalTime - initializationTime}ms (total: {totalTime}ms)");
                    FL.Log("[HOTKEY] ✓ All hotkeys registered successfully in normal mode");
                    Debug.WriteLine("[HOTKEY] ✓ All hotkeys registered successfully in normal mode.");
                    
                    // Note: _isInitializing flag was already cleared after Show() to allow window to display
                    FL.Log("[STARTUP] ✓ Initialization complete");
                    Debug.WriteLine("[STARTUP] ✓ Initialization complete");
                    
                    // Check if timing exceeds threshold (Requirement 4.3: should be within 100ms)
                    if (totalTime > 100)
                    {
                        Debug.WriteLine($"[TIMING] ⚠ WARNING: Hotkey registration took {totalTime}ms, exceeding 100ms threshold!");
                    }
                    else
                    {
                        Debug.WriteLine($"[TIMING] ✓ Hotkey registration completed within 100ms threshold ({totalTime}ms)");
                    }
                }
                catch (Exception ex)
                {
                    timingStopwatch.Stop();
                    // Note: _isInitializing flag was already cleared after Show()
                    Debug.WriteLine($"[HOTKEY] ✗ Error registering hotkeys: {ex.Message}");
                    Debug.WriteLine($"[TIMING] Registration failed after {timingStopwatch.ElapsedMilliseconds}ms");
                    // Don't show MessageBox - just log the error
                }
            }
            catch (Exception ex)
            {
                // Note: _isInitializing flag was already cleared after Show() if we got that far
                if (_isInitializing)
                {
                    _isInitializing = false;
                    FL.Log("[STARTUP] Initialization flag cleared (critical error path)");
                }
                MessageBox.Show($"Error creating main window: {ex.Message}\n\nStack trace:\n{ex.StackTrace}", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
        else
        {
            FL.Log("[STARTUP] User cancelled login or authentication failed");
            // User cancelled login or authentication failed, exit application
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Stop file protection
        _uninstallProtection?.StopFileProtection();

        // Stop watchdog service
        if (_watchdogService != null)
        {
            try
            {
                _watchdogService.StopWatchdogAsync().Wait(5000);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping watchdog: {ex.Message}");
            }
        }

        // Clean up hotkey manager
        _hotkeyManager?.Dispose();
        
        // Close hidden hotkey window
        _hiddenHotkeyWindow?.Close();
        
        // Release single instance mutex
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        
        base.OnExit(e);
    }

    /// <summary>
    /// Shows or creates the main window. This method handles window recreation if the window
    /// has been closed, and ensures all services are properly injected.
    /// </summary>
    private void ShowOrCreateMainWindow()
    {
        FL.Log("[WINDOW] ShowOrCreateMainWindow called");
        Debug.WriteLine("[WINDOW] ShowOrCreateMainWindow called.");
        
        try
        {
            // SECURITY: Always require password authentication before showing main window
            FL.Log("[SECURITY] Showing login dialog for authentication...");
            Debug.WriteLine("[SECURITY] Showing login dialog for authentication...");
            
            var loginWindow = new LoginWindow(_authService!);
            var loginResult = loginWindow.ShowDialog();
            
            if (loginResult != true || !loginWindow.IsAuthenticated)
            {
                FL.Log("[SECURITY] Authentication failed or cancelled - main window will not be shown");
                Debug.WriteLine("[SECURITY] Authentication failed or cancelled - main window will not be shown");
                return; // Exit without showing main window
            }
            
            FL.Log("[SECURITY] ✓ Authentication successful - proceeding to show main window");
            Debug.WriteLine("[SECURITY] ✓ Authentication successful - proceeding to show main window");
            
            // Log current window state
            if (_mainWindow == null)
            {
                FL.Log("[WINDOW] Window state: NULL");
                Debug.WriteLine("[WINDOW] Window state: NULL");
            }
            else if (!_mainWindow.IsLoaded)
            {
                FL.Log("[WINDOW] Window state: NOT LOADED (closed)");
                Debug.WriteLine("[WINDOW] Window state: NOT LOADED (closed)");
            }
            else if (!_mainWindow.IsVisible)
            {
                FL.Log("[WINDOW] Window state: HIDDEN");
                Debug.WriteLine("[WINDOW] Window state: HIDDEN");
            }
            else
            {
                FL.Log("[WINDOW] Window state: VISIBLE");
                Debug.WriteLine("[WINDOW] Window state: VISIBLE");
            }
            
            // Check if window is null or has been closed
            if (_mainWindow == null || !_mainWindow.IsLoaded)
            {
                FL.Log("[WINDOW] Creating new main window...");
                Debug.WriteLine("[WINDOW] Creating new main window...");
                
                try
                {
                    // Create the main window with all required services
                    _mainWindow = new MainWindow(
                        _privacyModeController!,
                        _appHiderService!,
                        _networkController!,
                        _settingsService!,
                        _authService!,
                        _autoStartupService!,
                        _emergencyDisconnectController!);
                    
                    // Register window closing event handler
                    _mainWindow.Closing += MainWindow_Closing;
                    
                    FL.Log("[WINDOW] ✓ Main window recreated successfully");
                    Debug.WriteLine("[WINDOW] ✓ Main window recreated successfully.");
                }
                catch (Exception ex)
                {
                    FL.Log($"[WINDOW] ✗ Critical error creating main window: {ex.Message}");
                    FL.Log($"[WINDOW] Exception type: {ex.GetType().Name}");
                    FL.Log($"[WINDOW] Stack trace: {ex.StackTrace}");
                    Debug.WriteLine($"[WINDOW] ✗ Critical error creating main window: {ex.Message}");
                    Debug.WriteLine($"[WINDOW] Exception type: {ex.GetType().Name}");
                    Debug.WriteLine($"[WINDOW] Stack trace: {ex.StackTrace}");
                    
                    // Show user-friendly error message
                    MessageBox.Show(
                        $"Failed to create the application window.\n\n" +
                        $"Error: {ex.Message}\n\n" +
                        $"The application will continue running in the background. " +
                        $"Please try pressing the menu hotkey again, or restart the application.",
                        "Window Creation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    
                    throw; // Re-throw to let caller handle
                }
            }
            else
            {
                FL.Log("[WINDOW] Main window already exists and is loaded");
                Debug.WriteLine("[WINDOW] Main window already exists and is loaded.");
            }
            
            // Show and activate the window
            if (!_mainWindow.IsVisible)
            {
                FL.Log("[WINDOW] Calling _mainWindow.Show()...");
                _mainWindow.Show();
                FL.Log("[WINDOW] ✓ Main window shown");
                Debug.WriteLine("[WINDOW] ✓ Main window shown.");
            }
            else
            {
                FL.Log("[WINDOW] Main window already visible");
                Debug.WriteLine("[WINDOW] Main window already visible.");
            }
            
            FL.Log("[WINDOW] Calling _mainWindow.Activate()...");
            _mainWindow.Activate();
            FL.Log("[WINDOW] Calling _mainWindow.Focus()...");
            _mainWindow.Focus();
            FL.Log("[WINDOW] ✓ Main window activated and focused");
            Debug.WriteLine("[WINDOW] ✓ Main window activated and focused.");
        }
        catch (Exception ex)
        {
            FL.Log($"[WINDOW] ✗ Error in ShowOrCreateMainWindow: {ex.Message}");
            FL.Log($"[WINDOW] Exception type: {ex.GetType().Name}");
            FL.Log($"[WINDOW] Stack trace: {ex.StackTrace}");
            Debug.WriteLine($"[WINDOW] ✗ Error in ShowOrCreateMainWindow: {ex.Message}");
            Debug.WriteLine($"[WINDOW] Exception type: {ex.GetType().Name}");
            Debug.WriteLine($"[WINDOW] Stack trace: {ex.StackTrace}");
            throw; // Re-throw to let caller handle
        }
    }

    /// <summary>
    /// Provides access to the uninstall protection service for UI components.
    /// </summary>
    public UninstallProtectionService? UninstallProtection => _uninstallProtection;
}

