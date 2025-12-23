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
    private Action? _lockScreenCallback;
    private IntPtr _windowHandle;
    private HwndSource? _hwndSource;
    private int _nextHotkeyId = 1;
    private bool _disposed;
    private bool _initialized;

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

    public void Dispose()
    {
        if (_disposed)
            return;

        // Unregister all hotkeys
        if (_windowHandle != IntPtr.Zero)
        {
            foreach (var hotkeyId in _registeredHotkeys.Keys.ToList())
            {
                UnregisterHotKey(_windowHandle, hotkeyId);
            }
            _registeredHotkeys.Clear();

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
