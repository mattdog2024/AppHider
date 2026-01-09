using System;
using System.IO;
using System.Threading.Tasks;
using AppHider.Services;

namespace AppHider.Utils;

/// <summary>
/// Simple integration test that writes directly to a file
/// </summary>
public static class SimpleIntegrationTest
{
    /// <summary>
    /// Runs a simple integration test and writes results to a file
    /// </summary>
    public static async Task<bool> RunSimpleTestAsync()
    {
        var outputFile = "simple_integration_test.txt";
        var output = new System.Text.StringBuilder();
        bool testPassed = true;

        try
        {
            output.AppendLine("========================================");
            output.AppendLine("Simple Remote Desktop Integration Test");
            output.AppendLine("========================================");
            output.AppendLine($"Started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            output.AppendLine();

            // Test 1: Initialize services
            output.AppendLine("Test 1: Initializing services...");
            try
            {
                var settingsService = new SettingsService();
                var networkController = new NetworkController();
                var rdSessionService = new RDSessionService();
                var rdClientService = new RDClientService();
                var remoteDesktopManager = new RemoteDesktopManager(rdSessionService, rdClientService);
                var emergencyDisconnectController = new EmergencyDisconnectController(remoteDesktopManager, networkController, null);

                // Enable safe mode for testing
                remoteDesktopManager.IsSafeMode = true;
                networkController.IsSafeMode = true;

                output.AppendLine("✓ All services initialized successfully");
                output.AppendLine("✓ Safe mode enabled for testing");
            }
            catch (Exception ex)
            {
                output.AppendLine($"✗ Service initialization failed: {ex.Message}");
                testPassed = false;
            }
            output.AppendLine();

            // Test 2: Test remote desktop detection
            output.AppendLine("Test 2: Remote desktop detection...");
            try
            {
                var rdSessionService = new RDSessionService();
                var rdClientService = new RDClientService();
                var remoteDesktopManager = new RemoteDesktopManager(rdSessionService, rdClientService);
                remoteDesktopManager.IsSafeMode = true;

                var connections = await remoteDesktopManager.GetActiveConnectionsAsync();
                output.AppendLine($"✓ Detected {connections.Count} connections (safe mode simulation)");
            }
            catch (Exception ex)
            {
                output.AppendLine($"✗ Remote desktop detection failed: {ex.Message}");
                testPassed = false;
            }
            output.AppendLine();

            // Test 3: Test emergency disconnect
            output.AppendLine("Test 3: Emergency disconnect...");
            try
            {
                var networkController = new NetworkController();
                var rdSessionService = new RDSessionService();
                var rdClientService = new RDClientService();
                var remoteDesktopManager = new RemoteDesktopManager(rdSessionService, rdClientService);
                var emergencyDisconnectController = new EmergencyDisconnectController(remoteDesktopManager, networkController, null);

                // Enable safe mode for testing
                remoteDesktopManager.IsSafeMode = true;
                networkController.IsSafeMode = true;

                var result = await emergencyDisconnectController.ExecuteEmergencyDisconnectAsync();
                
                if (result != null && result.Success)
                {
                    output.AppendLine($"✓ Emergency disconnect completed successfully in {result.ExecutionTime.TotalMilliseconds:F0}ms");
                    output.AppendLine($"  - Sessions terminated: {result.SessionsTerminated}");
                    output.AppendLine($"  - Clients terminated: {result.ClientsTerminated}");
                    output.AppendLine($"  - Network disconnected: {result.NetworkDisconnected}");
                }
                else
                {
                    output.AppendLine("✗ Emergency disconnect failed");
                    if (result?.Errors.Count > 0)
                    {
                        output.AppendLine($"  Errors: {string.Join(", ", result.Errors)}");
                    }
                    testPassed = false;
                }
            }
            catch (Exception ex)
            {
                output.AppendLine($"✗ Emergency disconnect test failed: {ex.Message}");
                testPassed = false;
            }
            output.AppendLine();

            // Final results
            output.AppendLine("========================================");
            if (testPassed)
            {
                output.AppendLine("✓ ALL TESTS PASSED!");
                output.AppendLine("Remote desktop integration is working correctly.");
            }
            else
            {
                output.AppendLine("✗ SOME TESTS FAILED!");
                output.AppendLine("Please review the test output above for details.");
            }
            output.AppendLine("========================================");
            output.AppendLine($"Completed at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }
        catch (Exception ex)
        {
            output.AppendLine($"✗ Critical error during testing: {ex.Message}");
            output.AppendLine($"Stack trace: {ex.StackTrace}");
            testPassed = false;
        }

        // Write results to file
        try
        {
            await File.WriteAllTextAsync(outputFile, output.ToString());
        }
        catch (Exception ex)
        {
            // If we can't write to file, at least try to write to a different location
            try
            {
                await File.WriteAllTextAsync($"C:\\temp\\{outputFile}", output.ToString());
            }
            catch
            {
                // Last resort - write to current directory with timestamp
                await File.WriteAllTextAsync($"test_result_{DateTime.Now:yyyyMMdd_HHmmss}.txt", output.ToString());
            }
        }

        return testPassed;
    }
}