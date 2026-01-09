using System;
using System.Threading.Tasks;
using AppHider.Services;
using FL = AppHider.Utils.FileLogger;

namespace AppHider.Utils;

/// <summary>
/// Comprehensive integration test runner that tests the complete remote desktop disconnect
/// functionality integration with existing AppHider services
/// </summary>
public class ComprehensiveIntegrationTestRunner
{
    private readonly IPrivacyModeController _privacyModeController;
    private readonly IEmergencyDisconnectController _emergencyDisconnectController;
    private readonly IRemoteDesktopManager _remoteDesktopManager;
    private readonly INetworkController _networkController;
    private readonly ISettingsService _settingsService;

    public ComprehensiveIntegrationTestRunner(
        IPrivacyModeController privacyModeController,
        IEmergencyDisconnectController emergencyDisconnectController,
        IRemoteDesktopManager remoteDesktopManager,
        INetworkController networkController,
        ISettingsService settingsService)
    {
        _privacyModeController = privacyModeController;
        _emergencyDisconnectController = emergencyDisconnectController;
        _remoteDesktopManager = remoteDesktopManager;
        _networkController = networkController;
        _settingsService = settingsService;
    }

    /// <summary>
    /// Runs all integration tests with full service context
    /// </summary>
    public async Task<bool> RunAllIntegrationTestsAsync()
    {
        FL.Log("ComprehensiveIntegrationTestRunner: ========================================");
        FL.Log("ComprehensiveIntegrationTestRunner: Starting Comprehensive Integration Tests");
        FL.Log("ComprehensiveIntegrationTestRunner: ========================================");

        bool allTestsPassed = true;
        int totalTests = 0;
        int passedTests = 0;

        try
        {
            // Test 1: Remote Desktop Integration Tests
            FL.Log("ComprehensiveIntegrationTestRunner: Running Remote Desktop Integration Tests...");
            var rdIntegrationTest = new RemoteDesktopIntegrationTest(
                _privacyModeController,
                _emergencyDisconnectController,
                _remoteDesktopManager,
                _networkController,
                _settingsService);

            bool rdIntegrationPassed = await rdIntegrationTest.RunAllIntegrationTestsAsync();
            totalTests++;
            if (rdIntegrationPassed)
            {
                passedTests++;
                FL.Log("ComprehensiveIntegrationTestRunner: ✓ Remote Desktop Integration Tests: PASSED");
            }
            else
            {
                FL.Log("ComprehensiveIntegrationTestRunner: ✗ Remote Desktop Integration Tests: FAILED");
                allTestsPassed = false;
            }

            // Test 2: Emergency Disconnect Controller Tests
            FL.Log("ComprehensiveIntegrationTestRunner: Running Emergency Disconnect Controller Tests...");
            var emergencyDisconnectTest = new EmergencyDisconnectControllerTest(
                _remoteDesktopManager,
                _networkController);

            bool emergencyDisconnectPassed = await emergencyDisconnectTest.RunAllTestsAsync();
            totalTests++;
            if (emergencyDisconnectPassed)
            {
                passedTests++;
                FL.Log("ComprehensiveIntegrationTestRunner: ✓ Emergency Disconnect Controller Tests: PASSED");
            }
            else
            {
                FL.Log("ComprehensiveIntegrationTestRunner: ✗ Emergency Disconnect Controller Tests: FAILED");
                allTestsPassed = false;
            }

            // Test 3: Remote Desktop Manager Tests
            FL.Log("ComprehensiveIntegrationTestRunner: Running Remote Desktop Manager Tests...");
            try
            {
                await RemoteDesktopManagerTest.RunTestsAsync();
                totalTests++;
                passedTests++;
                FL.Log("ComprehensiveIntegrationTestRunner: ✓ Remote Desktop Manager Tests: PASSED");
            }
            catch (Exception ex)
            {
                totalTests++;
                FL.Log($"ComprehensiveIntegrationTestRunner: ✗ Remote Desktop Manager Tests: FAILED - {ex.Message}");
                allTestsPassed = false;
            }

            // Test 4: Safe Mode Remote Desktop Tests
            FL.Log("ComprehensiveIntegrationTestRunner: Running Safe Mode Remote Desktop Tests...");
            try
            {
                await SafeModeRemoteDesktopTest.RunTests();
                totalTests++;
                passedTests++;
                FL.Log("ComprehensiveIntegrationTestRunner: ✓ Safe Mode Remote Desktop Tests: PASSED");
            }
            catch (Exception ex)
            {
                totalTests++;
                FL.Log($"ComprehensiveIntegrationTestRunner: ✗ Safe Mode Remote Desktop Tests: FAILED - {ex.Message}");
                allTestsPassed = false;
            }

            // Test 5: Error Handling Remote Desktop Tests
            FL.Log("ComprehensiveIntegrationTestRunner: Running Error Handling Remote Desktop Tests...");
            try
            {
                await ErrorHandlingRemoteDesktopTest.RunAllTestsAsync();
                totalTests++;
                passedTests++;
                FL.Log("ComprehensiveIntegrationTestRunner: ✓ Error Handling Remote Desktop Tests: PASSED");
            }
            catch (Exception ex)
            {
                totalTests++;
                FL.Log($"ComprehensiveIntegrationTestRunner: ✗ Error Handling Remote Desktop Tests: FAILED - {ex.Message}");
                allTestsPassed = false;
            }

            // Test 6: Performance Optimization Tests
            FL.Log("ComprehensiveIntegrationTestRunner: Running Performance Optimization Tests...");
            try
            {
                await PerformanceOptimizationTest.RunAllTestsAsync();
                totalTests++;
                passedTests++;
                FL.Log("ComprehensiveIntegrationTestRunner: ✓ Performance Optimization Tests: PASSED");
            }
            catch (Exception ex)
            {
                totalTests++;
                FL.Log($"ComprehensiveIntegrationTestRunner: ✗ Performance Optimization Tests: FAILED - {ex.Message}");
                allTestsPassed = false;
            }

        }
        catch (Exception ex)
        {
            FL.Log($"ComprehensiveIntegrationTestRunner: Critical error during integration tests: {ex.Message}");
            FL.Log($"ComprehensiveIntegrationTestRunner: Stack trace: {ex.StackTrace}");
            allTestsPassed = false;
        }

        // Summary
        FL.Log("ComprehensiveIntegrationTestRunner: ========================================");
        FL.Log($"ComprehensiveIntegrationTestRunner: Integration Test Results: {passedTests}/{totalTests} tests passed");
        FL.Log($"ComprehensiveIntegrationTestRunner: Success Rate: {(passedTests * 100.0 / Math.Max(totalTests, 1)):F1}%");
        FL.Log($"ComprehensiveIntegrationTestRunner: Overall Result: {(allTestsPassed ? "PASSED" : "FAILED")}");
        FL.Log("ComprehensiveIntegrationTestRunner: ========================================");

        if (allTestsPassed)
        {
            FL.Log("ComprehensiveIntegrationTestRunner: ✓ ALL INTEGRATION TESTS PASSED!");
            FL.Log("ComprehensiveIntegrationTestRunner: Remote desktop disconnect functionality is fully integrated and working correctly.");
        }
        else
        {
            FL.Log("ComprehensiveIntegrationTestRunner: ✗ SOME INTEGRATION TESTS FAILED!");
            FL.Log("ComprehensiveIntegrationTestRunner: Please review the test output and fix any issues.");
        }

        return allTestsPassed;
    }

    /// <summary>
    /// Runs a quick integration verification test
    /// </summary>
    public async Task<bool> RunQuickIntegrationVerificationAsync()
    {
        try
        {
            FL.Log("ComprehensiveIntegrationTestRunner: Running quick integration verification...");

            // Enable safe mode for testing
            _remoteDesktopManager.IsSafeMode = true;
            _networkController.IsSafeMode = true;

            // Quick test: Execute emergency disconnect and verify it works
            var result = await _emergencyDisconnectController.ExecuteEmergencyDisconnectAsync();

            bool success = result != null && result.Success;

            if (success)
            {
                FL.Log("ComprehensiveIntegrationTestRunner: ✓ Quick integration verification PASSED");
                FL.Log($"ComprehensiveIntegrationTestRunner: Emergency disconnect completed in {result.ExecutionTime.TotalMilliseconds:F0}ms");
            }
            else
            {
                FL.Log("ComprehensiveIntegrationTestRunner: ✗ Quick integration verification FAILED");
                if (result != null && result.Errors.Count > 0)
                {
                    FL.Log($"ComprehensiveIntegrationTestRunner: Errors: {string.Join(", ", result.Errors)}");
                }
            }

            return success;
        }
        catch (Exception ex)
        {
            FL.Log($"ComprehensiveIntegrationTestRunner: Exception during quick verification: {ex.Message}");
            return false;
        }
    }
}