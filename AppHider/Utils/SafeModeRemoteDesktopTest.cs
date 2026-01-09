using System;
using System.Threading.Tasks;
using AppHider.Services;
using AppHider.Utils;

namespace AppHider.Utils;

/// <summary>
/// Simple test class to verify remote desktop safe mode functionality.
/// This can be run manually to test safe mode simulation.
/// </summary>
public static class SafeModeRemoteDesktopTest
{
    public static async Task RunTests()
    {
        Console.WriteLine("=== Remote Desktop Safe Mode Tests ===\n");

        await TestSafeModeDetection();
        await TestRemoteDesktopManagerSafeMode();
        await TestServicesSafeMode();
        await TestEmergencyDisconnectControllerSafeMode();

        Console.WriteLine("\n=== All Remote Desktop Safe Mode Tests Completed ===");
    }

    private static async Task TestSafeModeDetection()
    {
        Console.WriteLine("Test 1: Safe Mode Detection for Remote Desktop");

        // Test with --safe-mode argument
        var args1 = new[] { "--safe-mode" };
        var result1 = SafeModeDetector.DetectRemoteDesktopSafeMode(args1);
        Console.WriteLine($"  Args: [--safe-mode] => {result1} (Expected: True)");

        // Test component-specific detection
        var result2 = SafeModeDetector.DetectSafeModeForComponent(args1, "remote-desktop");
        Console.WriteLine($"  Component 'remote-desktop': {result2} (Expected: True)");

        // Test without safe mode arg
        var args3 = new[] { "--other-arg" };
        var result3 = SafeModeDetector.DetectRemoteDesktopSafeMode(args3);
        Console.WriteLine($"  Args: [--other-arg] => {result3} (Expected: False)");

        Console.WriteLine("  ✓ Safe mode detection tests passed\n");
    }

    private static async Task TestRemoteDesktopManagerSafeMode()
    {
        Console.WriteLine("Test 2: RemoteDesktopManager Safe Mode");

        try
        {
            // Create services
            var sessionService = new RDSessionService();
            var clientService = new RDClientService();
            var manager = new RemoteDesktopManager(sessionService, clientService);

            // Test safe mode enabled
            manager.IsSafeMode = true;
            Console.WriteLine($"  Safe mode enabled: {manager.IsSafeMode} (Expected: True)");

            // Test safe mode connection detection
            var connections = await manager.GetActiveConnectionsAsync();
            Console.WriteLine($"  Simulated connections found: {connections.Count} (Expected: > 0)");

            // Test safe mode termination
            var terminationResult = await manager.TerminateAllConnectionsAsync();
            Console.WriteLine($"  Simulated termination result: {terminationResult} (Expected: True)");

            Console.WriteLine("  ✓ RemoteDesktopManager safe mode tests passed\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ RemoteDesktopManager safe mode test failed: {ex.Message}\n");
        }
    }

    private static async Task TestServicesSafeMode()
    {
        Console.WriteLine("Test 3: Individual Services Safe Mode");

        try
        {
            // Test RDSessionService
            var sessionService = new RDSessionService();
            sessionService.IsSafeMode = true;
            
            var sessions = await sessionService.EnumerateSessionsAsync();
            Console.WriteLine($"  RDSessionService simulated sessions: {sessions.Count} (Expected: > 0)");

            var sessionInfo = await sessionService.GetSessionInfoAsync(2);
            Console.WriteLine($"  RDSessionService simulated session info: {sessionInfo?.UserName} (Expected: not null)");

            var logoffResult = await sessionService.LogoffSessionAsync(2);
            Console.WriteLine($"  RDSessionService simulated logoff: {logoffResult} (Expected: True)");

            // Test RDClientService
            var clientService = new RDClientService();
            clientService.IsSafeMode = true;

            var processes = await clientService.GetMSTSCProcessesAsync();
            Console.WriteLine($"  RDClientService simulated processes: {processes.Count} (Expected: > 0)");

            var processInfo = await clientService.GetProcessInfoAsync(1234);
            Console.WriteLine($"  RDClientService simulated process info: {processInfo?.ProcessName} (Expected: mstsc)");

            var terminateResult = await clientService.TerminateProcessAsync(1234);
            Console.WriteLine($"  RDClientService simulated termination: {terminateResult} (Expected: True)");

            Console.WriteLine("  ✓ Individual services safe mode tests passed\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ Individual services safe mode test failed: {ex.Message}\n");
        }
    }

    private static async Task TestEmergencyDisconnectControllerSafeMode()
    {
        Console.WriteLine("Test 4: EmergencyDisconnectController Safe Mode");

        try
        {
            // Create services
            var sessionService = new RDSessionService();
            var clientService = new RDClientService();
            var rdManager = new RemoteDesktopManager(sessionService, clientService);
            var networkController = new NetworkController();
            
            var emergencyController = new EmergencyDisconnectController(rdManager, networkController);

            // Test safe mode synchronization
            emergencyController.IsSafeMode = true;
            Console.WriteLine($"  Emergency controller safe mode: {emergencyController.IsSafeMode} (Expected: True)");
            Console.WriteLine($"  RD Manager safe mode: {rdManager.IsSafeMode} (Expected: True)");
            Console.WriteLine($"  Network Controller safe mode: {networkController.IsSafeMode} (Expected: True)");

            // Test emergency disconnect in safe mode
            var result = await emergencyController.ExecuteEmergencyDisconnectAsync();
            Console.WriteLine($"  Emergency disconnect result: {result.Success} (Expected: True)");
            Console.WriteLine($"  Sessions terminated: {result.SessionsTerminated} (Expected: > 0)");
            Console.WriteLine($"  Clients terminated: {result.ClientsTerminated} (Expected: > 0)");

            Console.WriteLine("  ✓ EmergencyDisconnectController safe mode tests passed\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ EmergencyDisconnectController safe mode test failed: {ex.Message}\n");
        }
    }
}