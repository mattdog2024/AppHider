using System;
using System.Diagnostics;
using System.IO;

namespace AppHider.Utils;

/// <summary>
/// Simple file logger that writes to both Debug output and a log file
/// </summary>
public static class FileLogger
{
    private static readonly string LogFilePath;
    private static readonly object LockObject = new object();

    static FileLogger()
    {
        try
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AppHider"
            );
            Directory.CreateDirectory(appDataPath);
            
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            LogFilePath = Path.Combine(appDataPath, $"AppHider_{timestamp}.log");
            
            // Write initial log entry
            Log("========================================");
            Log($"AppHider Log Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Log($"Log File: {LogFilePath}");
            Log("========================================");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize FileLogger: {ex.Message}");
            LogFilePath = string.Empty;
        }
    }

    public static void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var logMessage = $"[{timestamp}] {message}";
        
        // Always write to Debug output
        Debug.WriteLine(logMessage);
        
        // Also write to file if available
        if (!string.IsNullOrEmpty(LogFilePath))
        {
            try
            {
                lock (LockObject)
                {
                    File.AppendAllText(LogFilePath, logMessage + Environment.NewLine);
                }
            }
            catch
            {
                // Silently fail if file write fails
            }
        }
    }

    public static string GetLogFilePath()
    {
        return LogFilePath;
    }
}
