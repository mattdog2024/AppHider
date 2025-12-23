namespace AppHider.Models;

public class ProcessInfo
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public IntPtr MainWindowHandle { get; set; }
    public string ExecutablePath { get; set; } = string.Empty;
}
