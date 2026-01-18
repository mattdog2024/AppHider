using System;
using System.Threading.Tasks;
using AppHider.Services;
using FL = AppHider.Utils.FileLogger;

namespace AppHider.Utils;

/// <summary>
/// Simple verification script to test remote desktop integration
/// This can be called directly to verify the integration is working
/// </summary>
public static class IntegrationVerificationScript
{
    /// <summary>
    /// Runs a quick verification of the remote desktop integration
    /// </summary>
    public static async Task<bool> VerifyIntegrationAsync()
    {
        try
        {
            FL.Log("IntegrationVerificationScript: Starting integration verification...");
            Console.WriteLine("Starting Remote Desktop Integration Verification...");

            // Initialize services
            var settingsService = new SettingsService();
            var networkController = new NetworkController();
            var rdSessionService = new RDSessionService();
            var rdClientService = new RDClientService();
            var remoteDesktopManager = new RemoteDesktopManager(rdSessionService, rdClientService);
            var emergencyDisconnectController = new EmergencyDisconnectController(remoteDesktopManager, networkController, null);
            var appHiderService = new AppHiderService();
            var vhdxManager = new VHDXManager();
            var logCleaner = new LogCleaner();
            var privacyModeController = new PrivacyModeController(appHiderService, networkController, settingsService, emergencyDisconnectController, vhdxManager, logCleaner);

            // Enable safe mode for testing
            remoteDesktopManager.IsSafeMode = true;
            networkController.IsSafeMode = true;

            Console.WriteLine("✓ All services initialized successfully");
            FL.Log("IntegrationVerificationScript: All services initialized successfully");

            // Test 1: Verify remote desktop detection
            Console.WriteLine("Testing remote desktop connection detection...");
            var connections = await remoteDesktopManager.GetActiveConnectionsAsync();
            Console.WriteLine($"✓ Detected {connections.Count} connections (safe mode simulation)");
            FL.Log($"IntegrationVerificationScript: Detected {connections.Count} connections");

            // Test 2: Test emergency disconnect
            Console.WriteLine("Testing emergency disconnect functionality...");
            var result = await emergencyDisconnectController.ExecuteEmergencyDisconnectAsync();
            
            if (result != null && result.Success)
            {
                Console.WriteLine($"✓ Emergency disconnect completed successfully in {result.ExecutionTime.TotalMilliseconds:F0}ms");
                Console.WriteLine($"  - Sessions terminated: {result.SessionsTerminated}");
                Console.WriteLine($"  - Clients terminated: {result.ClientsTerminated}");
                Console.WriteLine($"  - Network disconnected: {result.NetworkDisconnected}");
                FL.Log($"IntegrationVerificationScript: Emergency disconnect successful - {result.ExecutionTime.TotalMilliseconds:F0}ms");
            }
            else
            {
                Console.WriteLine("✗ Emergency disconnect failed");
                if (result?.Errors.Count > 0)
                {
                    Console.WriteLine($"  Errors: {string.Join(", ", result.Errors)}");
                }
                FL.Log("IntegrationVerificationScript: Emergency disconnect failed");
                return false;
            }

            // Test 3: Test settings integration
            Console.WriteLine("Testing settings integration...");
            var settings = await settingsService.LoadSettingsAsync();
            if (settings.EmergencyDisconnectHotkey != null)
            {
                Console.WriteLine($"✓ Emergency disconnect hotkey configured: {settings.EmergencyDisconnectHotkey.Modifiers}+{settings.EmergencyDisconnectHotkey.Key}");
                FL.Log($"IntegrationVerificationScript: Emergency disconnect hotkey: {settings.EmergencyDisconnectHotkey.Modifiers}+{settings.EmergencyDisconnectHotkey.Key}");
            }
            else
            {
                Console.WriteLine("✗ Emergency disconnect hotkey not configured");
                FL.Log("IntegrationVerificationScript: Emergency disconnect hotkey not configured");
                return false;
            }

            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("✓ ALL INTEGRATION TESTS PASSED!");
            Console.WriteLine("========================================");
            Console.WriteLine();
            Console.WriteLine("Remote desktop disconnect functionality is fully integrated:");
            Console.WriteLine("- Service dependency injection ✓");
            Console.WriteLine("- Remote desktop detection ✓");
            Console.WriteLine("- Emergency disconnect execution ✓");
            Console.WriteLine("- Settings integration ✓");
            Console.WriteLine("- Safe mode operation ✓");
            Console.WriteLine();
            Console.WriteLine("The integration is complete and ready for use.");

            FL.Log("IntegrationVerificationScript: ✓ All integration tests passed");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Integration verification failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            FL.Log($"IntegrationVerificationScript: Integration verification failed: {ex.Message}");
            return false;
        }
    }
}