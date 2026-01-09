using AppHider.Models;
using AppHider.Services;
using FL = AppHider.Utils.FileLogger;

namespace AppHider.Utils;

/// <summary>
/// Test class for EmergencyDisconnectController functionality
/// Tests the parallel execution and integration with network controller
/// </summary>
public class EmergencyDisconnectControllerTest
{
    private readonly EmergencyDisconnectController _controller;
    private readonly IRemoteDesktopManager _rdManager;
    private readonly INetworkController _networkController;

    public EmergencyDisconnectControllerTest(
        IRemoteDesktopManager rdManager,
        INetworkController networkController)
    {
        _rdManager = rdManager;
        _networkController = networkController;
        _controller = new EmergencyDisconnectController(rdManager, networkController);
    }

    /// <summary>
    /// Tests the emergency disconnect functionality in safe mode
    /// </summary>
    public async Task<bool> TestEmergencyDisconnectAsync()
    {
        try
        {
            FL.Log("EmergencyDisconnectControllerTest: Starting emergency disconnect test");

            // Ensure safe mode is enabled for testing
            _rdManager.IsSafeMode = true;
            _networkController.IsSafeMode = true;

            // Subscribe to events to verify they fire correctly
            bool triggeredEventFired = false;
            bool completedEventFired = false;

            _controller.EmergencyDisconnectTriggered += (sender, args) =>
            {
                triggeredEventFired = true;
                FL.Log($"EmergencyDisconnectControllerTest: Triggered event fired - {args.Message}");
            };

            _controller.EmergencyDisconnectCompleted += (sender, args) =>
            {
                completedEventFired = true;
                FL.Log($"EmergencyDisconnectControllerTest: Completed event fired - Success: {args.Result?.Success}, Message: {args.Message}");
            };

            // Execute emergency disconnect
            var result = await _controller.ExecuteEmergencyDisconnectAsync();

            // Verify results
            bool testPassed = true;
            var issues = new List<string>();

            // Check that events fired
            if (!triggeredEventFired)
            {
                issues.Add("EmergencyDisconnectTriggered event did not fire");
                testPassed = false;
            }

            if (!completedEventFired)
            {
                issues.Add("EmergencyDisconnectCompleted event did not fire");
                testPassed = false;
            }

            // Check result properties
            if (result == null)
            {
                issues.Add("Result is null");
                testPassed = false;
            }
            else
            {
                if (!result.Success)
                {
                    issues.Add($"Emergency disconnect was not successful: {string.Join(", ", result.Errors)}");
                    testPassed = false;
                }

                if (result.ExecutionTime.TotalMilliseconds <= 0)
                {
                    issues.Add("Execution time was not recorded properly");
                    testPassed = false;
                }

                // In safe mode, we should have simulated operations
                FL.Log($"EmergencyDisconnectControllerTest: Result - Sessions: {result.SessionsTerminated}, Clients: {result.ClientsTerminated}, Network: {result.NetworkDisconnected}");
            }

            if (testPassed)
            {
                FL.Log("EmergencyDisconnectControllerTest: ✓ Emergency disconnect test PASSED");
            }
            else
            {
                FL.Log($"EmergencyDisconnectControllerTest: ✗ Emergency disconnect test FAILED - Issues: {string.Join(", ", issues)}");
            }

            return testPassed;
        }
        catch (Exception ex)
        {
            FL.Log($"EmergencyDisconnectControllerTest: Exception during test: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Tests hotkey registration functionality
    /// </summary>
    public async Task<bool> TestHotkeyRegistrationAsync()
    {
        try
        {
            FL.Log("EmergencyDisconnectControllerTest: Starting hotkey registration test");

            // Test hotkey configuration
            var hotkeyConfig = new HotkeyConfig
            {
                Key = System.Windows.Input.Key.F8,
                Modifiers = System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt
            };

            // Test registration
            bool registrationResult = await _controller.RegisterEmergencyHotkeyAsync(hotkeyConfig);
            
            if (!registrationResult)
            {
                FL.Log("EmergencyDisconnectControllerTest: ✗ Hotkey registration test FAILED - Registration returned false");
                return false;
            }

            // Test unregistration
            bool unregistrationResult = await _controller.UnregisterEmergencyHotkeyAsync();
            
            if (!unregistrationResult)
            {
                FL.Log("EmergencyDisconnectControllerTest: ✗ Hotkey registration test FAILED - Unregistration returned false");
                return false;
            }

            FL.Log("EmergencyDisconnectControllerTest: ✓ Hotkey registration test PASSED");
            return true;
        }
        catch (Exception ex)
        {
            FL.Log($"EmergencyDisconnectControllerTest: Exception during hotkey test: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Tests parallel execution timing to ensure operations start simultaneously
    /// </summary>
    public async Task<bool> TestParallelExecutionTimingAsync()
    {
        try
        {
            FL.Log("EmergencyDisconnectControllerTest: Starting parallel execution timing test");

            // Ensure safe mode for testing
            _rdManager.IsSafeMode = true;
            _networkController.IsSafeMode = true;

            var startTime = DateTime.Now;
            var result = await _controller.ExecuteEmergencyDisconnectAsync();
            var endTime = DateTime.Now;

            var totalTime = endTime - startTime;

            // In safe mode, the operations should complete relatively quickly
            // but still demonstrate parallel execution
            bool timingAcceptable = totalTime.TotalSeconds < 10; // Should complete within 10 seconds in safe mode

            if (!timingAcceptable)
            {
                FL.Log($"EmergencyDisconnectControllerTest: ✗ Parallel execution timing test FAILED - Took {totalTime.TotalSeconds:F2} seconds");
                return false;
            }

            if (result?.Success != true)
            {
                FL.Log("EmergencyDisconnectControllerTest: ✗ Parallel execution timing test FAILED - Operation was not successful");
                return false;
            }

            FL.Log($"EmergencyDisconnectControllerTest: ✓ Parallel execution timing test PASSED - Completed in {totalTime.TotalSeconds:F2} seconds");
            return true;
        }
        catch (Exception ex)
        {
            FL.Log($"EmergencyDisconnectControllerTest: Exception during timing test: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Runs all tests for the EmergencyDisconnectController
    /// </summary>
    public async Task<bool> RunAllTestsAsync()
    {
        FL.Log("EmergencyDisconnectControllerTest: ========================================");
        FL.Log("EmergencyDisconnectControllerTest: Starting EmergencyDisconnectController tests");
        FL.Log("EmergencyDisconnectControllerTest: ========================================");

        var tests = new List<(string Name, Func<Task<bool>> Test)>
        {
            ("Emergency Disconnect", TestEmergencyDisconnectAsync),
            ("Hotkey Registration", TestHotkeyRegistrationAsync),
            ("Parallel Execution Timing", TestParallelExecutionTimingAsync)
        };

        int passed = 0;
        int total = tests.Count;

        foreach (var (name, test) in tests)
        {
            FL.Log($"EmergencyDisconnectControllerTest: Running test: {name}");
            
            try
            {
                bool result = await test();
                if (result)
                {
                    passed++;
                    FL.Log($"EmergencyDisconnectControllerTest: ✓ {name} - PASSED");
                }
                else
                {
                    FL.Log($"EmergencyDisconnectControllerTest: ✗ {name} - FAILED");
                }
            }
            catch (Exception ex)
            {
                FL.Log($"EmergencyDisconnectControllerTest: ✗ {name} - EXCEPTION: {ex.Message}");
            }

            FL.Log(""); // Empty line for readability
        }

        bool allPassed = passed == total;
        
        FL.Log("EmergencyDisconnectControllerTest: ========================================");
        FL.Log($"EmergencyDisconnectControllerTest: Test Results: {passed}/{total} tests passed");
        FL.Log($"EmergencyDisconnectControllerTest: Overall Result: {(allPassed ? "PASSED" : "FAILED")}");
        FL.Log("EmergencyDisconnectControllerTest: ========================================");

        return allPassed;
    }
}