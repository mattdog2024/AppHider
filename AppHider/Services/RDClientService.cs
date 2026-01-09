using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using AppHider.Models;
using AppHider.Utils;
using FL = AppHider.Utils.FileLogger;

namespace AppHider.Services;

/// <summary>
/// Service for managing Remote Desktop client processes (outgoing connections)
/// Handles MSTSC process detection, information retrieval, and termination
/// Supports safe mode for testing without affecting real processes
/// </summary>
public class RDClientService : IRDClientService
{
    private bool _isSafeMode;

    /// <summary>
    /// Gets or sets safe mode flag for testing without affecting real processes
    /// </summary>
    public bool IsSafeMode 
    { 
        get => _isSafeMode;
        set 
        {
            _isSafeMode = value;
            FL.Log($"RDClientService: Safe mode {(value ? "enabled" : "disabled")}");
        }
    }

    public RDClientService()
    {
        // Detect safe mode using SafeModeDetector for consistency
        _isSafeMode = SafeModeDetector.DetectRemoteDesktopSafeMode(Environment.GetCommandLineArgs());
        FL.Log($"RDClientService: Initialized with safe mode {(_isSafeMode ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Gets all active MSTSC (Microsoft Terminal Services Client) processes
    /// </summary>
    /// <returns>List of ProcessInfo containing MSTSC process details</returns>
    public async Task<List<ProcessInfo>> GetMSTSCProcessesAsync()
    {
        if (_isSafeMode)
        {
            return await GetMSTSCProcessesSafeModeAsync();
        }

        return await Task.Run(() =>
        {
            var processes = new List<ProcessInfo>();

            try
            {
                FL.Log("RDClientService: Starting MSTSC process detection");

                // Get all processes named "mstsc"
                var mstscProcesses = Process.GetProcessesByName("mstsc");
                
                FL.Log($"RDClientService: Found {mstscProcesses.Length} MSTSC processes");

                foreach (var process in mstscProcesses)
                {
                    try
                    {
                        var processInfo = new ProcessInfo
                        {
                            ProcessId = process.Id,
                            ProcessName = process.ProcessName,
                            MainWindowHandle = process.MainWindowHandle
                        };

                        // Get window title if available
                        if (process.MainWindowHandle != IntPtr.Zero)
                        {
                            processInfo.WindowTitle = process.MainWindowTitle ?? string.Empty;
                        }

                        // Get executable path if accessible
                        try
                        {
                            processInfo.ExecutablePath = process.MainModule?.FileName ?? string.Empty;
                        }
                        catch (Exception ex)
                        {
                            FL.Log($"RDClientService: Could not get executable path for process {process.Id}: {ex.Message}");
                            processInfo.ExecutablePath = string.Empty;
                        }

                        processes.Add(processInfo);
                        FL.Log($"RDClientService: Added MSTSC process {processInfo.ProcessId}: {processInfo.WindowTitle}");
                    }
                    catch (Exception ex)
                    {
                        FL.Log($"RDClientService: Exception processing MSTSC process {process.Id}: {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                FL.Log($"RDClientService: Exception during MSTSC process detection: {ex.Message}");
            }

            FL.Log($"RDClientService: MSTSC process detection completed, returning {processes.Count} processes");
            return processes;
        });
    }

    /// <summary>
    /// Gets detailed information for a specific process
    /// </summary>
    /// <param name="processId">The process ID to query</param>
    /// <returns>ProcessInfo object with detailed process information, or null if failed</returns>
    public async Task<ProcessInfo?> GetProcessInfoAsync(int processId)
    {
        if (_isSafeMode)
        {
            return await GetProcessInfoSafeModeAsync(processId);
        }

        return await Task.Run(() =>
        {
            try
            {
                FL.Log($"RDClientService: Getting detailed info for process {processId}");

                using var process = Process.GetProcessById(processId);
                
                var processInfo = new ProcessInfo
                {
                    ProcessId = process.Id,
                    ProcessName = process.ProcessName,
                    MainWindowHandle = process.MainWindowHandle
                };

                // Get window title
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    processInfo.WindowTitle = process.MainWindowTitle ?? string.Empty;
                }

                // Get executable path
                try
                {
                    processInfo.ExecutablePath = process.MainModule?.FileName ?? string.Empty;
                }
                catch (Exception ex)
                {
                    FL.Log($"RDClientService: Could not get executable path for process {processId}: {ex.Message}");
                    processInfo.ExecutablePath = string.Empty;
                }

                FL.Log($"RDClientService: Retrieved info for process {processId}: {processInfo.ProcessName} - {processInfo.WindowTitle}");
                return processInfo;
            }
            catch (ArgumentException)
            {
                FL.Log($"RDClientService: Process {processId} not found or already terminated");
                return null;
            }
            catch (Exception ex)
            {
                FL.Log($"RDClientService: Exception getting process info for {processId}: {ex.Message}");
                return null;
            }
        });
    }

    /// <summary>
    /// Terminates a specific process using multiple methods with enhanced fallback and retry logic
    /// Requirements 7.3, 7.4: Use alternative methods and forced termination when normal methods fail
    /// </summary>
    /// <param name="processId">The process ID to terminate</param>
    /// <param name="maxRetries">Maximum number of retry attempts per method</param>
    /// <returns>True if successful</returns>
    public async Task<bool> TerminateProcessAsync(int processId, int maxRetries = 2)
    {
        if (_isSafeMode)
        {
            return await TerminateProcessSafeModeAsync(processId);
        }

        var terminationMethods = new List<(string Name, Func<int, Task<bool>> Method)>
        {
            ("Process.Kill", TryProcessKillAsync),
            ("TerminateProcess API", TryTerminateProcessAPIAsync),
            ("Forced Termination", TryForcedTerminationAsync)
        };

        Exception? lastException = null;
        var attemptResults = new List<string>();

        foreach (var (methodName, method) in terminationMethods)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    FL.Log($"RDClientService: Attempting {methodName} (attempt {attempt}/{maxRetries}) for process {processId}");

                    bool success = await method(processId);
                    if (success)
                    {
                        FL.Log($"RDClientService: Successfully terminated process {processId} using {methodName} on attempt {attempt}");
                        return true;
                    }
                    else
                    {
                        string result = $"{methodName} attempt {attempt} failed";
                        attemptResults.Add(result);
                        FL.Log($"RDClientService: {result} for process {processId}");
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    string result = $"{methodName} attempt {attempt} threw exception: {ex.Message}";
                    attemptResults.Add(result);
                    FL.LogDetailedError($"TerminateProcess_{methodName.Replace(" ", "")}_Attempt{attempt}", ex, 
                        $"Process {processId} termination attempt failed");
                }

                // Wait before retry (except for last attempt of last method)
                if (attempt < maxRetries)
                {
                    int delay = CalculateRetryDelay(attempt);
                    await Task.Delay(delay);
                }
            }
        }

        // All methods failed - log comprehensive failure information
        FL.LogDetailedError("TerminateProcessAllMethodsFailed", 
            lastException ?? new InvalidOperationException("All termination methods failed"), 
            $"Failed to terminate process {processId}. Attempts: {string.Join("; ", attemptResults)}");

        return false;
    }

    /// <summary>
    /// Terminates all MSTSC processes
    /// </summary>
    /// <returns>True if all processes were successfully terminated</returns>
    public async Task<bool> TerminateAllMSTSCProcessesAsync()
    {
        if (_isSafeMode)
        {
            return await TerminateAllMSTSCProcessesSafeModeAsync();
        }

        try
        {
            FL.Log("RDClientService: Starting termination of all MSTSC processes");

            var processes = await GetMSTSCProcessesAsync();
            
            if (processes.Count == 0)
            {
                FL.Log("RDClientService: No MSTSC processes found to terminate");
                return true;
            }

            bool allSuccessful = true;
            int successCount = 0;

            // Terminate each process
            foreach (var processInfo in processes)
            {
                bool success = await TerminateProcessAsync(processInfo.ProcessId);
                if (success)
                {
                    successCount++;
                }
                else
                {
                    allSuccessful = false;
                }
            }

            FL.Log($"RDClientService: Terminated {successCount}/{processes.Count} MSTSC processes");
            return allSuccessful;
        }
        catch (Exception ex)
        {
            FL.Log($"RDClientService: Exception terminating all MSTSC processes: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Attempts to terminate a process using Process.Kill() with enhanced error handling
    /// </summary>
    /// <param name="processId">Process ID to terminate</param>
    /// <returns>True if successful</returns>
    private async Task<bool> TryProcessKillAsync(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            
            // Check if process has already exited
            if (process.HasExited)
            {
                FL.Log($"RDClientService: Process {processId} has already exited");
                return true;
            }

            process.Kill();
            
            // Wait with timeout to verify termination
            bool exited = await Task.Run(() => process.WaitForExit(5000));
            
            if (exited)
            {
                FL.Log($"RDClientService: Process {processId} terminated successfully with Process.Kill()");
                return true;
            }
            else
            {
                FL.Log($"RDClientService: Process {processId} did not exit within timeout after Process.Kill()");
                return false;
            }
        }
        catch (ArgumentException)
        {
            // Process not found - consider it successfully terminated
            FL.Log($"RDClientService: Process {processId} not found (already terminated)");
            return true;
        }
        catch (InvalidOperationException ex)
        {
            FL.Log($"RDClientService: Process {processId} cannot be killed: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            FL.LogDetailedError("TryProcessKill", ex, $"Exception killing process {processId}");
            return false;
        }
    }

    /// <summary>
    /// Attempts to terminate a process using TerminateProcess Windows API with enhanced error handling
    /// </summary>
    /// <param name="processId">Process ID to terminate</param>
    /// <returns>True if successful</returns>
    private async Task<bool> TryTerminateProcessAPIAsync(int processId)
    {
        IntPtr processHandle = IntPtr.Zero;
        
        try
        {
            // Open process with terminate access
            processHandle = WindowsAPI.OpenProcess(
                WindowsAPI.ProcessAccessFlags.Terminate,
                false,
                processId);

            if (processHandle == IntPtr.Zero)
            {
                int error = WindowsAPI.GetLastError();
                FL.LogDetailedError("OpenProcessForTermination", 
                    new InvalidOperationException($"Failed to open process {processId}"), 
                    $"OpenProcess failed for process {processId}", error);
                return false;
            }

            // Terminate the process
            bool success = WindowsAPI.TerminateProcess(processHandle, 1);
            
            if (success)
            {
                FL.Log($"RDClientService: Process {processId} terminated successfully with TerminateProcess API");
                
                // Give the process time to terminate and verify
                await Task.Delay(2000);
                
                // Try to verify the process is gone
                try
                {
                    using var checkProcess = Process.GetProcessById(processId);
                    if (checkProcess.HasExited)
                    {
                        FL.Log($"RDClientService: Verified process {processId} has exited");
                        return true;
                    }
                    else
                    {
                        FL.Log($"RDClientService: Process {processId} still running after TerminateProcess");
                        return false;
                    }
                }
                catch (ArgumentException)
                {
                    // Process not found - successfully terminated
                    FL.Log($"RDClientService: Process {processId} not found after termination (success)");
                    return true;
                }
            }
            else
            {
                int error = WindowsAPI.GetLastError();
                FL.LogDetailedError("TerminateProcessAPI", 
                    new InvalidOperationException($"TerminateProcess failed for process {processId}"), 
                    $"TerminateProcess API failed", error);
                return false;
            }
        }
        catch (Exception ex)
        {
            FL.LogDetailedError("TryTerminateProcessAPI", ex, $"Exception using TerminateProcess API for process {processId}");
            return false;
        }
        finally
        {
            // Clean up process handle
            if (processHandle != IntPtr.Zero)
            {
                WindowsAPI.CloseHandle(processHandle);
            }
        }
    }

    /// <summary>
    /// Validates that a process is actually an MSTSC process
    /// </summary>
    /// <param name="processInfo">Process information to validate</param>
    /// <returns>True if the process is a valid MSTSC process</returns>
    private bool ValidateMSTSCProcess(ProcessInfo processInfo)
    {
        try
        {
            // Check process name
            if (!processInfo.ProcessName.Equals("mstsc", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Additional validation: check if executable path contains mstsc
            if (!string.IsNullOrEmpty(processInfo.ExecutablePath))
            {
                string fileName = Path.GetFileNameWithoutExtension(processInfo.ExecutablePath);
                if (!fileName.Equals("mstsc", StringComparison.OrdinalIgnoreCase))
                {
                    FL.Log($"RDClientService: Process {processInfo.ProcessId} executable name mismatch: {fileName}");
                    return false;
                }
            }

            FL.Log($"RDClientService: Process {processInfo.ProcessId} validated as MSTSC process");
            return true;
        }
        catch (Exception ex)
        {
            FL.Log($"RDClientService: Exception validating MSTSC process {processInfo.ProcessId}: {ex.Message}");
            return false;
        }
    }

    #region Safe Mode Simulation Methods

    /// <summary>
    /// Safe mode implementation for MSTSC process detection
    /// Simulates process detection without accessing actual processes
    /// </summary>
    private async Task<List<ProcessInfo>> GetMSTSCProcessesSafeModeAsync()
    {
        FL.Log("[SAFE MODE] Simulating MSTSC process detection");
        
        // Simulate some processing time
        await Task.Delay(200);

        // Generate simulated MSTSC processes for testing
        var simulatedProcesses = new List<ProcessInfo>
        {
            new ProcessInfo
            {
                ProcessId = 1234,
                ProcessName = "mstsc",
                WindowTitle = "Remote Desktop Connection - SERVER01",
                ExecutablePath = @"C:\Windows\System32\mstsc.exe",
                MainWindowHandle = new IntPtr(0x12345)
            },
            new ProcessInfo
            {
                ProcessId = 5678,
                ProcessName = "mstsc",
                WindowTitle = "Remote Desktop Connection - SERVER02",
                ExecutablePath = @"C:\Windows\System32\mstsc.exe",
                MainWindowHandle = new IntPtr(0x56789)
            }
        };

        // Log safe mode operation with detailed parameters
        FL.LogSafeModeOperation("GetMSTSCProcesses", 
            new { RequestedAt = DateTime.Now }, 
            new { ProcessCount = simulatedProcesses.Count, Processes = simulatedProcesses });

        FL.Log($"[SAFE MODE] Simulated {simulatedProcesses.Count} MSTSC processes");
        return simulatedProcesses;
    }

    /// <summary>
    /// Safe mode implementation for process information retrieval
    /// </summary>
    private async Task<ProcessInfo?> GetProcessInfoSafeModeAsync(int processId)
    {
        FL.Log($"[SAFE MODE] Simulating process info retrieval for process {processId}");
        
        // Simulate some processing time
        await Task.Delay(50);

        // Generate simulated process info based on process ID
        var simulatedInfo = new ProcessInfo
        {
            ProcessId = processId,
            ProcessName = "mstsc",
            WindowTitle = $"Remote Desktop Connection - SERVER{processId % 10:D2}",
            ExecutablePath = @"C:\Windows\System32\mstsc.exe",
            MainWindowHandle = new IntPtr(processId * 10)
        };

        // Log safe mode operation
        FL.LogSafeModeOperation("GetProcessInfo", 
            new { ProcessId = processId, RequestedAt = DateTime.Now }, 
            simulatedInfo);

        FL.Log($"[SAFE MODE] Simulated process info for process {processId}: {simulatedInfo.WindowTitle}");
        return simulatedInfo;
    }

    /// <summary>
    /// Safe mode implementation for process termination
    /// </summary>
    private async Task<bool> TerminateProcessSafeModeAsync(int processId)
    {
        FL.Log($"[SAFE MODE] Simulating process termination for process {processId}");
        
        // Simulate operation time
        await Task.Delay(300);

        // Log safe mode operation
        FL.LogSafeModeOperation("TerminateProcess", 
            new { ProcessId = processId, Method = "Process.Kill", RequestedAt = DateTime.Now }, 
            new { Success = true, SimulatedOperation = true });

        FL.Log($"[SAFE MODE] Simulated successful termination for process {processId}");
        return true;
    }

    /// <summary>
    /// Safe mode implementation for terminating all MSTSC processes
    /// </summary>
    private async Task<bool> TerminateAllMSTSCProcessesSafeModeAsync()
    {
        FL.Log("[SAFE MODE] Simulating termination of all MSTSC processes");
        
        // Get simulated processes to show what would be terminated
        var processes = await GetMSTSCProcessesSafeModeAsync();
        
        FL.Log($"[SAFE MODE] Would terminate {processes.Count} MSTSC processes:");
        foreach (var process in processes)
        {
            FL.Log($"[SAFE MODE] - Process {process.ProcessId}: {process.WindowTitle}");
        }

        // Log safe mode operation
        FL.LogSafeModeOperation("TerminateAllMSTSCProcesses", 
            new { ProcessesToTerminate = processes.Count, RequestedAt = DateTime.Now }, 
            new { Success = true, ProcessesTerminated = processes.Count, SimulatedOperation = true });

        // Simulate termination time
        await Task.Delay(1000);

        FL.Log("[SAFE MODE] Simulated termination of all MSTSC processes completed successfully");
        return true;
    }

    #endregion

    #region Enhanced Error Handling and Fallback Methods

    /// <summary>
    /// Attempts forced termination using additional Windows APIs as last resort
    /// Requirement 7.4: Use forced termination when normal process termination fails
    /// </summary>
    /// <param name="processId">Process ID to terminate</param>
    /// <returns>True if successful</returns>
    private async Task<bool> TryForcedTerminationAsync(int processId)
    {
        try
        {
            FL.Log($"RDClientService: Attempting forced termination for process {processId}");

            // Method 1: Try to get process and use Kill with immediate flag
            try
            {
                using var process = Process.GetProcessById(processId);
                if (!process.HasExited)
                {
                    // Force immediate termination (FIXED: removed entireProcessTree to prevent system instability)
                    process.Kill();
                    
                    // Wait longer for forced termination
                    bool exited = await Task.Run(() => process.WaitForExit(10000));
                    if (exited)
                    {
                        FL.Log($"RDClientService: Process {processId} forcibly terminated safely (target process only)");
                        return true;
                    }
                }
                else
                {
                    FL.Log($"RDClientService: Process {processId} already exited during forced termination");
                    return true;
                }
            }
            catch (ArgumentException)
            {
                // Process not found - consider success
                FL.Log($"RDClientService: Process {processId} not found during forced termination (success)");
                return true;
            }

            // Method 2: Try using taskkill command as ultimate fallback
            try
            {
                FL.Log($"RDClientService: Attempting taskkill command for process {processId}");
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/F /PID {processId}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var taskKillProcess = Process.Start(startInfo);
                if (taskKillProcess != null)
                {
                    await taskKillProcess.WaitForExitAsync();
                    
                    if (taskKillProcess.ExitCode == 0)
                    {
                        FL.Log($"RDClientService: Process {processId} terminated using taskkill command");
                        return true;
                    }
                    else
                    {
                        string error = await taskKillProcess.StandardError.ReadToEndAsync();
                        FL.Log($"RDClientService: taskkill failed for process {processId}: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                FL.LogDetailedError("ForcedTermination_TaskKill", ex, $"taskkill command failed for process {processId}");
            }

            FL.Log($"RDClientService: All forced termination methods failed for process {processId}");
            return false;
        }
        catch (Exception ex)
        {
            FL.LogDetailedError("TryForcedTermination", ex, $"Exception during forced termination of process {processId}");
            return false;
        }
    }

    /// <summary>
    /// Calculates retry delay with exponential backoff
    /// </summary>
    /// <param name="attemptNumber">Current attempt number (1-based)</param>
    /// <returns>Delay in milliseconds</returns>
    private int CalculateRetryDelay(int attemptNumber)
    {
        // Exponential backoff: 300ms, 600ms, 1200ms, etc. (shorter for process operations)
        return Math.Min(300 * (int)Math.Pow(2, attemptNumber - 1), 3000);
    }

    /// <summary>
    /// Enhanced MSTSC process detection with fallback mechanisms
    /// Requirement 7.1: Continue operation even if some detection methods fail
    /// </summary>
    public async Task<List<ProcessInfo>> GetMSTSCProcessesWithFallbackAsync()
    {
        if (_isSafeMode)
        {
            return await GetMSTSCProcessesSafeModeAsync();
        }

        var processes = new List<ProcessInfo>();
        Exception? lastException = null;

        try
        {
            // Primary method: Use Process.GetProcessesByName
            processes = await GetMSTSCProcessesAsync();
            
            if (processes.Count > 0)
            {
                FL.Log($"RDClientService: Successfully detected {processes.Count} MSTSC processes using primary method");
                return processes;
            }
        }
        catch (Exception ex)
        {
            lastException = ex;
            FL.LogDetailedError("GetMSTSCProcessesPrimary", ex, "Primary MSTSC detection failed, attempting fallback");
        }

        try
        {
            // Fallback method: Search all processes for mstsc
            FL.Log("RDClientService: Attempting fallback MSTSC process detection");
            
            var allProcesses = Process.GetProcesses();
            foreach (var process in allProcesses)
            {
                try
                {
                    if (process.ProcessName.Equals("mstsc", StringComparison.OrdinalIgnoreCase))
                    {
                        var processInfo = new ProcessInfo
                        {
                            ProcessId = process.Id,
                            ProcessName = process.ProcessName,
                            MainWindowHandle = process.MainWindowHandle,
                            WindowTitle = process.MainWindowTitle ?? string.Empty
                        };

                        // Try to get executable path
                        try
                        {
                            processInfo.ExecutablePath = process.MainModule?.FileName ?? string.Empty;
                        }
                        catch
                        {
                            processInfo.ExecutablePath = string.Empty;
                        }

                        processes.Add(processInfo);
                    }
                }
                catch (Exception ex)
                {
                    // Skip processes we can't access
                    FL.Log($"RDClientService: Could not access process {process.Id}: {ex.Message}");
                }
                finally
                {
                    process.Dispose();
                }
            }
            
            FL.Log($"RDClientService: Fallback detection found {processes.Count} MSTSC processes");
        }
        catch (Exception ex)
        {
            FL.LogDetailedError("GetMSTSCProcessesFallback", ex, "Fallback MSTSC detection also failed");
        }

        if (processes.Count == 0 && lastException != null)
        {
            FL.LogDetailedError("GetMSTSCProcessesComplete", lastException, "All MSTSC detection methods failed");
        }

        return processes;
    }

    /// <summary>
    /// Terminates all MSTSC processes with enhanced error handling and partial success support
    /// Requirement 7.2: Attempt to terminate remaining connections even if some fail
    /// </summary>
    public async Task<(bool AllSuccessful, int SuccessCount, int TotalCount, List<string> Errors)> TerminateAllMSTSCProcessesWithDetailsAsync()
    {
        if (_isSafeMode)
        {
            bool safeResult = await TerminateAllMSTSCProcessesSafeModeAsync();
            return (safeResult, safeResult ? 2 : 0, 2, new List<string>());
        }

        var errors = new List<string>();
        int successCount = 0;
        int totalCount = 0;

        try
        {
            FL.Log("RDClientService: Starting enhanced termination of all MSTSC processes");

            // Use fallback detection to ensure we find all processes
            var processes = await GetMSTSCProcessesWithFallbackAsync();
            totalCount = processes.Count;

            if (totalCount == 0)
            {
                FL.Log("RDClientService: No MSTSC processes found to terminate");
                return (true, 0, 0, errors);
            }

            FL.Log($"RDClientService: Found {totalCount} MSTSC processes to terminate");

            // Terminate each process individually, continuing even if some fail
            var terminationTasks = processes.Select(async processInfo =>
            {
                try
                {
                    bool success = await TerminateProcessAsync(processInfo.ProcessId);
                    if (success)
                    {
                        Interlocked.Increment(ref successCount);
                        FL.Log($"RDClientService: Successfully terminated MSTSC process {processInfo.ProcessId}");
                    }
                    else
                    {
                        string error = $"Failed to terminate MSTSC process {processInfo.ProcessId}";
                        lock (errors)
                        {
                            errors.Add(error);
                        }
                        FL.Log($"RDClientService: {error}");
                    }
                }
                catch (Exception ex)
                {
                    string error = $"Exception terminating MSTSC process {processInfo.ProcessId}: {ex.Message}";
                    lock (errors)
                    {
                        errors.Add(error);
                    }
                    FL.LogDetailedError($"TerminateMSTSCProcess_{processInfo.ProcessId}", ex, 
                        $"Exception during MSTSC process termination");
                }
            });

            // Wait for all termination attempts to complete
            await Task.WhenAll(terminationTasks);

            bool allSuccessful = successCount == totalCount;
            FL.Log($"RDClientService: MSTSC termination completed - {successCount}/{totalCount} successful, {errors.Count} errors");

            return (allSuccessful, successCount, totalCount, errors);
        }
        catch (Exception ex)
        {
            errors.Add($"Critical failure during MSTSC termination: {ex.Message}");
            FL.LogDetailedError("TerminateAllMSTSCProcessesWithDetails", ex, "Critical failure during MSTSC process termination");
            return (false, successCount, totalCount, errors);
        }
    }

    #endregion
}