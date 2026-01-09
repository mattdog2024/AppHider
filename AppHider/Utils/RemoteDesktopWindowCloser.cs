using System.Runtime.InteropServices;
using System.Text;
using FL = AppHider.Utils.FileLogger;

namespace AppHider.Utils;

/// <summary>
/// Simple utility to close Remote Desktop Connection dialog windows
/// Uses only FindWindow and SendMessage APIs - no session management or process termination
/// </summary>
public static class RemoteDesktopWindowCloser
{
    private const int WM_CLOSE = 0x0010;
    
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    
    /// <summary>
    /// Closes all windows with titles containing "远程桌面连接" or "Remote Desktop Connection"
    /// This is a simple, safe operation that only closes windows - no session management
    /// </summary>
    public static void CloseRemoteDesktopWindows()
    {
        try
        {
            FL.Log("[RD_WINDOW] Starting remote desktop window close operation");
            var windowsToClose = new List<IntPtr>();
            
            // Enumerate all windows to find remote desktop connection dialogs
            EnumWindows((hWnd, lParam) =>
            {
                try
                {
                    var sb = new StringBuilder(256);
                    int length = GetWindowText(hWnd, sb, sb.Capacity);
                    
                    if (length > 0)
                    {
                        string title = sb.ToString();
                        
                        // Check if window title contains remote desktop keywords
                        if (title.Contains("远程桌面连接") || 
                            title.Contains("Remote Desktop Connection") ||
                            title.Contains("远程桌面"))
                        {
                            FL.Log($"[RD_WINDOW] Found remote desktop window: '{title}' (Handle: {hWnd})");
                            windowsToClose.Add(hWnd);
                        }
                    }
                }
                catch (Exception ex)
                {
                    FL.Log($"[RD_WINDOW] Error checking window: {ex.Message}");
                }
                
                return true; // Continue enumeration
            }, IntPtr.Zero);
            
            // Close all found windows
            FL.Log($"[RD_WINDOW] Found {windowsToClose.Count} remote desktop windows to close");
            
            foreach (var hWnd in windowsToClose)
            {
                try
                {
                    SendMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    FL.Log($"[RD_WINDOW] Sent WM_CLOSE to window handle {hWnd}");
                }
                catch (Exception ex)
                {
                    FL.Log($"[RD_WINDOW] Error closing window {hWnd}: {ex.Message}");
                }
            }
            
            FL.Log("[RD_WINDOW] Remote desktop window close operation completed");
        }
        catch (Exception ex)
        {
            // Log error but don't throw - this should never prevent network disconnect
            FL.Log($"[RD_WINDOW] Error in CloseRemoteDesktopWindows: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Error closing remote desktop windows: {ex.Message}");
        }
    }
}
