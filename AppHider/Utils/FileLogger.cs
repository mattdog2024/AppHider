using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using AppHider.Models;

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

    #region Remote Desktop Logging Methods

    /// <summary>
    /// Logs remote desktop connection detection with detailed connection information
    /// Requirement 5.1: Log connection details including session ID, user name, and connection type
    /// </summary>
    /// <param name="connection">The detected RDP connection</param>
    /// <param name="additionalInfo">Optional additional information</param>
    public static void LogConnectionDetected(RDPConnection connection, string additionalInfo = "")
    {
        try
        {
            var logEntry = new
            {
                Operation = "CONNECTION_DETECTED",
                Timestamp = DateTime.Now,
                ConnectionId = connection.Id,
                Type = connection.Type.ToString(),
                SessionId = connection.SessionId,
                ProcessId = connection.ProcessId,
                UserName = connection.UserName,
                ClientName = connection.ClientName,
                ClientAddress = connection.ClientAddress,
                State = connection.State.ToString(),
                ConnectedTime = connection.ConnectedTime,
                AdditionalInfo = additionalInfo
            };

            var jsonLog = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions { WriteIndented = false });
            Log($"RD_DETECTION: {jsonLog}");
        }
        catch (Exception ex)
        {
            Log($"ERROR: Failed to log connection detection: {ex.Message}");
        }
    }

    /// <summary>
    /// Logs remote desktop connection termination results
    /// Requirement 5.2: Log termination result including success/failure status and error messages
    /// </summary>
    /// <param name="connection">The connection being terminated</param>
    /// <param name="success">Whether termination was successful</param>
    /// <param name="method">The termination method used</param>
    /// <param name="errorMessage">Error message if termination failed</param>
    /// <param name="attemptNumber">Attempt number for retry scenarios</param>
    public static void LogConnectionTermination(RDPConnection connection, bool success, string method, string errorMessage = "", int attemptNumber = 1)
    {
        try
        {
            var logEntry = new
            {
                Operation = "CONNECTION_TERMINATION",
                Timestamp = DateTime.Now,
                ConnectionId = connection.Id,
                Type = connection.Type.ToString(),
                SessionId = connection.SessionId,
                ProcessId = connection.ProcessId,
                UserName = connection.UserName,
                Success = success,
                Method = method,
                AttemptNumber = attemptNumber,
                ErrorMessage = errorMessage,
                WindowsErrorCode = success ? 0 : System.Runtime.InteropServices.Marshal.GetLastWin32Error()
            };

            var jsonLog = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions { WriteIndented = false });
            Log($"RD_TERMINATION: {jsonLog}");
        }
        catch (Exception ex)
        {
            Log($"ERROR: Failed to log connection termination: {ex.Message}");
        }
    }

    /// <summary>
    /// Logs the complete emergency disconnect sequence with timestamps
    /// Requirement 5.3: Log complete sequence of operations with timestamps
    /// </summary>
    /// <param name="result">The emergency disconnect result</param>
    /// <param name="startTime">When the emergency disconnect started</param>
    /// <param name="rdStartTime">When remote desktop termination started</param>
    /// <param name="rdEndTime">When remote desktop termination ended</param>
    /// <param name="networkStartTime">When network disconnection started</param>
    /// <param name="networkEndTime">When network disconnection ended</param>
    public static void LogEmergencyDisconnectSequence(EmergencyDisconnectResult result, DateTime startTime, 
        DateTime rdStartTime, DateTime rdEndTime, DateTime networkStartTime, DateTime networkEndTime)
    {
        try
        {
            var logEntry = new
            {
                Operation = "EMERGENCY_DISCONNECT_SEQUENCE",
                Timestamp = DateTime.Now,
                OverallSuccess = result.Success,
                TotalExecutionTime = result.ExecutionTime.TotalMilliseconds,
                
                // Timing information
                SequenceStartTime = startTime,
                RemoteDesktopStartTime = rdStartTime,
                RemoteDesktopEndTime = rdEndTime,
                RemoteDesktopDuration = (rdEndTime - rdStartTime).TotalMilliseconds,
                NetworkStartTime = networkStartTime,
                NetworkEndTime = networkEndTime,
                NetworkDuration = (networkEndTime - networkStartTime).TotalMilliseconds,
                
                // Results
                SessionsTerminated = result.SessionsTerminated,
                ClientsTerminated = result.ClientsTerminated,
                NetworkDisconnected = result.NetworkDisconnected,
                
                // Errors
                ErrorCount = result.Errors.Count,
                Errors = result.Errors
            };

            var jsonLog = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions { WriteIndented = false });
            Log($"EMERGENCY_SEQUENCE: {jsonLog}");
        }
        catch (Exception ex)
        {
            Log($"ERROR: Failed to log emergency disconnect sequence: {ex.Message}");
        }
    }

    /// <summary>
    /// Logs network adapter states before and after network operations
    /// Requirement 5.4: Log network adapter states before and after operation
    /// </summary>
    /// <param name="beforeStates">Network adapter states before operation</param>
    /// <param name="afterStates">Network adapter states after operation</param>
    /// <param name="operation">The network operation performed</param>
    public static void LogNetworkAdapterStates(Dictionary<string, bool> beforeStates, Dictionary<string, bool> afterStates, string operation)
    {
        try
        {
            var logEntry = new
            {
                Operation = "NETWORK_ADAPTER_STATES",
                Timestamp = DateTime.Now,
                NetworkOperation = operation,
                BeforeStates = beforeStates,
                AfterStates = afterStates,
                AdaptersChanged = beforeStates.Where(kvp => afterStates.ContainsKey(kvp.Key) && afterStates[kvp.Key] != kvp.Value)
                    .Select(kvp => new { Adapter = kvp.Key, Before = kvp.Value, After = afterStates[kvp.Key] })
                    .ToList()
            };

            var jsonLog = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions { WriteIndented = false });
            Log($"NETWORK_STATES: {jsonLog}");
        }
        catch (Exception ex)
        {
            Log($"ERROR: Failed to log network adapter states: {ex.Message}");
        }
    }

    /// <summary>
    /// Logs detailed error information including Windows error codes and exception details
    /// Requirement 5.5: Log detailed error information including Windows error codes and exception details
    /// </summary>
    /// <param name="operation">The operation that failed</param>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="additionalContext">Additional context information</param>
    /// <param name="windowsErrorCode">Windows error code if available</param>
    public static void LogDetailedError(string operation, Exception exception, string additionalContext = "", int? windowsErrorCode = null)
    {
        try
        {
            var logEntry = new
            {
                Operation = "DETAILED_ERROR",
                Timestamp = DateTime.Now,
                FailedOperation = operation,
                ExceptionType = exception.GetType().Name,
                ExceptionMessage = exception.Message,
                StackTrace = exception.StackTrace,
                InnerException = exception.InnerException?.Message,
                WindowsErrorCode = windowsErrorCode ?? System.Runtime.InteropServices.Marshal.GetLastWin32Error(),
                AdditionalContext = additionalContext,
                ProcessId = Environment.ProcessId,
                ThreadId = Environment.CurrentManagedThreadId
            };

            var jsonLog = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions { WriteIndented = false });
            Log($"DETAILED_ERROR: {jsonLog}");
        }
        catch (Exception ex)
        {
            // Fallback to simple logging if JSON serialization fails
            Log($"CRITICAL_ERROR: Failed to log detailed error for operation '{operation}': {ex.Message}");
            Log($"ORIGINAL_ERROR: {exception.Message}");
        }
    }

    /// <summary>
    /// Logs safe mode operations with clear indication that they are simulated
    /// </summary>
    /// <param name="operation">The operation being simulated</param>
    /// <param name="parameters">Parameters for the simulated operation</param>
    /// <param name="simulatedResult">The simulated result</param>
    public static void LogSafeModeOperation(string operation, object parameters, object simulatedResult)
    {
        try
        {
            var logEntry = new
            {
                Operation = "SAFE_MODE_SIMULATION",
                Timestamp = DateTime.Now,
                SimulatedOperation = operation,
                Parameters = parameters,
                SimulatedResult = simulatedResult,
                ActualExecution = false,
                SafeModeActive = true
            };

            var jsonLog = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions { WriteIndented = false });
            Log($"SAFE_MODE: {jsonLog}");
        }
        catch (Exception ex)
        {
            Log($"ERROR: Failed to log safe mode operation: {ex.Message}");
        }
    }

    /// <summary>
    /// Logs performance metrics for remote desktop operations
    /// </summary>
    /// <param name="operation">The operation being measured</param>
    /// <param name="duration">Duration of the operation</param>
    /// <param name="itemCount">Number of items processed</param>
    /// <param name="successCount">Number of successful operations</param>
    /// <param name="failureCount">Number of failed operations</param>
    public static void LogPerformanceMetrics(string operation, TimeSpan duration, int itemCount, int successCount, int failureCount)
    {
        try
        {
            var logEntry = new
            {
                Operation = "PERFORMANCE_METRICS",
                Timestamp = DateTime.Now,
                MeasuredOperation = operation,
                DurationMs = duration.TotalMilliseconds,
                ItemCount = itemCount,
                SuccessCount = successCount,
                FailureCount = failureCount,
                SuccessRate = itemCount > 0 ? (double)successCount / itemCount * 100 : 0,
                AverageTimePerItem = itemCount > 0 ? duration.TotalMilliseconds / itemCount : 0
            };

            var jsonLog = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions { WriteIndented = false });
            Log($"PERFORMANCE: {jsonLog}");
        }
        catch (Exception ex)
        {
            Log($"ERROR: Failed to log performance metrics: {ex.Message}");
        }
    }

    #endregion
}
