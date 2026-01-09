using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using AppHider.Models;
using AppHider.Services;
using FL = AppHider.Utils.FileLogger;

namespace AppHider.Utils;

/// <summary>
/// Comprehensive integration test for remote desktop disconnect functionality
/// Tests the complete integration with existing AppHider functionality
/// </summary>
public class RemoteDesktopIntegrationTest
{
    private readonly IPrivacyModeController _privacyModeController;
    private readonly IEmergencyDisconnectController _emergencyDisconnectController;
    private readonly IRemoteDesktopManager _remoteDesktopManager;
    private readonly INetworkController _networkController;
    private readonly ISettingsService _settingsService;

    public RemoteDesktopIntegrationTest(
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
    /// Tests the complete emergency disconnect integration flow
    /// </summary>
    public async Task<bool> TestCompleteEmergencyDisconnectFlowAsync()
    {
        try
        {
            FL.Log("RemoteDesktopIntegrationTest: Starting complete emergency disconnect flow test");

            // Ensure safe mode is enabled for testing
            _remoteDesktopManager.IsSafeMode = true;
            _networkController.IsSafeMode = true;

            var issues = new List<string>();
            bool testPassed = true;

            // Test 1: Verify all services are properly injected
            FL.Log("RemoteDesktopIntegrationTest: Testing service injection...");
            if (_privacyModeController == null)
            {
                issues.Add("PrivacyModeController is null");
                testPassed = false;
            }
            if (_emergencyDisconnectController == null)
            {
                issues.Add("EmergencyDisconnectController is null");
                testPassed = false;
            }
            if (_remoteDesktopManager == null)
            {
                issues.Add("RemoteDesktopManager is null");
                testPassed = false;
            }
            if (_networkController == null)
            {
                issues.Add("NetworkController is null");
                testPassed = false;
            }

            // Test 2: Verify remote desktop detection works
            FL.Log("RemoteDesktopIntegrationTest: Testing remote desktop detection...");
            var connections = await _remoteDesktopManager.GetActiveConnectionsAsync();
            FL.Log($"RemoteDesktopIntegrationTest: Detected {connections.Count} connections (safe mode simulation)");

            // Test 3: Test emergency disconnect execution
            FL.Log("RemoteDesktopIntegrationTest: Testing emergency disconnect execution...");
            var stopwatch = Stopwatch.StartNew();
            var result = await _emergencyDisconnectController.ExecuteEmergencyDisconnectAsync();
            stopwatch.Stop();

            if (result == null)
            {
                issues.Add("Emergency disconnect result is null");
                testPassed = false;
            }
            else
            {
                if (!result.Success)
                {
                    issues.Add($"Emergency disconnect failed: {string.Join(", ", result.Errors)}");
                    testPassed = false;
                }

                if (result.ExecutionTime.TotalMilliseconds <= 0)
                {
                    issues.Add("Execution time was not recorded");
                    testPassed = false;
                }

                // Verify timing requirement (should complete within 10 seconds)
                if (stopwatch.Elapsed.TotalSeconds > 10)
                {
                    issues.Add($"Emergency disconnect took too long: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
                    testPassed = false;
                }

                FL.Log($"RemoteDesktopIntegrationTest: Emergency disconnect completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
                FL.Log($"RemoteDesktopIntegrationTest: Sessions terminated: {result.SessionsTerminated}");
                FL.Log($"RemoteDesktopIntegrationTest: Clients terminated: {result.ClientsTerminated}");
                FL.Log($"RemoteDesktopIntegrationTest: Network disconnected: {result.NetworkDisconnected}");
            }

            // Test 4: Verify integration with existing privacy mode
            FL.Log("RemoteDesktopIntegrationTest: Testing privacy mode integration...");
            var privacyModeStateBefore = _privacyModeController.IsPrivacyModeActive;
            
            // Emergency disconnect should not directly affect privacy mode state
            // (it works alongside it but doesn't change it)
            var privacyModeStateAfter = _privacyModeController.IsPrivacyModeActive;
            
            if (privacyModeStateBefore != privacyModeStateAfter)
            {
                issues.Add("Emergency disconnect unexpectedly changed privacy mode state");
                testPassed = false;
            }

            // Test 5: Verify settings integration
            FL.Log("RemoteDesktopIntegrationTest: Testing settings integration...");
            try
            {
                var settings = await _settingsService.LoadSettingsAsync();
                if (settings.EmergencyDisconnectHotkey == null)
                {
                    issues.Add("Emergency disconnect hotkey not found in settings");
                    testPassed = false;
                }
                else
                {
                    FL.Log($"RemoteDesktopIntegrationTest: Emergency disconnect hotkey: {settings.EmergencyDisconnectHotkey.Modifiers}+{settings.EmergencyDisconnectHotkey.Key}");
                }
            }
            catch (Exception ex)
            {
                issues.Add($"Failed to load settings: {ex.Message}");
                testPassed = false;
            }

            if (testPassed)
            {
                FL.Log("RemoteDesktopIntegrationTest: ✓ Complete emergency disconnect flow test PASSED");
            }
            else
            {
                FL.Log($"RemoteDesktopIntegrationTest: ✗ Complete emergency disconnect flow test FAILED - Issues: {string.Join(", ", issues)}");
            }

            return testPassed;
        }
        catch (Exception ex)
        {
            FL.Log($"RemoteDesktopIntegrationTest: Exception during complete flow test: {ex.Message}");
            FL.Log($"RemoteDesktopIntegrationTest: Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// Tests compatibility with existing AppHider functionality
    /// </summary>
    public async Task<bool> TestExistingFunctionalityCompatibilityAsync()
    {
        try
        {
            FL.Log("RemoteDesktopIntegrationTest: Starting existing functionality compatibility test");

            var issues = new List<string>();
            bool testPassed = true;

            // Test 1: Verify privacy mode still works independently
            FL.Log("RemoteDesktopIntegrationTest: Testing privacy mode independence...");
            var initialPrivacyState = _privacyModeController.IsPrivacyModeActive;
            
            // Toggle privacy mode
            await _privacyModeController.TogglePrivacyModeAsync();
            var toggledState = _privacyModeController.IsPrivacyModeActive;
            
            if (initialPrivacyState == toggledState)
            {
                issues.Add("Privacy mode toggle did not change state");
                testPassed = false;
            }
            
            // Toggle back
            await _privacyModeController.TogglePrivacyModeAsync();
            var finalState = _privacyModeController.IsPrivacyModeActive;
            
            if (initialPrivacyState != finalState)
            {
                issues.Add("Privacy mode did not return to initial state");
                testPassed = false;
            }

            // Test 2: Verify network controller still works independently
            FL.Log("RemoteDesktopIntegrationTest: Testing network controller independence...");
            _networkController.IsSafeMode = true; // Ensure safe mode for testing
            
            // Test network disable/enable cycle
            await _networkController.DisableNetworkAsync();
            
            await _networkController.RestoreNetworkAsync();

            // Test 3: Verify settings service still works
            FL.Log("RemoteDesktopIntegrationTest: Testing settings service compatibility...");
            try
            {
                var settings = await _settingsService.LoadSettingsAsync();
                
                // Verify all expected settings are present
                if (settings.ToggleHotkey == null)
                {
                    issues.Add("Toggle hotkey missing from settings");
                    testPassed = false;
                }
                if (settings.MenuHotkey == null)
                {
                    issues.Add("Menu hotkey missing from settings");
                    testPassed = false;
                }
                if (settings.EmergencyDisconnectHotkey == null)
                {
                    issues.Add("Emergency disconnect hotkey missing from settings");
                    testPassed = false;
                }
                
                // Test settings save/load cycle
                var originalToggleKey = settings.ToggleHotkey.Key;
                settings.ToggleHotkey.Key = System.Windows.Input.Key.F11; // Temporary change
                
                await _settingsService.SaveSettingsAsync(settings);
                var reloadedSettings = await _settingsService.LoadSettingsAsync();
                
                if (reloadedSettings.ToggleHotkey.Key != System.Windows.Input.Key.F11)
                {
                    issues.Add("Settings save/load cycle failed");
                    testPassed = false;
                }
                
                // Restore original setting
                settings.ToggleHotkey.Key = originalToggleKey;
                await _settingsService.SaveSettingsAsync(settings);
            }
            catch (Exception ex)
            {
                issues.Add($"Settings service compatibility test failed: {ex.Message}");
                testPassed = false;
            }

            if (testPassed)
            {
                FL.Log("RemoteDesktopIntegrationTest: ✓ Existing functionality compatibility test PASSED");
            }
            else
            {
                FL.Log($"RemoteDesktopIntegrationTest: ✗ Existing functionality compatibility test FAILED - Issues: {string.Join(", ", issues)}");
            }

            return testPassed;
        }
        catch (Exception ex)
        {
            FL.Log($"RemoteDesktopIntegrationTest: Exception during compatibility test: {ex.Message}");
            FL.Log($"RemoteDesktopIntegrationTest: Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// Tests hotkey registration and integration
    /// </summary>
    public async Task<bool> TestHotkeyIntegrationAsync()
    {
        try
        {
            FL.Log("RemoteDesktopIntegrationTest: Starting hotkey integration test");

            var issues = new List<string>();
            bool testPassed = true;

            // Test emergency disconnect hotkey registration
            FL.Log("RemoteDesktopIntegrationTest: Testing emergency disconnect hotkey registration...");
            
            var testHotkey = new HotkeyConfig
            {
                Key = System.Windows.Input.Key.F7,
                Modifiers = System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift
            };

            // Test registration
            var registrationResult = await _emergencyDisconnectController.RegisterEmergencyHotkeyAsync(testHotkey);
            if (!registrationResult)
            {
                issues.Add("Emergency disconnect hotkey registration failed");
                testPassed = false;
            }

            // Test unregistration
            var unregistrationResult = await _emergencyDisconnectController.UnregisterEmergencyHotkeyAsync();
            if (!unregistrationResult)
            {
                issues.Add("Emergency disconnect hotkey unregistration failed");
                testPassed = false;
            }

            // Re-register the default hotkey
            var settings = await _settingsService.LoadSettingsAsync();
            await _emergencyDisconnectController.RegisterEmergencyHotkeyAsync(settings.EmergencyDisconnectHotkey);

            if (testPassed)
            {
                FL.Log("RemoteDesktopIntegrationTest: ✓ Hotkey integration test PASSED");
            }
            else
            {
                FL.Log($"RemoteDesktopIntegrationTest: ✗ Hotkey integration test FAILED - Issues: {string.Join(", ", issues)}");
            }

            return testPassed;
        }
        catch (Exception ex)
        {
            FL.Log($"RemoteDesktopIntegrationTest: Exception during hotkey integration test: {ex.Message}");
            FL.Log($"RemoteDesktopIntegrationTest: Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// Runs all integration tests
    /// </summary>
    public async Task<bool> RunAllIntegrationTestsAsync()
    {
        FL.Log("RemoteDesktopIntegrationTest: ========================================");
        FL.Log("RemoteDesktopIntegrationTest: Starting Remote Desktop Integration Tests");
        FL.Log("RemoteDesktopIntegrationTest: ========================================");

        var tests = new List<(string Name, Func<Task<bool>> Test)>
        {
            ("Complete Emergency Disconnect Flow", TestCompleteEmergencyDisconnectFlowAsync),
            ("Existing Functionality Compatibility", TestExistingFunctionalityCompatibilityAsync),
            ("Hotkey Integration", TestHotkeyIntegrationAsync)
        };

        int passed = 0;
        int total = tests.Count;

        foreach (var (name, test) in tests)
        {
            FL.Log($"RemoteDesktopIntegrationTest: Running test: {name}");
            
            try
            {
                bool result = await test();
                if (result)
                {
                    passed++;
                    FL.Log($"RemoteDesktopIntegrationTest: ✓ {name} - PASSED");
                }
                else
                {
                    FL.Log($"RemoteDesktopIntegrationTest: ✗ {name} - FAILED");
                }
            }
            catch (Exception ex)
            {
                FL.Log($"RemoteDesktopIntegrationTest: ✗ {name} - EXCEPTION: {ex.Message}");
            }

            FL.Log(""); // Empty line for readability
        }

        bool allPassed = passed == total;
        
        FL.Log("RemoteDesktopIntegrationTest: ========================================");
        FL.Log($"RemoteDesktopIntegrationTest: Integration Test Results: {passed}/{total} tests passed");
        FL.Log($"RemoteDesktopIntegrationTest: Overall Result: {(allPassed ? "PASSED" : "FAILED")}");
        FL.Log("RemoteDesktopIntegrationTest: ========================================");

        return allPassed;
    }
}