using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace AppHider.Services;

/// <summary>
/// Service for managing application auto-startup using Windows Task Scheduler.
/// Uses a non-descriptive task name for stealth as per requirements 8.1 and 8.3.
/// </summary>
public class AutoStartupService : IAutoStartupService
{
    // Non-descriptive task name to avoid easy identification (Requirement 8.3)
    private const string TaskName = "SystemMaintenanceTask";
    private const string TaskPath = "\\Microsoft\\Windows\\";
    private readonly string _executablePath;

    public AutoStartupService()
    {
        // Get the path to the current executable
        _executablePath = Process.GetCurrentProcess().MainModule?.FileName 
            ?? throw new InvalidOperationException("Could not determine executable path");
    }

    /// <summary>
    /// Registers the application to start automatically at Windows startup with highest privileges.
    /// </summary>
    public async Task<bool> RegisterAutoStartupAsync()
    {
        try
        {
            // Check if we're running as administrator
            if (!IsRunningAsAdministrator())
            {
                Debug.WriteLine("Auto-startup registration requires administrator privileges.");
                return false;
            }

            // Create the scheduled task using schtasks command
            // /Create: Creates a new scheduled task
            // /TN: Task name (non-descriptive for stealth)
            // /TR: Task run command (executable path with --background flag)
            // /SC: Schedule type (ONLOGON = at user logon)
            // /RL: Run level (HIGHEST = run with highest privileges)
            // /F: Force creation (overwrite if exists)
            var arguments = $"/Create /TN \"{TaskPath}{TaskName}\" " +
                          $"/TR \"\\\"{_executablePath}\\\" --background\" " +
                          $"/SC ONLOGON " +
                          $"/RL HIGHEST " +
                          $"/F";

            var result = await ExecuteSchTasksCommandAsync(arguments);
            
            if (result.Success)
            {
                Debug.WriteLine($"Auto-startup registered successfully: {TaskPath}{TaskName}");
                return true;
            }
            else
            {
                Debug.WriteLine($"Failed to register auto-startup: {result.Output}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error registering auto-startup: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Unregisters the application from auto-startup.
    /// </summary>
    public async Task<bool> UnregisterAutoStartupAsync()
    {
        try
        {
            // Check if we're running as administrator
            if (!IsRunningAsAdministrator())
            {
                Debug.WriteLine("Auto-startup unregistration requires administrator privileges.");
                return false;
            }

            // Delete the scheduled task
            // /Delete: Deletes a scheduled task
            // /TN: Task name
            // /F: Force deletion without confirmation
            var arguments = $"/Delete /TN \"{TaskPath}{TaskName}\" /F";

            var result = await ExecuteSchTasksCommandAsync(arguments);
            
            if (result.Success)
            {
                Debug.WriteLine($"Auto-startup unregistered successfully: {TaskPath}{TaskName}");
                return true;
            }
            else
            {
                Debug.WriteLine($"Failed to unregister auto-startup: {result.Output}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error unregistering auto-startup: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks if auto-startup is currently registered.
    /// </summary>
    public async Task<bool> IsAutoStartupRegisteredAsync()
    {
        try
        {
            // Query the scheduled task
            // /Query: Displays scheduled tasks
            // /TN: Task name
            var arguments = $"/Query /TN \"{TaskPath}{TaskName}\"";

            var result = await ExecuteSchTasksCommandAsync(arguments);
            
            // If the query succeeds, the task exists
            return result.Success;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking auto-startup status: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Executes a schtasks command and returns the result.
    /// </summary>
    private async Task<CommandResult> ExecuteSchTasksCommandAsync(string arguments)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = processStartInfo };
        
        process.Start();
        
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        
        await process.WaitForExitAsync();

        var success = process.ExitCode == 0;
        var resultOutput = success ? output : error;

        return new CommandResult
        {
            Success = success,
            Output = resultOutput,
            ExitCode = process.ExitCode
        };
    }

    /// <summary>
    /// Checks if the current process is running with administrator privileges.
    /// </summary>
    private bool IsRunningAsAdministrator()
    {
        try
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Result of a command execution.
    /// </summary>
    private class CommandResult
    {
        public bool Success { get; set; }
        public string Output { get; set; } = string.Empty;
        public int ExitCode { get; set; }
    }
}
