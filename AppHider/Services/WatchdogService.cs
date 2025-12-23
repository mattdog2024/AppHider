using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace AppHider.Services;

/// <summary>
/// Watchdog service that monitors the main process and restarts it if terminated.
/// Implements a heartbeat mechanism to detect process crashes.
/// </summary>
public class WatchdogService : IWatchdogService
{
    private const string WATCHDOG_PIPE_NAME = "AppHider_Watchdog_Pipe";
    private const int HEARTBEAT_INTERVAL_MS = 5000; // 5 seconds
    private const int HEARTBEAT_TIMEOUT_MS = 15000; // 15 seconds
    
    private Process? _watchdogProcess;
    private CancellationTokenSource? _heartbeatCancellation;
    private Task? _heartbeatTask;
    private readonly object _lock = new object();

    public bool IsWatchdogRunning
    {
        get
        {
            lock (_lock)
            {
                return _watchdogProcess != null && !_watchdogProcess.HasExited;
            }
        }
    }

    public async Task StartWatchdogAsync()
    {
        lock (_lock)
        {
            if (IsWatchdogRunning)
            {
                Debug.WriteLine("Watchdog is already running.");
                return;
            }
        }

        try
        {
            // Get the current executable path
            var currentExePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(currentExePath))
            {
                throw new InvalidOperationException("Cannot determine current executable path.");
            }

            // Start watchdog process with special argument
            var startInfo = new ProcessStartInfo
            {
                FileName = currentExePath,
                Arguments = "--watchdog-mode",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            lock (_lock)
            {
                _watchdogProcess = Process.Start(startInfo);
            }

            if (_watchdogProcess == null)
            {
                throw new InvalidOperationException("Failed to start watchdog process.");
            }

            Debug.WriteLine($"Watchdog process started with PID: {_watchdogProcess.Id}");

            // Start heartbeat mechanism
            _heartbeatCancellation = new CancellationTokenSource();
            _heartbeatTask = SendHeartbeatsAsync(_heartbeatCancellation.Token);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error starting watchdog: {ex.Message}");
            throw;
        }
    }

    public async Task StopWatchdogAsync()
    {
        try
        {
            // Stop heartbeat
            if (_heartbeatCancellation != null)
            {
                _heartbeatCancellation.Cancel();
                if (_heartbeatTask != null)
                {
                    try
                    {
                        await _heartbeatTask;
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancelling
                    }
                }
            }

            // Get watchdog process reference outside lock
            Process? processToStop = null;
            lock (_lock)
            {
                processToStop = _watchdogProcess;
            }

            // Stop watchdog process outside lock
            if (processToStop != null && !processToStop.HasExited)
            {
                // Send shutdown signal via pipe
                try
                {
                    await SendCommandToWatchdogAsync("SHUTDOWN");
                }
                catch
                {
                    // If pipe communication fails, force kill
                    processToStop.Kill();
                }

                processToStop.WaitForExit(5000);
                processToStop.Dispose();
                
                lock (_lock)
                {
                    _watchdogProcess = null;
                }
            }

            Debug.WriteLine("Watchdog stopped successfully.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error stopping watchdog: {ex.Message}");
            throw;
        }
    }

    private async Task SendHeartbeatsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(HEARTBEAT_INTERVAL_MS, cancellationToken);
                
                if (!cancellationToken.IsCancellationRequested)
                {
                    await SendCommandToWatchdogAsync("HEARTBEAT");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending heartbeat: {ex.Message}");
            }
        }
    }

    private async Task SendCommandToWatchdogAsync(string command)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", WATCHDOG_PIPE_NAME, PipeDirection.Out);
            await client.ConnectAsync(1000); // 1 second timeout
            
            var bytes = Encoding.UTF8.GetBytes(command);
            await client.WriteAsync(bytes, 0, bytes.Length);
            await client.FlushAsync();
        }
        catch (TimeoutException)
        {
            Debug.WriteLine("Timeout connecting to watchdog pipe.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error sending command to watchdog: {ex.Message}");
        }
    }

    /// <summary>
    /// Watchdog mode entry point - monitors the parent process
    /// </summary>
    public static async Task RunWatchdogModeAsync(int parentProcessId)
    {
        Debug.WriteLine($"Watchdog mode started, monitoring PID: {parentProcessId}");
        
        var lastHeartbeat = DateTime.UtcNow;
        var shutdownRequested = false;

        // Start pipe server to receive heartbeats
        var pipeTask = Task.Run(async () =>
        {
            while (!shutdownRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        WATCHDOG_PIPE_NAME,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync();

                    var buffer = new byte[1024];
                    var bytesRead = await server.ReadAsync(buffer, 0, buffer.Length);
                    var command = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    if (command == "HEARTBEAT")
                    {
                        lastHeartbeat = DateTime.UtcNow;
                        Debug.WriteLine("Heartbeat received.");
                    }
                    else if (command == "SHUTDOWN")
                    {
                        Debug.WriteLine("Shutdown command received.");
                        shutdownRequested = true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Watchdog pipe error: {ex.Message}");
                    await Task.Delay(1000);
                }
            }
        });

        // Monitor parent process
        while (!shutdownRequested)
        {
            try
            {
                var parentProcess = Process.GetProcessById(parentProcessId);
                
                // Check if process is still alive
                if (parentProcess.HasExited)
                {
                    Debug.WriteLine("Parent process has exited. Restarting...");
                    await RestartMainProcessAsync();
                    break;
                }

                // Check heartbeat timeout
                var timeSinceLastHeartbeat = DateTime.UtcNow - lastHeartbeat;
                if (timeSinceLastHeartbeat.TotalMilliseconds > HEARTBEAT_TIMEOUT_MS)
                {
                    Debug.WriteLine("Heartbeat timeout detected. Parent process may have crashed. Restarting...");
                    
                    // Try to kill the hung process
                    try
                    {
                        if (!parentProcess.HasExited)
                        {
                            parentProcess.Kill();
                        }
                    }
                    catch { }

                    await RestartMainProcessAsync();
                    break;
                }
            }
            catch (ArgumentException)
            {
                // Process not found - it was terminated
                Debug.WriteLine("Parent process not found. Restarting...");
                await RestartMainProcessAsync();
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Watchdog monitoring error: {ex.Message}");
            }

            await Task.Delay(2000); // Check every 2 seconds
        }

        Debug.WriteLine("Watchdog exiting.");
    }

    private static async Task RestartMainProcessAsync()
    {
        try
        {
            var currentExePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(currentExePath))
            {
                Debug.WriteLine("Cannot determine executable path for restart.");
                return;
            }

            // Wait a moment before restarting
            await Task.Delay(2000);

            var startInfo = new ProcessStartInfo
            {
                FileName = currentExePath,
                Arguments = "--background", // Restart in background mode
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            var newProcess = Process.Start(startInfo);
            if (newProcess != null)
            {
                Debug.WriteLine($"Main process restarted with PID: {newProcess.Id}");
            }
            else
            {
                Debug.WriteLine("Failed to restart main process.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error restarting main process: {ex.Message}");
        }
    }
}
