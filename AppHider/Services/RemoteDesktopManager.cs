using System.Collections.Concurrent;
using AppHider.Models;
using AppHider.Utils;
using FL = AppHider.Utils.FileLogger;

namespace AppHider.Services;

/// <summary>
/// Main service for managing all remote desktop connections (both incoming sessions and outgoing clients)
/// Integrates session service and client service with advanced caching and performance optimizations
/// Requirements: 8.1-8.5 (performance optimization and monitoring)
/// </summary>
public class RemoteDesktopManager : IRemoteDesktopManager
{
    private readonly IRDSessionService _sessionService;
    private readonly IRDClientService _clientService;
    private readonly ConnectionCacheService _cacheService;
    private readonly BatchOperationService _batchService;
    private readonly PerformanceMonitor _performanceMonitor;
    private readonly SemaphoreSlim _operationSemaphore;
    private bool _isSafeMode;

    // Events for connection detection and termination
    public event EventHandler<RDPConnectionEventArgs>? ConnectionDetected;
    public event EventHandler<RDPConnectionEventArgs>? ConnectionTerminated;

    /// <summary>
    /// Gets or sets safe mode flag for testing without affecting real connections
    /// </summary>
    public bool IsSafeMode 
    { 
        get => _isSafeMode;
        set 
        {
            _isSafeMode = value;
            FL.Log($"RemoteDesktopManager: Safe mode {(value ? "enabled" : "disabled")}");
        }
    }

    public RemoteDesktopManager(IRDSessionService sessionService, IRDClientService clientService)
    {
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));
        
        // Initialize performance optimization services
        _cacheService = new ConnectionCacheService();
        _batchService = new BatchOperationService();
        _performanceMonitor = new PerformanceMonitor();

        // Semaphore to prevent concurrent operations that might interfere
        _operationSemaphore = new SemaphoreSlim(1, 1);

        // Detect safe mode using SafeModeDetector for consistency with network operations
        _isSafeMode = SafeModeDetector.DetectRemoteDesktopSafeMode(Environment.GetCommandLineArgs());

        FL.Log($"RemoteDesktopManager: Initialized with advanced performance optimization - safe mode {(_isSafeMode ? "enabled" : "disabled")}");
        
        // Preload cache for better initial performance
        _ = Task.Run(async () =>
        {
            try
            {
                await _cacheService.PreloadCacheAsync(async () =>
                {
                    if (_isSafeMode)
                    {
                        return await GetActiveConnectionsSafeModeAsync();
                    }
                    else
                    {
                        var sessionTask = GetSessionConnectionsAsync();
                        var clientTask = GetClientConnectionsAsync();
                        await Task.WhenAll(sessionTask, clientTask);
                        
                        var sessions = await sessionTask;
                        var clients = await clientTask;
                        
                        var allConnections = new List<RDPConnection>();
                        allConnections.AddRange(sessions);
                        allConnections.AddRange(clients);
                        return allConnections;
                    }
                });
            }
            catch (Exception ex)
            {
                FL.LogDetailedError("CachePreload", ex, "Failed to preload connection cache");
            }
        });
    }

    /// <summary>
    /// Gets all active remote desktop connections with enhanced performance optimization
    /// Requirements: 8.2 (detection within 2 seconds), 8.4 (efficient CPU usage), 8.5 (minimal performance impact)
    /// </summary>
    /// <returns>List of all active RDP connections</returns>
    public async Task<List<RDPConnection>> GetActiveConnectionsAsync()
    {
        const string cacheKey = "active_connections";
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            FL.Log("RemoteDesktopManager: Starting optimized connection detection");

            if (_isSafeMode)
            {
                var safeConnections = await GetActiveConnectionsSafeModeAsync();
                _performanceMonitor.RecordOperationTime("ConnectionDetection", stopwatch.Elapsed);
                return safeConnections;
            }

            // Use advanced caching service for optimal performance
            var connections = await _cacheService.GetOrSetConnectionsAsync(cacheKey, async () =>
            {
                // Use enhanced detection methods with fallback mechanisms
                var sessionTask = GetSessionConnectionsWithFallbackAsync();
                var clientTask = GetClientConnectionsWithFallbackAsync();

                await Task.WhenAll(sessionTask, clientTask);

                var sessionConnections = await sessionTask;
                var clientConnections = await clientTask;

                // Combine all connections
                var allConnections = new List<RDPConnection>();
                allConnections.AddRange(sessionConnections);
                allConnections.AddRange(clientConnections);

                return allConnections;
            });

            stopwatch.Stop();
            _performanceMonitor.RecordOperationTime("ConnectionDetection", stopwatch.Elapsed);

            FL.Log($"RemoteDesktopManager: Optimized connection detection completed - {connections.Count} connections found in {stopwatch.ElapsedMilliseconds}ms");

            // Fire events and log detailed connection information for newly detected connections
            foreach (var connection in connections)
            {
                FL.LogConnectionDetected(connection, "Active connection detected during optimized scan");
                ConnectionDetected?.Invoke(this, new RDPConnectionEventArgs(connection, "Connection detected"));
            }

            return connections;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _performanceMonitor.RecordOperationTime("ConnectionDetection", stopwatch.Elapsed);
            FL.LogDetailedError("GetActiveConnectionsOptimized", ex, "Failed to retrieve active remote desktop connections with optimization");
            
            // Return empty list but don't throw - allow other operations to continue
            return new List<RDPConnection>();
        }
    }

    /// <summary>
    /// Terminates all active remote desktop connections with performance optimization
    /// Requirements: 8.1 (complete within 10 seconds), 8.3 (start within 1 second)
    /// </summary>
    /// <returns>True if all connections were successfully terminated</returns>
    public async Task<bool> TerminateAllConnectionsAsync()
    {
        await _operationSemaphore.WaitAsync();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            FL.Log("RemoteDesktopManager: Starting optimized termination of all connections");

            if (_isSafeMode)
            {
                var result = await TerminateAllConnectionsSafeModeAsync();
                _performanceMonitor.RecordOperationTime("TerminationStart", stopwatch.Elapsed);
                return result;
            }

            // Clear cache to ensure fresh data
            _cacheService.InvalidateCache("active_connections");

            // Record termination start time (Requirement 8.3: within 1 second)
            _performanceMonitor.RecordOperationTime("TerminationStart", stopwatch.Elapsed);

            // Execute both termination operations using batch service for optimal performance
            var sessionTask = TerminateSessionConnectionsOptimizedAsync();
            var clientTask = TerminateClientConnectionsOptimizedAsync();

            await Task.WhenAll(sessionTask, clientTask);

            bool sessionResult = await sessionTask;
            bool clientResult = await clientTask;

            bool overallResult = sessionResult && clientResult;

            stopwatch.Stop();
            _performanceMonitor.RecordOperationTime("EmergencyDisconnectSequence", stopwatch.Elapsed);

            FL.Log($"RemoteDesktopManager: Optimized termination completed - Sessions: {sessionResult}, Clients: {clientResult}, Overall: {overallResult} in {stopwatch.ElapsedMilliseconds}ms");

            return overallResult;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _performanceMonitor.RecordOperationTime("EmergencyDisconnectSequence", stopwatch.Elapsed);
            FL.LogDetailedError("TerminateAllConnectionsOptimized", ex, "Failed to terminate all remote desktop connections with optimization");
            return false;
        }
        finally
        {
            _operationSemaphore.Release();
        }
    }

    /// <summary>
    /// Terminates only remote desktop session connections with performance optimization
    /// Requirements: 8.1 (fast execution), 8.4 (efficient processing)
    /// </summary>
    /// <returns>True if all session connections were successfully terminated</returns>
    public async Task<bool> TerminateSessionConnectionsOptimizedAsync()
    {
        try
        {
            FL.Log("RemoteDesktopManager: Starting optimized termination of session connections");

            if (_isSafeMode)
            {
                FL.Log("[SAFE MODE] Would terminate all remote desktop sessions");
                await Task.Delay(1000); // Simulate operation time
                return true;
            }

            // Use enhanced session enumeration with fallback
            var allSessions = await _sessionService.EnumerateSessionsWithFallbackAsync();
            var remoteSessions = allSessions.Where(s => _sessionService.IsRemoteSession(s)).ToList();

            if (remoteSessions.Count == 0)
            {
                FL.Log("RemoteDesktopManager: No remote sessions found to terminate");
                return true;
            }

            FL.Log($"RemoteDesktopManager: Found {remoteSessions.Count} remote sessions to terminate with batch optimization");

            // Use batch service for optimal performance
            var sessionIds = remoteSessions.Select(s => s.SessionId).ToList();
            var batchResult = await _batchService.ExecuteSessionTerminationBatchAsync(
                sessionIds,
                async (sessionId, isLogoff) =>
                {
                    return await TerminateSessionWithEnhancedRetry(sessionId);
                },
                true // Use logoff first
            );

            // Log individual connection terminations for events and detailed logging
            foreach (var sessionId in batchResult.SuccessfulTerminations)
            {
                var session = remoteSessions.FirstOrDefault(s => s.SessionId == sessionId);
                if (session.SessionId != 0) // Check if we found a valid session
                {
                    var connection = new RDPConnection
                    {
                        Id = session.SessionId,
                        Type = RDPConnectionType.IncomingSession,
                        SessionId = session.SessionId,
                        State = session.State
                    };

                    FL.LogConnectionTermination(connection, true, "Optimized Batch WTS API", "", 1);
                    ConnectionTerminated?.Invoke(this, new RDPConnectionEventArgs(connection, "Session terminated"));
                }
            }

            // Log failed terminations
            foreach (var kvp in batchResult.FailedTerminations)
            {
                var session = remoteSessions.FirstOrDefault(s => s.SessionId == kvp.Key);
                if (session.SessionId != 0) // Check if we found a valid session
                {
                    var connection = new RDPConnection
                    {
                        Id = session.SessionId,
                        Type = RDPConnectionType.IncomingSession,
                        SessionId = session.SessionId,
                        State = session.State
                    };
                    
                    FL.LogConnectionTermination(connection, false, "Optimized Batch WTS API", kvp.Value, 1);
                }
            }

            FL.Log($"RemoteDesktopManager: Optimized session termination completed - {batchResult.SuccessfulTerminations.Count}/{batchResult.TotalAttempted} successful in {batchResult.ExecutionTime.TotalMilliseconds:F0}ms");

            return batchResult.Success;
        }
        catch (Exception ex)
        {
            FL.LogDetailedError("TerminateSessionConnectionsOptimized", ex, "Critical failure during optimized session connection termination");
            return false;
        }
    }

    /// <summary>
    /// Terminates only remote desktop client connections with performance optimization
    /// Requirements: 8.1 (fast execution), 8.4 (efficient processing)
    /// </summary>
    /// <returns>True if all client connections were successfully terminated</returns>
    public async Task<bool> TerminateClientConnectionsOptimizedAsync()
    {
        try
        {
            FL.Log("RemoteDesktopManager: Starting optimized termination of client connections");

            if (_isSafeMode)
            {
                FL.Log("[SAFE MODE] Would terminate all MSTSC client processes");
                await Task.Delay(1000); // Simulate operation time
                return true;
            }

            // Get MSTSC processes with enhanced detection
            var mstscProcesses = await _clientService.GetMSTSCProcessesWithFallbackAsync();

            if (mstscProcesses.Count == 0)
            {
                FL.Log("RemoteDesktopManager: No MSTSC processes found to terminate");
                return true;
            }

            FL.Log($"RemoteDesktopManager: Found {mstscProcesses.Count} MSTSC processes to terminate with batch optimization");

            // Use batch service for optimal performance
            var processIds = mstscProcesses.Select(p => p.ProcessId).ToList();
            var batchResult = await _batchService.ExecuteProcessTerminationBatchAsync(
                processIds,
                async (processId) =>
                {
                    try
                    {
                        var process = System.Diagnostics.Process.GetProcessById(processId);
                        process.Kill();
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }
            );

            // Log individual connection terminations for events and detailed logging
            foreach (var processId in batchResult.SuccessfulTerminations)
            {
                var connection = new RDPConnection
                {
                    Id = processId,
                    Type = RDPConnectionType.OutgoingClient,
                    ProcessId = processId
                };

                FL.LogConnectionTermination(connection, true, "Optimized Batch Process Kill", "", 1);
                ConnectionTerminated?.Invoke(this, new RDPConnectionEventArgs(connection, "Client process terminated"));
            }

            // Log failed terminations
            foreach (var kvp in batchResult.FailedTerminations)
            {
                var connection = new RDPConnection
                {
                    Id = kvp.Key,
                    Type = RDPConnectionType.OutgoingClient,
                    ProcessId = kvp.Key
                };
                
                FL.LogConnectionTermination(connection, false, "Optimized Batch Process Kill", kvp.Value, 1);
            }

            FL.Log($"RemoteDesktopManager: Optimized client termination completed - {batchResult.SuccessfulTerminations.Count}/{batchResult.TotalAttempted} successful in {batchResult.ExecutionTime.TotalMilliseconds:F0}ms");

            return batchResult.Success;
        }
        catch (Exception ex)
        {
            FL.LogDetailedError("TerminateClientConnectionsOptimized", ex, "Critical failure during optimized client connection termination");
            return false;
        }
    }
    public async Task<bool> TerminateSessionConnectionsAsync()
    {
        try
        {
            FL.Log("RemoteDesktopManager: Starting enhanced termination of session connections");

            if (_isSafeMode)
            {
                FL.Log("[SAFE MODE] Would terminate all remote desktop sessions");
                await Task.Delay(1000); // Simulate operation time
                return true;
            }

            // Use enhanced session enumeration with fallback
            var allSessions = await _sessionService.EnumerateSessionsWithFallbackAsync();
            var remoteSessions = allSessions.Where(s => _sessionService.IsRemoteSession(s)).ToList();

            if (remoteSessions.Count == 0)
            {
                FL.Log("RemoteDesktopManager: No remote sessions found to terminate");
                return true;
            }

            FL.Log($"RemoteDesktopManager: Found {remoteSessions.Count} remote sessions to terminate");

            int successCount = 0;
            int totalCount = remoteSessions.Count;
            var errors = new List<string>();

            // Terminate each remote session with enhanced retry logic, continuing even if some fail
            var terminationTasks = remoteSessions.Select(async session =>
            {
                try
                {
                    bool success = await TerminateSessionWithEnhancedRetry(session.SessionId);
                    if (success)
                    {
                        Interlocked.Increment(ref successCount);
                        
                        // Create connection object for event and logging
                        var connection = new RDPConnection
                        {
                            Id = session.SessionId,
                            Type = RDPConnectionType.IncomingSession,
                            SessionId = session.SessionId,
                            State = session.State
                        };

                        FL.LogConnectionTermination(connection, true, "Enhanced WTS API", "", 1);
                        ConnectionTerminated?.Invoke(this, new RDPConnectionEventArgs(connection, "Session terminated"));
                    }
                    else
                    {
                        string error = $"Failed to terminate session {session.SessionId} after all retry attempts";
                        lock (errors)
                        {
                            errors.Add(error);
                        }
                        
                        // Log failed termination
                        var connection = new RDPConnection
                        {
                            Id = session.SessionId,
                            Type = RDPConnectionType.IncomingSession,
                            SessionId = session.SessionId,
                            State = session.State
                        };
                        
                        FL.LogConnectionTermination(connection, false, "Enhanced WTS API", "All termination methods failed", 1);
                    }
                }
                catch (Exception ex)
                {
                    string error = $"Exception terminating session {session.SessionId}: {ex.Message}";
                    lock (errors)
                    {
                        errors.Add(error);
                    }
                    FL.LogDetailedError($"TerminateSession_{session.SessionId}", ex, 
                        $"Exception during session termination");
                }
            });

            // Wait for all termination attempts to complete
            await Task.WhenAll(terminationTasks);

            bool allSuccessful = successCount == totalCount;
            FL.Log($"RemoteDesktopManager: Session termination completed - {successCount}/{totalCount} successful");

            if (errors.Count > 0)
            {
                FL.Log($"RemoteDesktopManager: Session termination errors: {string.Join("; ", errors)}");
            }

            return allSuccessful;
        }
        catch (Exception ex)
        {
            FL.LogDetailedError("TerminateSessionConnectionsEnhanced", ex, "Critical failure during enhanced session connection termination");
            return false;
        }
    }

    /// <summary>
    /// Terminates only remote desktop client connections with enhanced error handling and partial success support
    /// Requirements 7.2, 7.4: Attempt remaining connections if some fail, use forced termination when normal methods fail
    /// </summary>
    /// <returns>True if all client connections were successfully terminated</returns>
    public async Task<bool> TerminateClientConnectionsAsync()
    {
        try
        {
            FL.Log("RemoteDesktopManager: Starting enhanced termination of client connections");

            if (_isSafeMode)
            {
                FL.Log("[SAFE MODE] Would terminate all MSTSC client processes");
                await Task.Delay(1000); // Simulate operation time
                return true;
            }

            // Use enhanced client service method with detailed results
            var (allSuccessful, successCount, totalCount, errors) = await _clientService.TerminateAllMSTSCProcessesWithDetailsAsync();

            if (totalCount == 0)
            {
                FL.Log("RemoteDesktopManager: No MSTSC processes found to terminate");
                return true;
            }

            FL.Log($"RemoteDesktopManager: Client termination completed - {successCount}/{totalCount} successful");

            // Log individual connection terminations for events and detailed logging
            if (successCount > 0)
            {
                // Create connection objects for successful terminations (we don't have exact process IDs here)
                for (int i = 0; i < successCount; i++)
                {
                    var connection = new RDPConnection
                    {
                        Id = i + 1000, // Use placeholder IDs
                        Type = RDPConnectionType.OutgoingClient,
                        ProcessId = i + 1000
                    };

                    FL.LogConnectionTermination(connection, true, "Enhanced Process Kill", "", 1);
                    ConnectionTerminated?.Invoke(this, new RDPConnectionEventArgs(connection, "Client process terminated"));
                }
            }

            // Log failed terminations
            if (errors.Count > 0)
            {
                FL.Log($"RemoteDesktopManager: Client termination errors: {string.Join("; ", errors)}");
                
                // Create connection objects for failed terminations
                for (int i = 0; i < (totalCount - successCount); i++)
                {
                    var connection = new RDPConnection
                    {
                        Id = i + 2000, // Use placeholder IDs
                        Type = RDPConnectionType.OutgoingClient,
                        ProcessId = i + 2000
                    };
                    
                    FL.LogConnectionTermination(connection, false, "Enhanced Process Kill", "All termination methods failed", 1);
                }
            }

            return allSuccessful;
        }
        catch (Exception ex)
        {
            FL.LogDetailedError("TerminateClientConnectionsEnhanced", ex, "Critical failure during enhanced client connection termination");
            return false;
        }
    }

    /// <summary>
    /// Gets session connections and converts them to RDPConnection objects
    /// </summary>
    private async Task<List<RDPConnection>> GetSessionConnectionsAsync()
    {
        var connections = new List<RDPConnection>();

        try
        {
            var allSessions = await _sessionService.EnumerateSessionsAsync();
            var remoteSessions = allSessions.Where(s => _sessionService.IsRemoteSession(s));

            foreach (var session in remoteSessions)
            {
                // Get detailed session information
                var sessionInfo = await _sessionService.GetSessionInfoAsync(session.SessionId);

                var connection = new RDPConnection
                {
                    Id = session.SessionId,
                    Type = RDPConnectionType.IncomingSession,
                    SessionId = session.SessionId,
                    State = session.State,
                    UserName = sessionInfo?.UserName ?? string.Empty,
                    ClientName = sessionInfo?.ClientName ?? string.Empty,
                    ClientAddress = sessionInfo?.ClientAddress ?? string.Empty,
                    ConnectedTime = sessionInfo?.ConnectedTime ?? DateTime.Now
                };

                connections.Add(connection);
            }
        }
        catch (Exception ex)
        {
            FL.LogDetailedError("GetSessionConnections", ex, "Failed to retrieve session connections");
        }

        return connections;
    }

    /// <summary>
    /// Gets client connections and converts them to RDPConnection objects
    /// </summary>
    private async Task<List<RDPConnection>> GetClientConnectionsAsync()
    {
        var connections = new List<RDPConnection>();

        try
        {
            var mstscProcesses = await _clientService.GetMSTSCProcessesAsync();

            foreach (var process in mstscProcesses)
            {
                var connection = new RDPConnection
                {
                    Id = process.ProcessId,
                    Type = RDPConnectionType.OutgoingClient,
                    ProcessId = process.ProcessId,
                    State = WTSConnectState.Active, // MSTSC processes are considered active
                    ConnectedTime = DateTime.Now // We don't have exact connection time for processes
                };

                connections.Add(connection);
            }
        }
        catch (Exception ex)
        {
            FL.LogDetailedError("GetClientConnections", ex, "Failed to retrieve client connections");
        }

        return connections;
    }

    /// <summary>
    /// Terminates a session with enhanced retry logic and multiple fallback methods
    /// Requirements 7.3, 7.4: Use alternative methods when API calls fail, escalate to forced termination
    /// </summary>
    private async Task<bool> TerminateSessionWithEnhancedRetry(int sessionId, int maxRetries = 3)
    {
        // Try logoff first (graceful)
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                FL.Log($"RemoteDesktopManager: Enhanced logoff attempt {attempt}/{maxRetries} for session {sessionId}");

                if (await _sessionService.LogoffSessionAsync(sessionId, 1)) // Single attempt per call
                {
                    FL.Log($"RemoteDesktopManager: Session {sessionId} terminated successfully with enhanced logoff");
                    return true;
                }
            }
            catch (Exception ex)
            {
                FL.LogDetailedError($"EnhancedLogoff_Attempt{attempt}", ex, $"Enhanced logoff attempt {attempt} failed for session {sessionId}");
            }

            // Wait before retry
            if (attempt < maxRetries)
            {
                await Task.Delay(1000 * attempt);
            }
        }

        // Fallback to disconnect (less graceful)
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                FL.Log($"RemoteDesktopManager: Enhanced disconnect attempt {attempt}/{maxRetries} for session {sessionId}");

                if (await _sessionService.DisconnectSessionAsync(sessionId, 1)) // Single attempt per call
                {
                    FL.Log($"RemoteDesktopManager: Session {sessionId} terminated successfully with enhanced disconnect");
                    return true;
                }
            }
            catch (Exception ex)
            {
                FL.LogDetailedError($"EnhancedDisconnect_Attempt{attempt}", ex, $"Enhanced disconnect attempt {attempt} failed for session {sessionId}");
            }

            // Wait before retry
            if (attempt < maxRetries)
            {
                await Task.Delay(1000 * attempt);
            }
        }

        FL.Log($"RemoteDesktopManager: All enhanced termination methods failed for session {sessionId}");
        return false;
    }

    /// <summary>
    /// Gets session connections with enhanced error handling and fallback mechanisms
    /// </summary>
    private async Task<List<RDPConnection>> GetSessionConnectionsWithFallbackAsync()
    {
        var connections = new List<RDPConnection>();

        try
        {
            // Use enhanced enumeration with fallback
            var allSessions = await _sessionService.EnumerateSessionsWithFallbackAsync();
            var remoteSessions = allSessions.Where(s => _sessionService.IsRemoteSession(s));

            foreach (var session in remoteSessions)
            {
                try
                {
                    // Get detailed session information with error handling
                    var sessionInfo = await GetSessionInfoWithFallback(session.SessionId);

                    var connection = new RDPConnection
                    {
                        Id = session.SessionId,
                        Type = RDPConnectionType.IncomingSession,
                        SessionId = session.SessionId,
                        State = session.State,
                        UserName = sessionInfo?.UserName ?? string.Empty,
                        ClientName = sessionInfo?.ClientName ?? string.Empty,
                        ClientAddress = sessionInfo?.ClientAddress ?? string.Empty,
                        ConnectedTime = sessionInfo?.ConnectedTime ?? DateTime.Now
                    };

                    connections.Add(connection);
                }
                catch (Exception ex)
                {
                    FL.LogDetailedError($"GetSessionConnection_{session.SessionId}", ex, 
                        $"Failed to get detailed info for session {session.SessionId}, using basic info");
                    
                    // Add connection with basic info only
                    var basicConnection = new RDPConnection
                    {
                        Id = session.SessionId,
                        Type = RDPConnectionType.IncomingSession,
                        SessionId = session.SessionId,
                        State = session.State,
                        UserName = "Unknown",
                        ClientName = "Unknown",
                        ClientAddress = "Unknown",
                        ConnectedTime = DateTime.Now
                    };

                    connections.Add(basicConnection);
                }
            }
        }
        catch (Exception ex)
        {
            FL.LogDetailedError("GetSessionConnectionsWithFallback", ex, "Failed to retrieve session connections with fallback");
        }

        return connections;
    }

    /// <summary>
    /// Gets client connections with enhanced error handling and fallback mechanisms
    /// </summary>
    private async Task<List<RDPConnection>> GetClientConnectionsWithFallbackAsync()
    {
        var connections = new List<RDPConnection>();

        try
        {
            // Use enhanced MSTSC detection with fallback
            var mstscProcesses = await _clientService.GetMSTSCProcessesWithFallbackAsync();

            foreach (var process in mstscProcesses)
            {
                try
                {
                    var connection = new RDPConnection
                    {
                        Id = process.ProcessId,
                        Type = RDPConnectionType.OutgoingClient,
                        ProcessId = process.ProcessId,
                        State = WTSConnectState.Active, // MSTSC processes are considered active
                        ConnectedTime = DateTime.Now // We don't have exact connection time for processes
                    };

                    connections.Add(connection);
                }
                catch (Exception ex)
                {
                    FL.LogDetailedError($"GetClientConnection_{process.ProcessId}", ex, 
                        $"Failed to create connection object for process {process.ProcessId}");
                }
            }
        }
        catch (Exception ex)
        {
            FL.LogDetailedError("GetClientConnectionsWithFallback", ex, "Failed to retrieve client connections with fallback");
        }

        return connections;
    }

    /// <summary>
    /// Gets session information with fallback to basic info if detailed retrieval fails
    /// </summary>
    private async Task<SessionInfo?> GetSessionInfoWithFallback(int sessionId)
    {
        try
        {
            return await _sessionService.GetSessionInfoAsync(sessionId);
        }
        catch (Exception ex)
        {
            FL.LogDetailedError($"GetSessionInfoFallback_{sessionId}", ex, 
                $"Failed to get detailed session info for {sessionId}, using fallback");
            
            // Return basic session info as fallback
            return new SessionInfo
            {
                SessionId = sessionId,
                UserName = "Unknown",
                ClientName = "Unknown",
                WinStationName = $"Session{sessionId}",
                ClientAddress = "Unknown",
                State = WTSConnectState.Active,
                ConnectedTime = DateTime.Now
            };
        }
    }

    /// <summary>
    /// Safe mode implementation for getting active connections
    /// </summary>
    private async Task<List<RDPConnection>> GetActiveConnectionsSafeModeAsync()
    {
        FL.Log("[SAFE MODE] Simulating active connection detection");
        
        // Simulate some processing time
        await Task.Delay(500);

        // Generate simulated connections for testing
        var simulatedConnections = new List<RDPConnection>
        {
            new RDPConnection
            {
                Id = 1,
                Type = RDPConnectionType.IncomingSession,
                SessionId = 2,
                UserName = "TestUser1",
                ClientName = "TEST-CLIENT-01",
                ClientAddress = "192.168.1.100",
                State = WTSConnectState.Active,
                ConnectedTime = DateTime.Now.AddMinutes(-30)
            },
            new RDPConnection
            {
                Id = 2,
                Type = RDPConnectionType.OutgoingClient,
                ProcessId = 1234,
                State = WTSConnectState.Active,
                ConnectedTime = DateTime.Now.AddMinutes(-15)
            }
        };

        // Log safe mode operation with detailed parameters
        FL.LogSafeModeOperation("GetActiveConnections", 
            new { RequestedAt = DateTime.Now }, 
            new { ConnectionCount = simulatedConnections.Count, Connections = simulatedConnections });

        FL.Log($"[SAFE MODE] Simulated {simulatedConnections.Count} active connections");
        return simulatedConnections;
    }

    /// <summary>
    /// Safe mode implementation for terminating all connections
    /// </summary>
    private async Task<bool> TerminateAllConnectionsSafeModeAsync()
    {
        FL.Log("[SAFE MODE] Simulating termination of all connections");
        
        // Get simulated connections to show what would be terminated
        var connections = await GetActiveConnectionsSafeModeAsync();
        
        FL.Log($"[SAFE MODE] Would terminate {connections.Count} connections:");
        foreach (var connection in connections)
        {
            FL.Log($"[SAFE MODE] - {connection.Type} connection ID {connection.Id}");
        }

        // Log safe mode operation with detailed parameters
        FL.LogSafeModeOperation("TerminateAllConnections", 
            new { ConnectionsToTerminate = connections.Count, RequestedAt = DateTime.Now }, 
            new { Success = true, SessionsTerminated = connections.Count(c => c.Type == RDPConnectionType.IncomingSession), 
                  ClientsTerminated = connections.Count(c => c.Type == RDPConnectionType.OutgoingClient) });

        // Simulate termination time
        await Task.Delay(2000);

        FL.Log("[SAFE MODE] Simulated termination completed successfully");
        return true;
    }

    /// <summary>
    /// Gets performance metrics for monitoring and compliance checking
    /// Requirements: 8.4 (CPU monitoring), 8.5 (performance impact assessment)
    /// </summary>
    public PerformanceMetrics GetPerformanceMetrics()
    {
        return _performanceMonitor.GetCurrentMetrics();
    }

    /// <summary>
    /// Checks if performance requirements are being met
    /// Requirements: 8.1-8.5 (all performance requirements)
    /// </summary>
    public PerformanceComplianceReport CheckPerformanceCompliance()
    {
        return _performanceMonitor.CheckPerformanceCompliance();
    }

    /// <summary>
    /// Gets cache statistics for performance analysis
    /// </summary>
    public CacheStatistics GetCacheStatistics()
    {
        return _cacheService.GetCacheStatistics();
    }

    /// <summary>
    /// Gets batch operation statistics for performance analysis
    /// </summary>
    public BatchOperationStatistics GetBatchStatistics()
    {
        return _batchService.GetStatistics();
    }

    /// <summary>
    /// Optimizes performance by clearing caches and resetting metrics
    /// </summary>
    public void OptimizePerformance()
    {
        _cacheService.ClearAllCaches();
        _performanceMonitor.ResetMetrics();
        FL.Log("RemoteDesktopManager: Performance optimization completed - caches cleared and metrics reset");
    }

    /// <summary>
    /// Dispose of resources
    /// </summary>
    public void Dispose()
    {
        try
        {
            _cacheService?.Dispose();
            _batchService?.Dispose();
            _performanceMonitor?.Dispose();
            _operationSemaphore?.Dispose();
            FL.Log("RemoteDesktopManager: Disposed successfully with performance optimization cleanup");
        }
        catch (Exception ex)
        {
            FL.LogDetailedError("RemoteDesktopManagerDispose", ex, "Error during optimized remote desktop manager disposal");
        }
    }
}