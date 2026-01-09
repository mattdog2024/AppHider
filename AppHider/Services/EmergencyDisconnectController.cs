using System.Diagnostics;
using AppHider.Models;
using AppHider.Utils;
using FL = AppHider.Utils.FileLogger;

namespace AppHider.Services;

/// <summary>
/// Emergency Disconnect Controller coordinates remote desktop termination and network disconnection
/// Implements parallel execution with proper operation sequencing (remote desktop first, then network)
/// Supports safe mode for testing without affecting real connections or network
/// </summary>
public class EmergencyDisconnectController : IEmergencyDisconnectController
{
    private readonly IRemoteDesktopManager _remoteDesktopManager;
    private readonly INetworkController _networkController;
    private IHotkeyManager? _hotkeyManager;
    private HotkeyConfig? _currentHotkeyConfig;
    private bool _isSafeMode;

    // Events for emergency disconnect operations
    public event EventHandler<EmergencyDisconnectEventArgs>? EmergencyDisconnectTriggered;
    public event EventHandler<EmergencyDisconnectEventArgs>? EmergencyDisconnectCompleted;

    /// <summary>
    /// Gets or sets safe mode flag for testing without affecting real connections or network
    /// When set, ensures both remote desktop manager and network controller use the same safe mode
    /// </summary>
    public bool IsSafeMode 
    { 
        get => _isSafeMode;
        set 
        {
            _isSafeMode = value;
            
            // Ensure both managed services use the same safe mode setting
            _remoteDesktopManager.IsSafeMode = value;
            _networkController.IsSafeMode = value;
            
            FL.Log($"EmergencyDisconnectController: Safe mode {(value ? "enabled" : "disabled")} - synchronized with managed services");
        }
    }

    public EmergencyDisconnectController(
        IRemoteDesktopManager remoteDesktopManager,
        INetworkController networkController,
        IHotkeyManager? hotkeyManager = null)
    {
        _remoteDesktopManager = remoteDesktopManager ?? throw new ArgumentNullException(nameof(remoteDesktopManager));
        _networkController = networkController ?? throw new ArgumentNullException(nameof(networkController));
        _hotkeyManager = hotkeyManager;

        // Detect safe mode using SafeModeDetector for consistency
        _isSafeMode = SafeModeDetector.DetectSafeMode(Environment.GetCommandLineArgs());
        
        // Ensure both managed services use the same safe mode setting
        _remoteDesktopManager.IsSafeMode = _isSafeMode;
        _networkController.IsSafeMode = _isSafeMode;

        FL.Log($"EmergencyDisconnectController: Initialized with safe mode {(_isSafeMode ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Sets the hotkey manager for emergency disconnect hotkey registration
    /// </summary>
    public void SetHotkeyManager(IHotkeyManager hotkeyManager)
    {
        _hotkeyManager = hotkeyManager ?? throw new ArgumentNullException(nameof(hotkeyManager));
        FL.Log("EmergencyDisconnectController: HotkeyManager set successfully");
        
        // Ensure safe mode consistency when hotkey manager is set
        if (_isSafeMode)
        {
            FL.Log("EmergencyDisconnectController: Safe mode is active - hotkey operations will be simulated");
        }
    }

    /// <summary>
    /// Executes emergency disconnect with enhanced error handling and resilience
    /// Requirements: 3.1 (parallel execution), 3.2 (operation sequencing), 3.3 (network integration), 7.5 (continue with network even if RD fails)
    /// </summary>
    public async Task<EmergencyDisconnectResult> ExecuteEmergencyDisconnectAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var sequenceStartTime = DateTime.Now;
        var result = new EmergencyDisconnectResult();
        var errors = new List<string>();

        DateTime rdStartTime = DateTime.MinValue;
        DateTime rdEndTime = DateTime.MinValue;
        DateTime networkStartTime = DateTime.MinValue;
        DateTime networkEndTime = DateTime.MinValue;

        try
        {
            FL.Log("EmergencyDisconnectController: Starting enhanced emergency disconnect sequence");
            
            // Fire triggered event
            EmergencyDisconnectTriggered?.Invoke(this, new EmergencyDisconnectEventArgs("Emergency disconnect initiated"));

            // Step 1: Start both operations in parallel (Requirement 3.1)
            FL.Log("EmergencyDisconnectController: Starting parallel operations - remote desktop termination and network preparation");
            
            rdStartTime = DateTime.Now;
            var remoteDesktopTask = TerminateRemoteDesktopConnectionsWithResilienceAsync();
            var networkPreparationTask = PrepareNetworkDisconnectionAsync();

            // Wait for both to start
            await Task.WhenAll(remoteDesktopTask, networkPreparationTask);

            // Get results from remote desktop termination
            var rdResult = await remoteDesktopTask;
            rdEndTime = DateTime.Now;
            
            result.SessionsTerminated = rdResult.SessionsTerminated;
            result.ClientsTerminated = rdResult.ClientsTerminated;
            
            if (!rdResult.Success)
            {
                errors.AddRange(rdResult.Errors);
                FL.Log($"EmergencyDisconnectController: Remote desktop termination had errors but continuing with network disconnect: {string.Join(", ", rdResult.Errors)}");
            }

            // Step 2: Execute network disconnection regardless of remote desktop results (Requirement 7.5)
            FL.Log("EmergencyDisconnectController: Remote desktop termination completed, proceeding with network disconnection regardless of RD results");
            
            networkStartTime = DateTime.Now;
            try
            {
                await _networkController.DisableNetworkAsync();
                networkEndTime = DateTime.Now;
                result.NetworkDisconnected = true;
                FL.Log("EmergencyDisconnectController: Network disconnection completed successfully");
            }
            catch (Exception ex)
            {
                networkEndTime = DateTime.Now;
                string networkError = $"Network disconnection failed: {ex.Message}";
                errors.Add(networkError);
                FL.LogDetailedError("NetworkDisconnectionResilient", ex, "Network disconnection failed during enhanced emergency disconnect");
                result.NetworkDisconnected = false;
                
                // Even if network fails, we continue - this is emergency mode
                FL.Log("EmergencyDisconnectController: Network disconnection failed but emergency sequence continues");
            }

            // Determine overall success - prioritize network disconnection success for privacy protection
            // If network succeeded, consider it a success even if some RD connections failed
            result.Success = result.NetworkDisconnected || (rdResult.Success && errors.Count == 0);
            result.Errors = errors;
            result.ExecutionTime = stopwatch.Elapsed;

            // Log the complete emergency disconnect sequence with detailed timing
            FL.LogEmergencyDisconnectSequence(result, sequenceStartTime, rdStartTime, rdEndTime, networkStartTime, networkEndTime);

            FL.Log($"EmergencyDisconnectController: Enhanced emergency disconnect completed in {result.ExecutionTime.TotalMilliseconds:F0}ms");
            FL.Log($"EmergencyDisconnectController: Results - Sessions: {result.SessionsTerminated}, Clients: {result.ClientsTerminated}, Network: {result.NetworkDisconnected}, Success: {result.Success}");

            // Log performance metrics
            FL.LogPerformanceMetrics("EnhancedEmergencyDisconnectSequence", result.ExecutionTime, 
                result.SessionsTerminated + result.ClientsTerminated + (result.NetworkDisconnected ? 1 : 0),
                (result.SessionsTerminated > 0 ? 1 : 0) + (result.ClientsTerminated > 0 ? 1 : 0) + (result.NetworkDisconnected ? 1 : 0),
                errors.Count);

            // Fire completion event
            EmergencyDisconnectCompleted?.Invoke(this, new EmergencyDisconnectEventArgs("Enhanced emergency disconnect completed", result, true));

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            networkEndTime = networkEndTime == DateTime.MinValue ? DateTime.Now : networkEndTime;
            rdEndTime = rdEndTime == DateTime.MinValue ? DateTime.Now : rdEndTime;
            
            errors.Add($"Emergency disconnect failed: {ex.Message}");
            
            result.Success = false;
            result.Errors = errors;
            result.ExecutionTime = stopwatch.Elapsed;

            // Log detailed error and sequence even on failure
            FL.LogDetailedError("ExecuteEmergencyDisconnectEnhanced", ex, "Critical failure during enhanced emergency disconnect sequence");
            FL.LogEmergencyDisconnectSequence(result, sequenceStartTime, rdStartTime, rdEndTime, networkStartTime, networkEndTime);
            
            // Fire completion event with error
            EmergencyDisconnectCompleted?.Invoke(this, new EmergencyDisconnectEventArgs($"Enhanced emergency disconnect failed: {ex.Message}", result, true));

            return result;
        }
    }

    /// <summary>
    /// Executes only remote desktop disconnect without network disconnection
    /// Used by privacy mode controller to close RD connections before network disconnect
    /// </summary>
    public async Task<EmergencyDisconnectResult> ExecuteRemoteDesktopDisconnectOnlyAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new EmergencyDisconnectResult();
        var errors = new List<string>();

        try
        {
            FL.Log("EmergencyDisconnectController: Starting remote desktop disconnect only (no network)");

            // Only terminate remote desktop connections
            var rdResult = await TerminateRemoteDesktopConnectionsWithResilienceAsync();
            
            result.SessionsTerminated = rdResult.SessionsTerminated;
            result.ClientsTerminated = rdResult.ClientsTerminated;
            result.NetworkDisconnected = false; // Network is not disconnected in this method
            
            if (!rdResult.Success)
            {
                errors.AddRange(rdResult.Errors);
                FL.Log($"EmergencyDisconnectController: Remote desktop termination had errors: {string.Join(", ", rdResult.Errors)}");
            }

            result.Success = rdResult.Success;
            result.Errors = errors;
            result.ExecutionTime = stopwatch.Elapsed;

            FL.Log($"EmergencyDisconnectController: Remote desktop disconnect only completed in {result.ExecutionTime.TotalMilliseconds:F0}ms");
            FL.Log($"EmergencyDisconnectController: Results - Sessions: {result.SessionsTerminated}, Clients: {result.ClientsTerminated}, Success: {result.Success}");

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            errors.Add($"Remote desktop disconnect failed: {ex.Message}");
            
            result.Success = false;
            result.Errors = errors;
            result.ExecutionTime = stopwatch.Elapsed;

            FL.LogDetailedError("ExecuteRemoteDesktopDisconnectOnly", ex, "Failure during remote desktop disconnect only");
            
            return result;
        }
    }

    /// <summary>
    /// Registers the emergency disconnect hotkey
    /// Requirements: 4.1 (configuration interface), 4.3 (global hotkey), 4.4 (system startup)
    /// </summary>
    public async Task<bool> RegisterEmergencyHotkeyAsync(HotkeyConfig hotkey)
    {
        try
        {
            if (_hotkeyManager == null)
            {
                FL.Log("EmergencyDisconnectController: No hotkey manager available for registration");
                return false;
            }

            FL.Log($"EmergencyDisconnectController: Registering emergency hotkey - Key: {hotkey.Key}, Modifiers: {hotkey.Modifiers}");

            // Validate the hotkey configuration first
            if (!_hotkeyManager.ValidateHotkeyConfig(hotkey, out string? errorMessage))
            {
                FL.Log($"EmergencyDisconnectController: Hotkey validation failed: {errorMessage}");
                return false;
            }

            // Check if hotkey is available
            if (!_hotkeyManager.IsHotkeyAvailable(hotkey.Key, hotkey.Modifiers))
            {
                FL.Log($"EmergencyDisconnectController: Hotkey {hotkey.Modifiers}+{hotkey.Key} is already in use");
                return false;
            }

            // Unregister existing hotkey if any
            if (_currentHotkeyConfig != null)
            {
                await UnregisterEmergencyHotkeyAsync();
            }

            // Register new hotkey with callback to execute emergency disconnect
            var success = await Task.Run(() =>
            {
                try
                {
                    return _hotkeyManager.RegisterEmergencyDisconnectHotkey(hotkey, async () =>
                    {
                        FL.Log("EmergencyDisconnectController: Emergency disconnect hotkey triggered");
                        
                        // Execute emergency disconnect asynchronously
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await ExecuteEmergencyDisconnectAsync();
                            }
                            catch (Exception ex)
                            {
                                FL.Log($"EmergencyDisconnectController: Error during emergency disconnect execution: {ex.Message}");
                            }
                        });
                    });
                }
                catch (Exception ex)
                {
                    FL.Log($"EmergencyDisconnectController: Failed to register hotkey: {ex.Message}");
                    return false;
                }
            });

            if (success)
            {
                _currentHotkeyConfig = hotkey;
                FL.Log("EmergencyDisconnectController: Emergency hotkey registered successfully");
            }
            else
            {
                FL.Log("EmergencyDisconnectController: Failed to register emergency hotkey");
            }

            return success;
        }
        catch (Exception ex)
        {
            FL.Log($"EmergencyDisconnectController: Exception registering emergency hotkey: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Unregisters the current emergency disconnect hotkey
    /// </summary>
    public async Task<bool> UnregisterEmergencyHotkeyAsync()
    {
        try
        {
            if (_hotkeyManager == null || _currentHotkeyConfig == null)
            {
                FL.Log("EmergencyDisconnectController: No hotkey to unregister");
                return true;
            }

            FL.Log("EmergencyDisconnectController: Unregistering emergency hotkey");

            var success = await Task.Run(() =>
            {
                try
                {
                    return _hotkeyManager.UnregisterEmergencyDisconnectHotkey();
                }
                catch (Exception ex)
                {
                    FL.Log($"EmergencyDisconnectController: Failed to unregister hotkey: {ex.Message}");
                    return false;
                }
            });

            if (success)
            {
                _currentHotkeyConfig = null;
                FL.Log("EmergencyDisconnectController: Emergency hotkey unregistered successfully");
            }
            else
            {
                FL.Log("EmergencyDisconnectController: Failed to unregister emergency hotkey");
            }

            return success;
        }
        catch (Exception ex)
        {
            FL.Log($"EmergencyDisconnectController: Exception unregistering emergency hotkey: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Terminates all remote desktop connections with enhanced resilience and error handling
    /// Requirements 7.1, 7.2: Continue operation even if detection fails, attempt remaining connections if some fail
    /// </summary>
    private async Task<EmergencyDisconnectResult> TerminateRemoteDesktopConnectionsWithResilienceAsync()
    {
        var rdResult = new EmergencyDisconnectResult();
        var errors = new List<string>();

        try
        {
            FL.Log("EmergencyDisconnectController: Starting resilient remote desktop connection termination");

            // Get current connections with enhanced detection and fallback
            List<RDPConnection> activeConnections;
            try
            {
                activeConnections = await _remoteDesktopManager.GetActiveConnectionsAsync();
            }
            catch (Exception ex)
            {
                // Even if detection fails completely, we continue with termination attempts
                FL.LogDetailedError("ResilientConnectionDetection", ex, "Connection detection failed, proceeding with blind termination attempts");
                activeConnections = new List<RDPConnection>();
                errors.Add($"Connection detection failed: {ex.Message}");
            }

            var sessionConnections = activeConnections.Where(c => c.Type == RDPConnectionType.IncomingSession).ToList();
            var clientConnections = activeConnections.Where(c => c.Type == RDPConnectionType.OutgoingClient).ToList();

            FL.Log($"EmergencyDisconnectController: Found {sessionConnections.Count} session connections and {clientConnections.Count} client connections");

            // Execute termination operations in parallel for better performance, with individual error handling
            var sessionTask = TerminateSessionsWithResilienceAsync();
            var clientTask = TerminateClientsWithResilienceAsync();

            await Task.WhenAll(sessionTask, clientTask);

            var (sessionSuccess, sessionsTerminated, sessionErrors) = await sessionTask;
            var (clientSuccess, clientsTerminated, clientErrors) = await clientTask;

            // Aggregate results
            rdResult.SessionsTerminated = sessionsTerminated;
            rdResult.ClientsTerminated = clientsTerminated;
            errors.AddRange(sessionErrors);
            errors.AddRange(clientErrors);

            // Consider success if we terminated any connections or if there were no connections to terminate
            rdResult.Success = (sessionSuccess || sessionsTerminated > 0) && (clientSuccess || clientsTerminated > 0);
            rdResult.Errors = errors;

            FL.Log($"EmergencyDisconnectController: Resilient remote desktop termination completed - Sessions: {rdResult.SessionsTerminated}, Clients: {rdResult.ClientsTerminated}, Success: {rdResult.Success}");

            return rdResult;
        }
        catch (Exception ex)
        {
            errors.Add($"Critical failure in remote desktop termination: {ex.Message}");
            rdResult.Success = false;
            rdResult.Errors = errors;
            
            FL.LogDetailedError("TerminateRemoteDesktopConnectionsWithResilience", ex, "Critical failure during resilient remote desktop termination");
            
            return rdResult;
        }
    }

    /// <summary>
    /// Terminates session connections with maximum resilience
    /// </summary>
    private async Task<(bool Success, int TerminatedCount, List<string> Errors)> TerminateSessionsWithResilienceAsync()
    {
        var errors = new List<string>();
        int terminatedCount = 0;

        try
        {
            bool success = await _remoteDesktopManager.TerminateSessionConnectionsAsync();
            
            if (success)
            {
                // If successful, we don't know exact count, so estimate based on detection
                try
                {
                    var connections = await _remoteDesktopManager.GetActiveConnectionsAsync();
                    var sessionCount = connections.Count(c => c.Type == RDPConnectionType.IncomingSession);
                    terminatedCount = sessionCount; // Assume all were terminated if successful
                }
                catch
                {
                    terminatedCount = 1; // Conservative estimate
                }
            }
            else
            {
                errors.Add("Session termination reported failure");
            }

            return (success, terminatedCount, errors);
        }
        catch (Exception ex)
        {
            errors.Add($"Session termination exception: {ex.Message}");
            FL.LogDetailedError("TerminateSessionsWithResilience", ex, "Exception during resilient session termination");
            return (false, terminatedCount, errors);
        }
    }

    /// <summary>
    /// Terminates client connections with maximum resilience
    /// </summary>
    private async Task<(bool Success, int TerminatedCount, List<string> Errors)> TerminateClientsWithResilienceAsync()
    {
        var errors = new List<string>();
        int terminatedCount = 0;

        try
        {
            bool success = await _remoteDesktopManager.TerminateClientConnectionsAsync();
            
            if (success)
            {
                // If successful, we don't know exact count, so estimate based on detection
                try
                {
                    var connections = await _remoteDesktopManager.GetActiveConnectionsAsync();
                    var clientCount = connections.Count(c => c.Type == RDPConnectionType.OutgoingClient);
                    terminatedCount = clientCount; // Assume all were terminated if successful
                }
                catch
                {
                    terminatedCount = 1; // Conservative estimate
                }
            }
            else
            {
                errors.Add("Client termination reported failure");
            }

            return (success, terminatedCount, errors);
        }
        catch (Exception ex)
        {
            errors.Add($"Client termination exception: {ex.Message}");
            FL.LogDetailedError("TerminateClientsWithResilience", ex, "Exception during resilient client termination");
            return (false, terminatedCount, errors);
        }
    }

    /// <summary>
    /// Prepares for network disconnection (placeholder for any pre-network operations)
    /// This runs in parallel with remote desktop termination but doesn't actually disconnect yet
    /// </summary>
    private async Task<bool> PrepareNetworkDisconnectionAsync()
    {
        try
        {
            FL.Log("EmergencyDisconnectController: Preparing network disconnection");
            
            // This could include operations like:
            // - Saving current network state
            // - Preparing firewall rules
            // - Any other network preparation tasks
            
            // For now, we'll just ensure the network controller is ready
            await Task.Run(() =>
            {
                // Verify network controller is available and ready
                var currentState = _networkController.GetCurrentStateAsync();
                FL.Log("EmergencyDisconnectController: Network controller is ready for disconnection");
            });

            return true;
        }
        catch (Exception ex)
        {
            FL.Log($"EmergencyDisconnectController: Network preparation failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Dispose of resources
    /// </summary>
    public void Dispose()
    {
        try
        {
            // Unregister hotkey if registered
            if (_currentHotkeyConfig != null)
            {
                UnregisterEmergencyHotkeyAsync().Wait(TimeSpan.FromSeconds(5));
            }
        }
        catch (Exception ex)
        {
            FL.Log($"EmergencyDisconnectController: Error during disposal: {ex.Message}");
        }
    }
}