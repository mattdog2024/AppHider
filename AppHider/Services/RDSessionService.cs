using System.Runtime.InteropServices;
using AppHider.Models;
using AppHider.Utils;
using FL = AppHider.Utils.FileLogger;

namespace AppHider.Services;

/// <summary>
/// Service for managing Remote Desktop sessions (incoming connections)
/// Handles WTS API calls for session enumeration, information retrieval, and termination
/// Supports safe mode for testing without affecting real connections
/// </summary>
public class RDSessionService : IRDSessionService
{
    private bool _isSafeMode;

    /// <summary>
    /// Gets or sets safe mode flag for testing without affecting real sessions
    /// </summary>
    public bool IsSafeMode 
    { 
        get => _isSafeMode;
        set 
        {
            _isSafeMode = value;
            FL.Log($"RDSessionService: Safe mode {(value ? "enabled" : "disabled")}");
        }
    }

    public RDSessionService()
    {
        // Detect safe mode using SafeModeDetector for consistency
        _isSafeMode = SafeModeDetector.DetectRemoteDesktopSafeMode(Environment.GetCommandLineArgs());
        FL.Log($"RDSessionService: Initialized with safe mode {(_isSafeMode ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Enumerates all active Windows Terminal Services sessions
    /// </summary>
    /// <returns>List of WTSSessionInfo containing session details</returns>
    public async Task<List<WTSSessionInfo>> EnumerateSessionsAsync()
    {
        if (_isSafeMode)
        {
            return await EnumerateSessionsSafeModeAsync();
        }

        return await Task.Run(() =>
        {
            var sessions = new List<WTSSessionInfo>();
            IntPtr sessionInfoPtr = IntPtr.Zero;
            int sessionCount = 0;

            try
            {
                FL.Log("RDSessionService: Starting session enumeration");

                // Call WTSEnumerateSessions to get all sessions
                bool success = WindowsAPI.WTSEnumerateSessions(
                    WindowsAPI.WTS_CURRENT_SERVER_HANDLE,
                    0, // Reserved
                    1, // Version
                    ref sessionInfoPtr,
                    ref sessionCount);

                if (!success)
                {
                    int error = WindowsAPI.GetLastError();
                    FL.Log($"RDSessionService: WTSEnumerateSessions failed with error {error}");
                    return sessions;
                }

                FL.Log($"RDSessionService: Found {sessionCount} sessions");

                // Parse the session information
                int structSize = Marshal.SizeOf<WindowsAPI.WTS_SESSION_INFO>();
                IntPtr currentPtr = sessionInfoPtr;

                for (int i = 0; i < sessionCount; i++)
                {
                    var sessionInfo = Marshal.PtrToStructure<WindowsAPI.WTS_SESSION_INFO>(currentPtr);
                    
                    var wtsSession = new WTSSessionInfo
                    {
                        SessionId = sessionInfo.SessionId,
                        WinStationName = sessionInfo.pWinStationName ?? string.Empty,
                        State = sessionInfo.State
                    };

                    sessions.Add(wtsSession);
                    FL.Log($"RDSessionService: Session {wtsSession.SessionId}: {wtsSession.WinStationName} ({wtsSession.State})");

                    currentPtr = IntPtr.Add(currentPtr, structSize);
                }
            }
            catch (Exception ex)
            {
                FL.Log($"RDSessionService: Exception during session enumeration: {ex.Message}");
            }
            finally
            {
                // Free the allocated memory
                if (sessionInfoPtr != IntPtr.Zero)
                {
                    WindowsAPI.WTSFreeMemory(sessionInfoPtr);
                }
            }

            FL.Log($"RDSessionService: Session enumeration completed, returning {sessions.Count} sessions");
            return sessions;
        });
    }

    /// <summary>
    /// Retrieves detailed information for a specific session
    /// </summary>
    /// <param name="sessionId">The session ID to query</param>
    /// <returns>SessionInfo object with detailed session information, or null if failed</returns>
    public async Task<SessionInfo?> GetSessionInfoAsync(int sessionId)
    {
        if (_isSafeMode)
        {
            return await GetSessionInfoSafeModeAsync(sessionId);
        }

        return await Task.Run(() =>
        {
            try
            {
                FL.Log($"RDSessionService: Getting detailed info for session {sessionId}");

                var sessionInfo = new SessionInfo
                {
                    SessionId = sessionId
                };

                // Get user name
                if (QuerySessionInformation(sessionId, WindowsAPI.WTSInfoClass.WTSUserName, out string userName))
                {
                    sessionInfo.UserName = userName;
                }

                // Get client name
                if (QuerySessionInformation(sessionId, WindowsAPI.WTSInfoClass.WTSClientName, out string clientName))
                {
                    sessionInfo.ClientName = clientName;
                }

                // Get workstation name
                if (QuerySessionInformation(sessionId, WindowsAPI.WTSInfoClass.WTSWinStationName, out string winStationName))
                {
                    sessionInfo.WinStationName = winStationName;
                }

                // Get client address
                if (QuerySessionInformation(sessionId, WindowsAPI.WTSInfoClass.WTSClientAddress, out string clientAddress))
                {
                    sessionInfo.ClientAddress = clientAddress;
                }

                // Get connection state
                if (QuerySessionInformation(sessionId, WindowsAPI.WTSInfoClass.WTSConnectState, out int connectState))
                {
                    sessionInfo.State = (WTSConnectState)connectState;
                }

                // Get logon time
                if (QuerySessionInformation(sessionId, WindowsAPI.WTSInfoClass.WTSLogonTime, out long logonTime))
                {
                    try
                    {
                        sessionInfo.ConnectedTime = DateTime.FromFileTime(logonTime);
                    }
                    catch
                    {
                        sessionInfo.ConnectedTime = DateTime.Now;
                    }
                }
                else
                {
                    sessionInfo.ConnectedTime = DateTime.Now;
                }

                FL.Log($"RDSessionService: Retrieved info for session {sessionId}: User={sessionInfo.UserName}, Client={sessionInfo.ClientName}, State={sessionInfo.State}");
                return sessionInfo;
            }
            catch (Exception ex)
            {
                FL.Log($"RDSessionService: Exception getting session info for {sessionId}: {ex.Message}");
                return null;
            }
        });
    }

    /// <summary>
    /// Determines if a session represents a remote desktop connection
    /// </summary>
    /// <param name="session">The session to check</param>
    /// <returns>True if the session is a remote desktop connection</returns>
    public bool IsRemoteSession(WTSSessionInfo session)
    {
        try
        {
            // Console session (session 0) is always local
            if (session.SessionId == 0)
            {
                FL.Log($"RDSessionService: Session {session.SessionId} is console session (local)");
                return false;
            }

            // Check if session is active or connected (remote sessions)
            bool isRemote = session.State == WTSConnectState.Active || 
                           session.State == WTSConnectState.Connected;

            // Additional check: console sessions typically have "Console" in the name
            if (session.WinStationName?.ToLower().Contains("console") == true)
            {
                FL.Log($"RDSessionService: Session {session.SessionId} contains 'console' in name (local)");
                return false;
            }

            // RDP sessions typically have names like "RDP-Tcp#0", "RDP-Tcp#1", etc.
            bool hasRdpName = session.WinStationName?.ToLower().Contains("rdp") == true;

            bool result = isRemote && (hasRdpName || session.SessionId > 0);
            FL.Log($"RDSessionService: Session {session.SessionId} ({session.WinStationName}) is {(result ? "remote" : "local")} - State: {session.State}");
            
            return result;
        }
        catch (Exception ex)
        {
            FL.Log($"RDSessionService: Exception checking if session {session.SessionId} is remote: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Logs off a session using WTSLogoffSession with enhanced error handling and retry logic
    /// Requirement 7.3: Use alternative methods when Windows API calls fail
    /// </summary>
    /// <param name="sessionId">The session ID to log off</param>
    /// <param name="maxRetries">Maximum number of retry attempts</param>
    /// <returns>True if successful</returns>
    public async Task<bool> LogoffSessionAsync(int sessionId, int maxRetries = 3)
    {
        if (_isSafeMode)
        {
            return await LogoffSessionSafeModeAsync(sessionId);
        }

        return await Task.Run(async () =>
        {
            Exception? lastException = null;
            int lastWindowsError = 0;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    FL.Log($"RDSessionService: Logoff attempt {attempt}/{maxRetries} for session {sessionId}");

                    bool success = WindowsAPI.WTSLogoffSession(
                        WindowsAPI.WTS_CURRENT_SERVER_HANDLE,
                        sessionId,
                        false); // Don't wait for response

                    if (success)
                    {
                        FL.Log($"RDSessionService: Successfully logged off session {sessionId} on attempt {attempt}");
                        return true;
                    }
                    else
                    {
                        lastWindowsError = WindowsAPI.GetLastError();
                        FL.Log($"RDSessionService: Logoff attempt {attempt} failed for session {sessionId}, Windows error: {lastWindowsError}");

                        // Check for specific error conditions that indicate we should not retry
                        if (IsNonRetryableError(lastWindowsError))
                        {
                            FL.Log($"RDSessionService: Non-retryable error {lastWindowsError} for session {sessionId}, stopping attempts");
                            break;
                        }

                        // Wait before retry with exponential backoff
                        if (attempt < maxRetries)
                        {
                            int delay = CalculateRetryDelay(attempt);
                            FL.Log($"RDSessionService: Waiting {delay}ms before retry for session {sessionId}");
                            await Task.Delay(delay);
                        }
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    FL.LogDetailedError($"LogoffSession_Attempt{attempt}", ex, $"Session {sessionId} logoff attempt {attempt} failed", lastWindowsError);
                    
                    // Wait before retry
                    if (attempt < maxRetries)
                    {
                        int delay = CalculateRetryDelay(attempt);
                        await Task.Delay(delay);
                    }
                }
            }

            // All attempts failed - log comprehensive error information
            FL.LogDetailedError("LogoffSessionFinalFailure", 
                lastException ?? new InvalidOperationException($"WTSLogoffSession failed with error {lastWindowsError}"), 
                $"Failed to log off session {sessionId} after {maxRetries} attempts", 
                lastWindowsError);

            return false;
        });
    }

    /// <summary>
    /// Disconnects a session using WTSDisconnectSession with enhanced error handling and retry logic
    /// Requirement 7.3: Use alternative methods when Windows API calls fail
    /// </summary>
    /// <param name="sessionId">The session ID to disconnect</param>
    /// <param name="maxRetries">Maximum number of retry attempts</param>
    /// <returns>True if successful</returns>
    public async Task<bool> DisconnectSessionAsync(int sessionId, int maxRetries = 3)
    {
        if (_isSafeMode)
        {
            return await DisconnectSessionSafeModeAsync(sessionId);
        }

        return await Task.Run(async () =>
        {
            Exception? lastException = null;
            int lastWindowsError = 0;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    FL.Log($"RDSessionService: Disconnect attempt {attempt}/{maxRetries} for session {sessionId}");

                    bool success = WindowsAPI.WTSDisconnectSession(
                        WindowsAPI.WTS_CURRENT_SERVER_HANDLE,
                        sessionId,
                        false); // Don't wait for response

                    if (success)
                    {
                        FL.Log($"RDSessionService: Successfully disconnected session {sessionId} on attempt {attempt}");
                        return true;
                    }
                    else
                    {
                        lastWindowsError = WindowsAPI.GetLastError();
                        FL.Log($"RDSessionService: Disconnect attempt {attempt} failed for session {sessionId}, Windows error: {lastWindowsError}");

                        // Check for specific error conditions that indicate we should not retry
                        if (IsNonRetryableError(lastWindowsError))
                        {
                            FL.Log($"RDSessionService: Non-retryable error {lastWindowsError} for session {sessionId}, stopping attempts");
                            break;
                        }

                        // Wait before retry with exponential backoff
                        if (attempt < maxRetries)
                        {
                            int delay = CalculateRetryDelay(attempt);
                            FL.Log($"RDSessionService: Waiting {delay}ms before retry for session {sessionId}");
                            await Task.Delay(delay);
                        }
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    FL.LogDetailedError($"DisconnectSession_Attempt{attempt}", ex, $"Session {sessionId} disconnect attempt {attempt} failed", lastWindowsError);
                    
                    // Wait before retry
                    if (attempt < maxRetries)
                    {
                        int delay = CalculateRetryDelay(attempt);
                        await Task.Delay(delay);
                    }
                }
            }

            // All attempts failed - log comprehensive error information
            FL.LogDetailedError("DisconnectSessionFinalFailure", 
                lastException ?? new InvalidOperationException($"WTSDisconnectSession failed with error {lastWindowsError}"), 
                $"Failed to disconnect session {sessionId} after {maxRetries} attempts", 
                lastWindowsError);

            return false;
        });
    }

    /// <summary>
    /// Helper method to query session information using WTSQuerySessionInformation
    /// </summary>
    /// <param name="sessionId">Session ID to query</param>
    /// <param name="infoClass">Type of information to retrieve</param>
    /// <param name="result">Output result as string</param>
    /// <returns>True if successful</returns>
    private bool QuerySessionInformation(int sessionId, WindowsAPI.WTSInfoClass infoClass, out string result)
    {
        result = string.Empty;
        IntPtr buffer = IntPtr.Zero;
        int bytesReturned = 0;

        try
        {
            bool success = WindowsAPI.WTSQuerySessionInformation(
                WindowsAPI.WTS_CURRENT_SERVER_HANDLE,
                sessionId,
                infoClass,
                out buffer,
                out bytesReturned);

            if (success && buffer != IntPtr.Zero)
            {
                result = Marshal.PtrToStringAuto(buffer) ?? string.Empty;
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            FL.Log($"RDSessionService: Exception querying session info {infoClass} for session {sessionId}: {ex.Message}");
            return false;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                WindowsAPI.WTSFreeMemory(buffer);
            }
        }
    }

    /// <summary>
    /// Helper method to query session information as integer
    /// </summary>
    /// <param name="sessionId">Session ID to query</param>
    /// <param name="infoClass">Type of information to retrieve</param>
    /// <param name="result">Output result as integer</param>
    /// <returns>True if successful</returns>
    private bool QuerySessionInformation(int sessionId, WindowsAPI.WTSInfoClass infoClass, out int result)
    {
        result = 0;
        IntPtr buffer = IntPtr.Zero;
        int bytesReturned = 0;

        try
        {
            bool success = WindowsAPI.WTSQuerySessionInformation(
                WindowsAPI.WTS_CURRENT_SERVER_HANDLE,
                sessionId,
                infoClass,
                out buffer,
                out bytesReturned);

            if (success && buffer != IntPtr.Zero && bytesReturned >= sizeof(int))
            {
                result = Marshal.ReadInt32(buffer);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            FL.Log($"RDSessionService: Exception querying session info {infoClass} for session {sessionId}: {ex.Message}");
            return false;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                WindowsAPI.WTSFreeMemory(buffer);
            }
        }
    }

    /// <summary>
    /// Helper method to query session information as long (for timestamps)
    /// </summary>
    /// <param name="sessionId">Session ID to query</param>
    /// <param name="infoClass">Type of information to retrieve</param>
    /// <param name="result">Output result as long</param>
    /// <returns>True if successful</returns>
    private bool QuerySessionInformation(int sessionId, WindowsAPI.WTSInfoClass infoClass, out long result)
    {
        result = 0;
        IntPtr buffer = IntPtr.Zero;
        int bytesReturned = 0;

        try
        {
            bool success = WindowsAPI.WTSQuerySessionInformation(
                WindowsAPI.WTS_CURRENT_SERVER_HANDLE,
                sessionId,
                infoClass,
                out buffer,
                out bytesReturned);

            if (success && buffer != IntPtr.Zero && bytesReturned >= sizeof(long))
            {
                result = Marshal.ReadInt64(buffer);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            FL.Log($"RDSessionService: Exception querying session info {infoClass} for session {sessionId}: {ex.Message}");
            return false;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                WindowsAPI.WTSFreeMemory(buffer);
            }
        }
    }

    #region Safe Mode Simulation Methods

    /// <summary>
    /// Safe mode implementation for session enumeration
    /// Simulates session detection without accessing actual Terminal Services
    /// </summary>
    private async Task<List<WTSSessionInfo>> EnumerateSessionsSafeModeAsync()
    {
        FL.Log("[SAFE MODE] Simulating session enumeration");
        
        // Simulate some processing time
        await Task.Delay(300);

        // Generate simulated sessions for testing
        var simulatedSessions = new List<WTSSessionInfo>
        {
            new WTSSessionInfo
            {
                SessionId = 0,
                WinStationName = "Console",
                State = WTSConnectState.Active
            },
            new WTSSessionInfo
            {
                SessionId = 2,
                WinStationName = "RDP-Tcp#0",
                State = WTSConnectState.Active
            },
            new WTSSessionInfo
            {
                SessionId = 3,
                WinStationName = "RDP-Tcp#1",
                State = WTSConnectState.Connected
            }
        };

        // Log safe mode operation with detailed parameters
        FL.LogSafeModeOperation("EnumerateSessions", 
            new { RequestedAt = DateTime.Now }, 
            new { SessionCount = simulatedSessions.Count, Sessions = simulatedSessions });

        FL.Log($"[SAFE MODE] Simulated {simulatedSessions.Count} sessions");
        return simulatedSessions;
    }

    /// <summary>
    /// Safe mode implementation for session information retrieval
    /// </summary>
    private async Task<SessionInfo?> GetSessionInfoSafeModeAsync(int sessionId)
    {
        FL.Log($"[SAFE MODE] Simulating session info retrieval for session {sessionId}");
        
        // Simulate some processing time
        await Task.Delay(100);

        // Generate simulated session info based on session ID
        var simulatedInfo = new SessionInfo
        {
            SessionId = sessionId,
            UserName = sessionId == 0 ? "LocalUser" : $"RemoteUser{sessionId}",
            ClientName = sessionId == 0 ? "Console" : $"CLIENT-PC-{sessionId:D2}",
            WinStationName = sessionId == 0 ? "Console" : $"RDP-Tcp#{sessionId - 1}",
            ClientAddress = sessionId == 0 ? "127.0.0.1" : $"192.168.1.{100 + sessionId}",
            State = WTSConnectState.Active,
            ConnectedTime = DateTime.Now.AddMinutes(-30 * sessionId)
        };

        // Log safe mode operation
        FL.LogSafeModeOperation("GetSessionInfo", 
            new { SessionId = sessionId, RequestedAt = DateTime.Now }, 
            simulatedInfo);

        FL.Log($"[SAFE MODE] Simulated session info for session {sessionId}: User={simulatedInfo.UserName}");
        return simulatedInfo;
    }

    /// <summary>
    /// Safe mode implementation for session logoff
    /// </summary>
    private async Task<bool> LogoffSessionSafeModeAsync(int sessionId)
    {
        FL.Log($"[SAFE MODE] Simulating session logoff for session {sessionId}");
        
        // Simulate operation time
        await Task.Delay(500);

        // Log safe mode operation
        FL.LogSafeModeOperation("LogoffSession", 
            new { SessionId = sessionId, Method = "WTSLogoffSession", RequestedAt = DateTime.Now }, 
            new { Success = true, SimulatedOperation = true });

        FL.Log($"[SAFE MODE] Simulated successful logoff for session {sessionId}");
        return true;
    }

    /// <summary>
    /// Safe mode implementation for session disconnect
    /// </summary>
    private async Task<bool> DisconnectSessionSafeModeAsync(int sessionId)
    {
        FL.Log($"[SAFE MODE] Simulating session disconnect for session {sessionId}");
        
        // Simulate operation time
        await Task.Delay(400);

        // Log safe mode operation
        FL.LogSafeModeOperation("DisconnectSession", 
            new { SessionId = sessionId, Method = "WTSDisconnectSession", RequestedAt = DateTime.Now }, 
            new { Success = true, SimulatedOperation = true });

        FL.Log($"[SAFE MODE] Simulated successful disconnect for session {sessionId}");
        return true;
    }

    #endregion

    #region Error Handling and Retry Logic

    /// <summary>
    /// Determines if a Windows error code indicates a non-retryable condition
    /// </summary>
    /// <param name="errorCode">Windows error code</param>
    /// <returns>True if the error should not be retried</returns>
    private bool IsNonRetryableError(int errorCode)
    {
        // Common non-retryable error codes for WTS operations
        return errorCode switch
        {
            5 => true,    // ERROR_ACCESS_DENIED - Permission denied
            87 => true,   // ERROR_INVALID_PARAMETER - Invalid parameter
            1008 => true, // ERROR_NO_TOKEN - No security token
            1314 => true, // ERROR_PRIVILEGE_NOT_HELD - Required privilege not held
            1326 => true, // ERROR_LOGON_FAILURE - Logon failure
            1722 => true, // RPC_S_SERVER_UNAVAILABLE - RPC server unavailable
            _ => false    // Other errors may be retryable
        };
    }

    /// <summary>
    /// Calculates retry delay with exponential backoff
    /// </summary>
    /// <param name="attemptNumber">Current attempt number (1-based)</param>
    /// <returns>Delay in milliseconds</returns>
    private int CalculateRetryDelay(int attemptNumber)
    {
        // Exponential backoff: 500ms, 1000ms, 2000ms, etc.
        return Math.Min(500 * (int)Math.Pow(2, attemptNumber - 1), 5000);
    }

    /// <summary>
    /// Enhanced session enumeration with error handling and fallback mechanisms
    /// Requirement 7.1: Continue operation even if some API calls fail
    /// </summary>
    public async Task<List<WTSSessionInfo>> EnumerateSessionsWithFallbackAsync()
    {
        if (_isSafeMode)
        {
            return await EnumerateSessionsSafeModeAsync();
        }

        var sessions = new List<WTSSessionInfo>();
        Exception? lastException = null;

        try
        {
            // Primary method: Use WTSEnumerateSessions
            sessions = await EnumerateSessionsAsync();
            
            if (sessions.Count > 0)
            {
                FL.Log($"RDSessionService: Successfully enumerated {sessions.Count} sessions using primary method");
                return sessions;
            }
        }
        catch (Exception ex)
        {
            lastException = ex;
            FL.LogDetailedError("EnumerateSessionsPrimary", ex, "Primary session enumeration failed, attempting fallback");
        }

        try
        {
            // Fallback method: Try to get at least the console session
            FL.Log("RDSessionService: Attempting fallback session enumeration");
            
            var fallbackSession = new WTSSessionInfo
            {
                SessionId = 0,
                WinStationName = "Console",
                State = WTSConnectState.Active
            };
            
            sessions.Add(fallbackSession);
            FL.Log("RDSessionService: Fallback enumeration provided console session");
        }
        catch (Exception ex)
        {
            FL.LogDetailedError("EnumerateSessionsFallback", ex, "Fallback session enumeration also failed");
        }

        if (sessions.Count == 0 && lastException != null)
        {
            FL.LogDetailedError("EnumerateSessionsComplete", lastException, "All session enumeration methods failed");
        }

        return sessions;
    }

    #endregion
}