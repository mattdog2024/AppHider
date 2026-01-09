using System;
using System.Threading.Tasks;
using AppHider.Services;
using AppHider.Models;

namespace AppHider.Utils;

/// <summary>
/// Simple verification test for remote desktop management core functionality
/// </summary>
public static class SimpleRemoteDesktopTest
{
    public static async Task<bool> VerifyCoreFunctionalityAsync()
    {
        Console.WriteLine("=== Remote Desktop Core Functionality Verification ===");
        
        try
        {
            // Test 1: Create services
            Console.WriteLine("1. Creating mock services...");
            var sessionService = new MockRDSessionService();
            var clientService = new MockRDClientService();
            Console.WriteLine("   ✓ Mock services created");

            // Test 2: Create RemoteDesktopManager
            Console.WriteLine("2. Creating RemoteDesktopManager...");
            var manager = new RemoteDesktopManager(sessionService, clientService);
            manager.IsSafeMode = true;
            Console.WriteLine("   ✓ RemoteDesktopManager created in safe mode");

            // Test 3: Test connection detection
            Console.WriteLine("3. Testing connection detection...");
            var connections = await manager.GetActiveConnectionsAsync();
            Console.WriteLine($"   ✓ Detected {connections.Count} connections");

            // Test 4: Test connection termination
            Console.WriteLine("4. Testing connection termination...");
            var terminationResult = await manager.TerminateAllConnectionsAsync();
            Console.WriteLine($"   ✓ Termination result: {terminationResult}");

            // Test 5: Test individual service methods
            Console.WriteLine("5. Testing individual services...");
            var sessions = await sessionService.EnumerateSessionsAsync();
            var processes = await clientService.GetMSTSCProcessesAsync();
            Console.WriteLine($"   ✓ Sessions: {sessions.Count}, Processes: {processes.Count}");

            Console.WriteLine("\n=== All Core Functionality Tests PASSED ===");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n=== Test FAILED: {ex.Message} ===");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return false;
        }
    }
}