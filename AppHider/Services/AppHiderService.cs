using System.Diagnostics;
using System.Runtime.InteropServices;
using AppHider.Models;

namespace AppHider.Services;

public class AppHiderService : IAppHiderService
{
    // Win32 API imports
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    // Constants
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;
    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    // Structure to store window placement
    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    // Storage for original window states
    private class WindowState
    {
        public IntPtr Handle { get; set; }
        public int OriginalExStyle { get; set; }
        public WINDOWPLACEMENT OriginalPlacement { get; set; }
        public bool WasVisible { get; set; }
    }

    private readonly Dictionary<int, WindowState> _hiddenWindows = new();
    private readonly object _lockObject = new();

    public IReadOnlyList<ProcessInfo> GetRunningApplications()
    {
        var applications = new List<ProcessInfo>();

        try
        {
            var processes = Process.GetProcesses();

            foreach (var process in processes)
            {
                try
                {
                    // Only include processes with a main window
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        var processInfo = new ProcessInfo
                        {
                            ProcessId = process.Id,
                            ProcessName = process.ProcessName,
                            WindowTitle = process.MainWindowTitle,
                            MainWindowHandle = process.MainWindowHandle,
                            ExecutablePath = GetProcessPath(process)
                        };

                        applications.Add(processInfo);
                    }
                }
                catch
                {
                    // Skip processes we can't access
                    continue;
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but return what we have
            Console.WriteLine($"Error getting running applications: {ex.Message}");
        }

        return applications;
    }

    public Task HideApplicationsAsync(IEnumerable<int> processIds)
    {
        return Task.Run(() =>
        {
            lock (_lockObject)
            {
                foreach (var processId in processIds)
                {
                    try
                    {
                        var process = Process.GetProcessById(processId);
                        var handle = process.MainWindowHandle;

                        if (handle == IntPtr.Zero)
                        {
                            process.Dispose();
                            continue;
                        }

                        // Store original state if not already hidden
                        if (!_hiddenWindows.ContainsKey(processId))
                        {
                            var windowState = new WindowState
                            {
                                Handle = handle,
                                OriginalExStyle = GetWindowLong(handle, GWL_EXSTYLE),
                                WasVisible = IsWindowVisible(handle)
                            };

                            // Get original window placement
                            var placement = new WINDOWPLACEMENT();
                            placement.length = Marshal.SizeOf(placement);
                            GetWindowPlacement(handle, ref placement);
                            windowState.OriginalPlacement = placement;

                            _hiddenWindows[processId] = windowState;
                        }

                        // Remove from Alt+Tab by adding WS_EX_TOOLWINDOW and removing WS_EX_APPWINDOW
                        var currentExStyle = GetWindowLong(handle, GWL_EXSTYLE);
                        var newExStyle = (currentExStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
                        SetWindowLong(handle, GWL_EXSTYLE, newExStyle);

                        // Hide the window
                        ShowWindow(handle, SW_HIDE);

                        process.Dispose();
                    }
                    catch (ArgumentException)
                    {
                        // Process no longer exists
                        continue;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error hiding process {processId}: {ex.Message}");
                    }
                }
            }
        });
    }

    public Task ShowApplicationsAsync(IEnumerable<int> processIds)
    {
        return Task.Run(() =>
        {
            lock (_lockObject)
            {
                foreach (var processId in processIds)
                {
                    try
                    {
                        if (!_hiddenWindows.TryGetValue(processId, out var windowState))
                        {
                            continue;
                        }

                        // Verify the process still exists
                        var process = Process.GetProcessById(processId);
                        var handle = process.MainWindowHandle;

                        // If handle changed, use the stored one
                        if (handle == IntPtr.Zero)
                        {
                            handle = windowState.Handle;
                        }

                        // Restore original window style
                        SetWindowLong(handle, GWL_EXSTYLE, windowState.OriginalExStyle);

                        // Restore original window placement
                        var placement = windowState.OriginalPlacement;
                        SetWindowPlacement(handle, ref placement);

                        // Show the window if it was visible before
                        if (windowState.WasVisible)
                        {
                            ShowWindow(handle, SW_SHOW);
                        }

                        // Remove from hidden windows tracking
                        _hiddenWindows.Remove(processId);

                        process.Dispose();
                    }
                    catch (ArgumentException)
                    {
                        // Process no longer exists, remove from tracking
                        _hiddenWindows.Remove(processId);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error showing process {processId}: {ex.Message}");
                    }
                }
            }
        });
    }

    public Task<bool> IsProcessHidden(int processId)
    {
        lock (_lockObject)
        {
            return Task.FromResult(_hiddenWindows.ContainsKey(processId));
        }
    }

    private static string GetProcessPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
