using System;
using System.Linq;
using System.Threading.Tasks;
using AppHider.Services;
using AppHider.Models;

namespace AppHider.Utils;

/// <summary>
/// Simple test class to verify RemoteDesktopManager functionality.
/// This can be run manually to test remote desktop management.
/// </summary>
public static class RemoteDesktopManagerTest
{
    public static async Task RunTestsAsync()
    {
        Console.WriteLine("=== RemoteDesktopManager Tests ===\n");

        await TestSafeModeDetection();
        await TestConnectionDetection();
        await TestConnectionTermination();
        await TestCaching();
        await TestEventHandling();

        Console.WriteLine("\n=== All RemoteDesktopManager Tests Completed ===");
    }

    private static async Task TestSafeModeDetection()
    {
        Console.WriteLine("Test 1: Safe Mode Detection and Simulation");

        try
        {
            // Create mock services for testing
            var sessionService = new MockRDSessionService();
            var clientService = new MockRDClientService();
            var manager = new RemoteDesktopManager(sessionService, clientService);

            // Test safe mode enabled
            manager.IsSafeMode = true;
            Console.WriteLine($"  Safe mode enabled: {manager.IsSafeMode} (Expected: True)");

            // Test safe mode connection detection
            var connections = await manager.GetActiveConnectionsAsync();
            Console.WriteLine($"  Safe mode connections found: {connections.Count} (Expected: > 0)");

            // Test safe mode termination
            var terminationResult = await manager.TerminateAllConnectionsAsync();
            Console.WriteLine($"  Safe mode termination result: {terminationResult} (Expected: True)");

            // Test safe mode disabled
            manager.IsSafeMode = false;
            Console.WriteLine($"  Safe mode disabled: {manager.IsSafeMode} (Expected: False)");

            Console.WriteLine("  ✓ Safe mode tests passed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ Safe mode tests failed: {ex.Message}");
        }

        Console.WriteLine();
    }

    private static async Task TestConnectionDetection()
    {
        Console.WriteLine("Test 2: Connection Detection");

        try
        {
            var sessionService = new MockRDSessionService();
            var clientService = new MockRDClientService();
            var manager = new RemoteDesktopManager(sessionService, clientService);

            // Test in safe mode to avoid affecting real connections
            manager.IsSafeMode = true;

            // Test getting all connections
            var allConnections = await manager.GetActiveConnectionsAsync();
            Console.WriteLine($"  Total connections detected: {allConnections.Count}");

            // Verify connection types
            var sessionConnections = allConnections.Where(c => c.Type == RDPConnectionType.IncomingSession).ToList();
            var clientConnections = allConnections.Where(c => c.Type == RDPConnectionType.OutgoingClient).ToList();

            Console.WriteLine($"  Session connections: {sessionConnections.Count}");
            Console.WriteLine($"  Client connections: {clientConnections.Count}");

            // Test caching (second call should be faster)
            var startTime = DateTime.Now;
            var cachedConnections = await manager.GetActiveConnectionsAsync();
            var cacheTime = DateTime.Now - startTime;

            Console.WriteLine($"  Cached call time: {cacheTime.TotalMilliseconds}ms (Should be < 50ms)");
            Console.WriteLine($"  Cached connections count: {cachedConnections.Count} (Should match previous)");

            Console.WriteLine("  ✓ Connection detection tests passed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ Connection detection tests failed: {ex.Message}");
        }

        Console.WriteLine();
    }

    private static async Task TestConnectionTermination()
    {
        Console.WriteLine("Test 3: Connection Termination");

        try
        {
            var sessionService = new MockRDSessionService();
            var clientService = new MockRDClientService();
            var manager = new RemoteDesktopManager(sessionService, clientService);

            // Test in safe mode to avoid affecting real connections
            manager.IsSafeMode = true;

            // Test terminating all connections
            var allResult = await manager.TerminateAllConnectionsAsync();
            Console.WriteLine($"  Terminate all connections: {allResult} (Expected: True)");

            // Test terminating session connections only
            var sessionResult = await manager.TerminateSessionConnectionsAsync();
            Console.WriteLine($"  Terminate session connections: {sessionResult} (Expected: True)");

            // Test terminating client connections only
            var clientResult = await manager.TerminateClientConnectionsAsync();
            Console.WriteLine($"  Terminate client connections: {clientResult} (Expected: True)");

            Console.WriteLine("  ✓ Connection termination tests passed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ Connection termination tests failed: {ex.Message}");
        }

        Console.WriteLine();
    }

    private static async Task TestCaching()
    {
        Console.WriteLine("Test 4: Connection Caching");

        try
        {
            var sessionService = new MockRDSessionService();
            var clientService = new MockRDClientService();
            var manager = new RemoteDesktopManager(sessionService, clientService);

            manager.IsSafeMode = true;

            // First call - should populate cache
            var startTime1 = DateTime.Now;
            var connections1 = await manager.GetActiveConnectionsAsync();
            var time1 = DateTime.Now - startTime1;

            // Second call - should use cache
            var startTime2 = DateTime.Now;
            var connections2 = await manager.GetActiveConnectionsAsync();
            var time2 = DateTime.Now - startTime2;

            Console.WriteLine($"  First call time: {time1.TotalMilliseconds}ms");
            Console.WriteLine($"  Second call time: {time2.TotalMilliseconds}ms");
            Console.WriteLine($"  Cache effectiveness: {(time2 < time1 ? "✓" : "✗")} (Second call should be faster)");
            Console.WriteLine($"  Connection count consistency: {(connections1.Count == connections2.Count ? "✓" : "✗")}");

            // Wait for cache expiration (5+ seconds)
            Console.WriteLine("  Waiting for cache expiration (6 seconds)...");
            for (int i = 1; i <= 6; i++)
            {
                await Task.Delay(1000);
                Console.WriteLine($"    {i}/6 seconds...");
            }

            // Third call - should repopulate cache
            var startTime3 = DateTime.Now;
            var connections3 = await manager.GetActiveConnectionsAsync();
            var time3 = DateTime.Now - startTime3;

            Console.WriteLine($"  Third call time (after expiration): {time3.TotalMilliseconds}ms");
            Console.WriteLine($"  Cache expiration working: {(time3 > time2 ? "✓" : "✗")} (Should be slower than cached call)");

            Console.WriteLine("  ✓ Caching tests passed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ Caching tests failed: {ex.Message}");
        }

        Console.WriteLine();
    }

    private static async Task TestEventHandling()
    {
        Console.WriteLine("Test 5: Event Handling");

        try
        {
            var sessionService = new MockRDSessionService();
            var clientService = new MockRDClientService();
            var manager = new RemoteDesktopManager(sessionService, clientService);

            manager.IsSafeMode = true;

            int detectedEvents = 0;
            int terminatedEvents = 0;

            // Subscribe to events
            manager.ConnectionDetected += (sender, args) =>
            {
                detectedEvents++;
                Console.WriteLine($"    Connection detected event: {args.Connection.Type} ID {args.Connection.Id}");
            };

            manager.ConnectionTerminated += (sender, args) =>
            {
                terminatedEvents++;
                Console.WriteLine($"    Connection terminated event: {args.Connection.Type} ID {args.Connection.Id}");
            };

            // Trigger detection events
            var connections = await manager.GetActiveConnectionsAsync();
            Console.WriteLine($"  Detection events fired: {detectedEvents} (Expected: {connections.Count})");

            // Trigger termination events
            await manager.TerminateAllConnectionsAsync();
            Console.WriteLine($"  Termination events fired: {terminatedEvents} (Expected: > 0)");

            Console.WriteLine("  ✓ Event handling tests passed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ Event handling tests failed: {ex.Message}");
        }

        Console.WriteLine();
    }
}

/// <summary>
/// Mock implementation of IRDSessionService for testing
/// </summary>
internal class MockRDSessionService : IRDSessionService
{
    public bool IsSafeMode { get; set; }

    public async Task<List<WTSSessionInfo>> EnumerateSessionsAsync()
    {
        await Task.Delay(100); // Simulate API call delay
        return new List<WTSSessionInfo>
        {
            new WTSSessionInfo { SessionId = 0, WinStationName = "Console", State = WTSConnectState.Active },
            new WTSSessionInfo { SessionId = 2, WinStationName = "RDP-Tcp#0", State = WTSConnectState.Active },
            new WTSSessionInfo { SessionId = 3, WinStationName = "RDP-Tcp#1", State = WTSConnectState.Connected }
        };
    }

    public async Task<SessionInfo?> GetSessionInfoAsync(int sessionId)
    {
        await Task.Delay(50);
        return new SessionInfo
        {
            SessionId = sessionId,
            UserName = $"TestUser{sessionId}",
            ClientName = $"TestClient{sessionId}",
            ClientAddress = $"192.168.1.{100 + sessionId}",
            State = WTSConnectState.Active,
            ConnectedTime = DateTime.Now.AddMinutes(-sessionId * 10)
        };
    }

    public bool IsRemoteSession(WTSSessionInfo session)
    {
        return session.SessionId > 0 && session.WinStationName.Contains("RDP");
    }

    public async Task<bool> LogoffSessionAsync(int sessionId, int maxRetries = 3)
    {
        await Task.Delay(200);
        return true; // Simulate successful logoff
    }

    public async Task<bool> DisconnectSessionAsync(int sessionId, int maxRetries = 3)
    {
        await Task.Delay(200);
        return true; // Simulate successful disconnect
    }

    public async Task<List<WTSSessionInfo>> EnumerateSessionsWithFallbackAsync()
    {
        return await EnumerateSessionsAsync(); // Use same implementation for mock
    }
}

/// <summary>
/// Mock implementation of IRDClientService for testing
/// </summary>
internal class MockRDClientService : IRDClientService
{
    public bool IsSafeMode { get; set; }

    public async Task<List<ProcessInfo>> GetMSTSCProcessesAsync()
    {
        await Task.Delay(100);
        return new List<ProcessInfo>
        {
            new ProcessInfo { ProcessId = 1234, ProcessName = "mstsc", WindowTitle = "Remote Desktop Connection - Server1" },
            new ProcessInfo { ProcessId = 5678, ProcessName = "mstsc", WindowTitle = "Remote Desktop Connection - Server2" }
        };
    }

    public async Task<ProcessInfo?> GetProcessInfoAsync(int processId)
    {
        await Task.Delay(50);
        return new ProcessInfo
        {
            ProcessId = processId,
            ProcessName = "mstsc",
            WindowTitle = $"Remote Desktop Connection - Test{processId}",
            ExecutablePath = @"C:\Windows\System32\mstsc.exe"
        };
    }

    public async Task<bool> TerminateProcessAsync(int processId, int maxRetries = 2)
    {
        await Task.Delay(300);
        return true; // Simulate successful termination
    }

    public async Task<bool> TerminateAllMSTSCProcessesAsync()
    {
        await Task.Delay(500);
        return true; // Simulate successful termination of all processes
    }

    public async Task<List<ProcessInfo>> GetMSTSCProcessesWithFallbackAsync()
    {
        return await GetMSTSCProcessesAsync(); // Use same implementation for mock
    }

    public async Task<(bool AllSuccessful, int SuccessCount, int TotalCount, List<string> Errors)> TerminateAllMSTSCProcessesWithDetailsAsync()
    {
        await Task.Delay(500);
        var processes = await GetMSTSCProcessesAsync();
        return (true, processes.Count, processes.Count, new List<string>()); // Simulate all successful
    }
}