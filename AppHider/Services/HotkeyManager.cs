using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using AppHider.Models;

namespace AppHider.Services;

public class HotkeyManager : IHotkeyManager, IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int WM_WTSSESSION_CHANGE = 0x02B1;
    private const int WTS_SESSION_LOCK = 0x7;
    private const int NOTIFY_FOR_THIS_SESSION = 0;

    private readonly Dictionary<int, (Key key, ModifierKeys modifiers, Action callback)> _registeredHotkeys = new();
    private readonly Dictionary<string, HotkeyConfig> _namedHotkeys = new(); // Track named hotkeys for conflict detection
    private Action? _lockScreenCallback;
    private IntPtr _windowHandle;
    private HwndSource? _hwndSource;
    private int _nextHotkeyId = 1;
    private bool _disposed;
    private bool _initialized;
    private HotkeyConfig? _emergencyDisconnectHotkey;
    private int _emergencyDisconnectHotkeyId = -1;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    // Win32 API imports
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSRegisterSessionNotification(IntPtr hWnd, int dwFlags);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSUnRegisterSessionNotification(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    public void Initialize(Window window)
    {
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        // Prevent re-initialization - use the first window handle throughout the application lifecycle
        if (_initialized)
        {
            System.Diagnostics.Debug.WriteLine("HotkeyManager already initialized. Ignoring re-initialization request.");
            return;
        }

        _windowHandle = new WindowInteropHelper(window).Handle;
        
        if (_windowHandle == IntPtr.Zero)
        {
            // Window not yet created, wait for SourceInitialized
            window.SourceInitialized += (s, e) =>
            {
                _windowHandle = new WindowInteropHelper(window).Handle;
                SetupMessageHook();
                _initialized = true;
            };
        }
        else
        {
            SetupMessageHook();
            _initialized = true;
        }
    }

    private void SetupMessageHook()
    {
        _hwndSource = HwndSource.FromHwnd(_windowHandle);
        if (_hwndSource != null)
        {
            _hwndSource.AddHook(WndProc);
        }
    }

    public void RegisterHotkey(Key key, ModifierKeys modifiers, Action callback)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        if (_windowHandle == IntPtr.Zero)
            throw new InvalidOperationException("HotkeyManager must be initialized with a window before registering hotkeys");

        // First, unregister any existing hotkey with the same key combination
        UnregisterHotkey(key, modifiers);

        uint modifierFlags = ConvertModifiers(modifiers);
        uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);

        int hotkeyId = _nextHotkeyId++;

        System.Diagnostics.Debug.WriteLine($"[HOTKEY] Attempting to register hotkey: {modifiers}+{key} (ID: {hotkeyId})");
        
        if (RegisterHotKey(_windowHandle, hotkeyId, modifierFlags, virtualKey))
        {
            _registeredHotkeys[hotkeyId] = (key, modifiers, callback);
            System.Diagnostics.Debug.WriteLine($"[HOTKEY] ✓ Hotkey registered successfully: {modifiers}+{key} (ID: {hotkeyId})");
        }
        else
        {
            int error = Marshal.GetLastWin32Error();
            System.Diagnostics.Debug.WriteLine($"[HOTKEY] ✗ Failed to register hotkey {modifiers}+{key}. Error code: {error}");
            throw new InvalidOperationException($"Failed to register hotkey {modifiers}+{key}. Error code: {error}");
        }
    }

    public void UnregisterHotkey(Key key, ModifierKeys modifiers)
    {
        if (_windowHandle == IntPtr.Zero)
            return;

        var hotkeyToRemove = _registeredHotkeys.FirstOrDefault(kvp => 
            kvp.Value.key == key && kvp.Value.modifiers == modifiers);

        if (hotkeyToRemove.Key != 0)
        {
            bool success = UnregisterHotKey(_windowHandle, hotkeyToRemove.Key);
            if (success)
            {
                _registeredHotkeys.Remove(hotkeyToRemove.Key);
                System.Diagnostics.Debug.WriteLine($"Hotkey unregistered successfully: {modifiers}+{key} (ID: {hotkeyToRemove.Key})");
            }
            else
            {
                int error = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"Failed to unregister hotkey {modifiers}+{key}. Error code: {error}");
            }
        }
    }

    public void RegisterLockScreenHook(Action callback)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        if (_windowHandle == IntPtr.Zero)
            throw new InvalidOperationException("HotkeyManager must be initialized with a window before registering lock screen hook");

        _lockScreenCallback = callback;

        if (!WTSRegisterSessionNotification(_windowHandle, NOTIFY_FOR_THIS_SESSION))
        {
            int error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to register session notification. Error code: {error}");
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_HOTKEY:
                HandleHotkey(wParam);
                handled = true;
                break;

            case WM_WTSSESSION_CHANGE:
                HandleSessionChange(wParam);
                handled = true;
                break;
        }

        return IntPtr.Zero;
    }

    private void HandleHotkey(IntPtr wParam)
    {
        int hotkeyId = wParam.ToInt32();

        System.Diagnostics.Debug.WriteLine($"[HOTKEY] Hotkey pressed (ID: {hotkeyId})");

        if (_registeredHotkeys.TryGetValue(hotkeyId, out var hotkeyInfo))
        {
            System.Diagnostics.Debug.WriteLine($"[HOTKEY] Executing callback for: {hotkeyInfo.modifiers}+{hotkeyInfo.key}");
            
            // Raise event
            HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs
            {
                Key = hotkeyInfo.key,
                Modifiers = hotkeyInfo.modifiers
            });

            // Execute callback
            try
            {
                hotkeyInfo.callback?.Invoke();
                System.Diagnostics.Debug.WriteLine($"[HOTKEY] ✓ Callback executed successfully for: {hotkeyInfo.modifiers}+{hotkeyInfo.key}");
            }
            catch (Exception ex)
            {
                // Log error but don't crash
                System.Diagnostics.Debug.WriteLine($"[HOTKEY] ✗ Error executing hotkey callback: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[HOTKEY] Stack trace: {ex.StackTrace}");
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[HOTKEY] ⚠ No callback registered for hotkey ID: {hotkeyId}");
        }
    }

    private void HandleSessionChange(IntPtr wParam)
    {
        int sessionChangeReason = wParam.ToInt32();

        if (sessionChangeReason == WTS_SESSION_LOCK)
        {
            // Lock screen detected
            try
            {
                _lockScreenCallback?.Invoke();
            }
            catch (Exception ex)
            {
                // Log error but don't crash
                System.Diagnostics.Debug.WriteLine($"Error executing lock screen callback: {ex.Message}");
            }
        }
    }

    private uint ConvertModifiers(ModifierKeys modifiers)
    {
        uint result = 0;

        if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
            result |= 0x0001; // MOD_ALT

        if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            result |= 0x0002; // MOD_CONTROL

        if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            result |= 0x0004; // MOD_SHIFT

        if ((modifiers & ModifierKeys.Windows) == ModifierKeys.Windows)
            result |= 0x0008; // MOD_WIN

        return result;
    }

    /// <summary>
    /// Registers the emergency disconnect hotkey with validation and conflict detection
    /// Requirements: 4.2 (validation), 4.3 (global registration), 4.4 (default configuration)
    /// </summary>
    public bool RegisterEmergencyDisconnectHotkey(HotkeyConfig hotkeyConfig, Action callback)
    {
        if (hotkeyConfig == null)
            throw new ArgumentNullException(nameof(hotkeyConfig));
        
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        try
        {
            System.Diagnostics.Debug.WriteLine($"[HOTKEY] Registering emergency disconnect hotkey: {hotkeyConfig.Modifiers}+{hotkeyConfig.Key}");

            // Validate the hotkey configuration
            if (!ValidateHotkeyConfig(hotkeyConfig, out string? errorMessage))
            {
                System.Diagnostics.Debug.WriteLine($"[HOTKEY] Emergency disconnect hotkey validation failed: {errorMessage}");
                return false;
            }

            // Check if hotkey is available (not already registered)
            if (!IsHotkeyAvailable(hotkeyConfig.Key, hotkeyConfig.Modifiers))
            {
                System.Diagnostics.Debug.WriteLine($"[HOTKEY] Emergency disconnect hotkey is already in use: {hotkeyConfig.Modifiers}+{hotkeyConfig.Key}");
                return false;
            }

            // Unregister existing emergency disconnect hotkey if any
            UnregisterEmergencyDisconnectHotkey();

            // Register the new hotkey
            if (_windowHandle == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("[HOTKEY] Cannot register emergency disconnect hotkey: HotkeyManager not initialized");
                return false;
            }

            uint modifierFlags = ConvertModifiers(hotkeyConfig.Modifiers);
            uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(hotkeyConfig.Key);

            int hotkeyId = _nextHotkeyId++;

            if (RegisterHotKey(_windowHandle, hotkeyId, modifierFlags, virtualKey))
            {
                _registeredHotkeys[hotkeyId] = (hotkeyConfig.Key, hotkeyConfig.Modifiers, callback);
                _namedHotkeys["EmergencyDisconnect"] = hotkeyConfig;
                _emergencyDisconnectHotkey = hotkeyConfig;
                _emergencyDisconnectHotkeyId = hotkeyId;

                System.Diagnostics.Debug.WriteLine($"[HOTKEY] ✓ Emergency disconnect hotkey registered successfully: {hotkeyConfig.Modifiers}+{hotkeyConfig.Key} (ID: {hotkeyId})");
                return true;
            }
            else
            {
                int error = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"[HOTKEY] ✗ Failed to register emergency disconnect hotkey {hotkeyConfig.Modifiers}+{hotkeyConfig.Key}. Error code: {error}");
                return false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HOTKEY] Exception registering emergency disconnect hotkey: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Unregisters the current emergency disconnect hotkey
    /// </summary>
    public bool UnregisterEmergencyDisconnectHotkey()
    {
        try
        {
            if (_emergencyDisconnectHotkeyId == -1 || _emergencyDisconnectHotkey == null)
            {
                System.Diagnostics.Debug.WriteLine("[HOTKEY] No emergency disconnect hotkey to unregister");
                return true;
            }

            System.Diagnostics.Debug.WriteLine($"[HOTKEY] Unregistering emergency disconnect hotkey: {_emergencyDisconnectHotkey.Modifiers}+{_emergencyDisconnectHotkey.Key}");

            if (_windowHandle != IntPtr.Zero)
            {
                bool success = UnregisterHotKey(_windowHandle, _emergencyDisconnectHotkeyId);
                if (success)
                {
                    _registeredHotkeys.Remove(_emergencyDisconnectHotkeyId);
                    _namedHotkeys.Remove("EmergencyDisconnect");
                    _emergencyDisconnectHotkey = null;
                    _emergencyDisconnectHotkeyId = -1;

                    System.Diagnostics.Debug.WriteLine("[HOTKEY] ✓ Emergency disconnect hotkey unregistered successfully");
                    return true;
                }
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    System.Diagnostics.Debug.WriteLine($"[HOTKEY] ✗ Failed to unregister emergency disconnect hotkey. Error code: {error}");
                    return false;
                }
            }

            // Clear the references even if unregistration failed
            _emergencyDisconnectHotkey = null;
            _emergencyDisconnectHotkeyId = -1;
            _namedHotkeys.Remove("EmergencyDisconnect");
            
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HOTKEY] Exception unregistering emergency disconnect hotkey: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks if a hotkey combination is available (not already registered)
    /// Requirements: 4.2 (conflict detection)
    /// </summary>
    public bool IsHotkeyAvailable(Key key, ModifierKeys modifiers)
    {
        try
        {
            // Check if already registered in our internal tracking
            var existingHotkey = _registeredHotkeys.Values.FirstOrDefault(h => h.key == key && h.modifiers == modifiers);
            if (existingHotkey.callback != null)
            {
                System.Diagnostics.Debug.WriteLine($"[HOTKEY] Hotkey {modifiers}+{key} is already registered internally");
                return false;
            }

            // Try to register temporarily to check system availability
            if (_windowHandle == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("[HOTKEY] Cannot check hotkey availability: HotkeyManager not initialized");
                return false;
            }

            uint modifierFlags = ConvertModifiers(modifiers);
            uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);

            // Use a temporary ID for testing
            int testId = 9999;
            
            bool canRegister = RegisterHotKey(_windowHandle, testId, modifierFlags, virtualKey);
            if (canRegister)
            {
                // Immediately unregister the test hotkey
                UnregisterHotKey(_windowHandle, testId);
                System.Diagnostics.Debug.WriteLine($"[HOTKEY] Hotkey {modifiers}+{key} is available");
                return true;
            }
            else
            {
                int error = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"[HOTKEY] Hotkey {modifiers}+{key} is not available. Error code: {error}");
                return false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HOTKEY] Exception checking hotkey availability: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Validates a hotkey configuration for correctness and system compatibility
    /// Requirements: 4.2 (validation logic)
    /// </summary>
    public bool ValidateHotkeyConfig(HotkeyConfig hotkeyConfig, out string? errorMessage)
    {
        errorMessage = null;

        try
        {
            if (hotkeyConfig == null)
            {
                errorMessage = "Hotkey configuration cannot be null";
                return false;
            }

            // Check if key is valid
            if (hotkeyConfig.Key == Key.None)
            {
                errorMessage = "Key cannot be None";
                return false;
            }

            // Check if at least one modifier is specified (recommended for global hotkeys)
            if (hotkeyConfig.Modifiers == ModifierKeys.None)
            {
                errorMessage = "At least one modifier key (Ctrl, Alt, Shift, or Windows) should be specified for global hotkeys";
                return false;
            }

            // Check for invalid key combinations
            var invalidKeys = new[]
            {
                Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt,
                Key.LeftShift, Key.RightShift, Key.LWin, Key.RWin,
                Key.CapsLock, Key.NumLock, Key.Scroll
            };

            if (invalidKeys.Contains(hotkeyConfig.Key))
            {
                errorMessage = $"Key '{hotkeyConfig.Key}' cannot be used as the main key in a hotkey combination";
                return false;
            }

            // Check for system reserved combinations
            var reservedCombinations = new[]
            {
                (ModifierKeys.Control | ModifierKeys.Alt, Key.Delete), // Ctrl+Alt+Del
                (ModifierKeys.Windows, Key.L), // Win+L (Lock screen)
                (ModifierKeys.Alt, Key.Tab), // Alt+Tab
                (ModifierKeys.Alt, Key.F4), // Alt+F4
                (ModifierKeys.Control | ModifierKeys.Shift, Key.Escape), // Ctrl+Shift+Esc
            };

            foreach (var (modifiers, key) in reservedCombinations)
            {
                if (hotkeyConfig.Modifiers == modifiers && hotkeyConfig.Key == key)
                {
                    errorMessage = $"The combination {modifiers}+{key} is reserved by the system";
                    return false;
                }
            }

            System.Diagnostics.Debug.WriteLine($"[HOTKEY] Hotkey configuration {hotkeyConfig.Modifiers}+{hotkeyConfig.Key} is valid");
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Error validating hotkey configuration: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[HOTKEY] Exception validating hotkey config: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets a read-only dictionary of currently registered named hotkeys
    /// Requirements: 4.2 (conflict detection support)
    /// </summary>
    public IReadOnlyDictionary<string, HotkeyConfig> GetRegisteredHotkeys()
    {
        return _namedHotkeys.AsReadOnly();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        // Unregister emergency disconnect hotkey
        UnregisterEmergencyDisconnectHotkey();

        // Unregister all hotkeys
        if (_windowHandle != IntPtr.Zero)
        {
            foreach (var hotkeyId in _registeredHotkeys.Keys.ToList())
            {
                UnregisterHotKey(_windowHandle, hotkeyId);
            }
            _registeredHotkeys.Clear();
            _namedHotkeys.Clear();

            // Unregister session notification
            if (_lockScreenCallback != null)
            {
                WTSUnRegisterSessionNotification(_windowHandle);
                _lockScreenCallback = null;
            }
        }

        // Remove message hook
        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }

        _disposed = true;
    }
}
