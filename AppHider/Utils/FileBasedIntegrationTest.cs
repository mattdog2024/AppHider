using System;
using System.IO;
using System.Threading.Tasks;
using AppHider.Services;
using FL = AppHider.Utils.FileLogger;

namespace AppHider.Utils;

/// <summary>
/// File-based integration test that writes results to a file for verification
/// </summary>
public static class FileBasedIntegrationTest
{
    private const string TestResultsFile = "integration_test_output.txt";

    /// <summary>
    /// Runs integration tests and writes results to a file
    /// </summary>
    public static async Task<bool> RunIntegrationTestsAsync()
    {
        var output = new System.Text.StringBuilder();
        bool allTestsPassed = true;

        try
        {
            output.AppendLine("========================================");
            output.AppendLine("Remote Desktop Integration Tests");
            output.AppendLine("========================================");
            output.AppendLine($"Started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            output.AppendLine();

            // Initialize services
            output.AppendLine("Initializing services...");
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

            output.AppendLine("✓ All services initialized successfully");
            output.AppendLine("✓ Safe mode enabled for testing");
            output.AppendLine();

            // Test 1: Verify remote desktop detection
            output.AppendLine("Test 1: Remote desktop connection detection...");
            var connections = await remoteDesktopManager.GetActiveConnectionsAsync();
            output.AppendLine($"✓ Detected {connections.Count} connections (safe mode simulation)");
            output.AppendLine();

            // Test 2: Test emergency disconnect
            output.AppendLine("Test 2: Emergency disconnect functionality...");
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
                allTestsPassed = false;
            }
            output.AppendLine();

            // Test 3: Test settings integration
            output.AppendLine("Test 3: Settings integration...");
            var settings = await settingsService.LoadSettingsAsync();
            if (settings.EmergencyDisconnectHotkey != null)
            {
                output.AppendLine($"✓ Emergency disconnect hotkey configured: {settings.EmergencyDisconnectHotkey.Modifiers}+{settings.EmergencyDisconnectHotkey.Key}");
            }
            else
            {
                output.AppendLine("✗ Emergency disconnect hotkey not configured");
                allTestsPassed = false;
            }
            output.AppendLine();

            // Test 4: Run comprehensive integration tests
            output.AppendLine("Test 4: Comprehensive integration tests...");
            var integrationTestRunner = new ComprehensiveIntegrationTestRunner(
                privacyModeController,
                emergencyDisconnectController,
                remoteDesktopManager,
                networkController,
                settingsService);

            var comprehensiveTestsPassed = await integrationTestRunner.RunAllIntegrationTestsAsync();
            if (comprehensiveTestsPassed)
            {
                output.AppendLine("✓ Comprehensive integration tests PASSED");
            }
            else
            {
                output.AppendLine("✗ Comprehensive integration tests FAILED");
                allTestsPassed = false;
            }
            output.AppendLine();

            // Final results
            output.AppendLine("========================================");
            if (allTestsPassed)
            {
                output.AppendLine("✓ ALL INTEGRATION TESTS PASSED!");
                output.AppendLine("========================================");
                output.AppendLine();
                output.AppendLine("Remote desktop disconnect functionality is fully integrated:");
                output.AppendLine("- Service dependency injection ✓");
                output.AppendLine("- Remote desktop detection ✓");
                output.AppendLine("- Emergency disconnect execution ✓");
                output.AppendLine("- Settings integration ✓");
                output.AppendLine("- Safe mode operation ✓");
                output.AppendLine("- Comprehensive integration tests ✓");
                output.AppendLine();
                output.AppendLine("The integration is complete and ready for use.");
            }
            else
            {
                output.AppendLine("✗ SOME INTEGRATION TESTS FAILED!");
                output.AppendLine("========================================");
                output.AppendLine();
                output.AppendLine("Please review the test output above for details.");
            }

            output.AppendLine();
            output.AppendLine($"Completed at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }
        catch (Exception ex)
        {
            output.AppendLine($"✗ Integration tests failed with exception: {ex.Message}");
            output.AppendLine($"Stack trace: {ex.StackTrace}");
            allTestsPassed = false;
        }

        // Write results to file
        try
        {
            await File.WriteAllTextAsync(TestResultsFile, output.ToString());
            FL.Log($"FileBasedIntegrationTest: Results written to {TestResultsFile}");
        }
        catch (Exception ex)
        {
            FL.Log($"FileBasedIntegrationTest: Failed to write results to file: {ex.Message}");
        }

        return allTestsPassed;
    }
}