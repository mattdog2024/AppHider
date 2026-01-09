using System;
using System.Threading.Tasks;

namespace AppHider.Utils;

/// <summary>
/// Comprehensive test runner for all manual tests in the application.
/// This runs all available test utilities to verify functionality.
/// </summary>
public static class TestRunner
{
    /// <summary>
    /// Runs all available tests and reports results.
    /// </summary>
    public static async Task RunAllTestsAsync()
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║         AppHider Comprehensive Test Suite                 ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        int totalTests = 0;
        int passedTests = 0;

        // Test 1: Safe Mode Detection
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("Test Suite 1: Safe Mode Detection");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        try
        {
            SafeModeDetectorTest.RunTests();
            passedTests++;
            Console.WriteLine("✓ Safe Mode Detection Tests: PASSED");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Safe Mode Detection Tests: FAILED - {ex.Message}");
        }
        totalTests++;
        Console.WriteLine();

        // Test 2: Background Mode
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("Test Suite 2: Background Mode");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        try
        {
            BackgroundModeTest.TestWindowHideShow();
            BackgroundModeTest.TestHotkeyShowWindow();
            passedTests++;
            Console.WriteLine("✓ Background Mode Tests: PASSED");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Background Mode Tests: FAILED - {ex.Message}");
        }
        totalTests++;
        Console.WriteLine();

        // Test 3: Directory Hiding
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("Test Suite 3: Directory Hiding");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        try
        {
            await DirectoryHidingTest.RunTestAsync();
            passedTests++;
            Console.WriteLine("✓ Directory Hiding Tests: PASSED");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Directory Hiding Tests: FAILED - {ex.Message}");
        }
        totalTests++;
        Console.WriteLine();

        // Test 4: Hotkey Independence
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("Test Suite 4: Hotkey Independence");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        try
        {
            HotkeyIndependenceTest.RunAllTests();
            passedTests++;
            Console.WriteLine("✓ Hotkey Independence Tests: PASSED");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Hotkey Independence Tests: FAILED - {ex.Message}");
        }
        totalTests++;
        Console.WriteLine();

        // Test 5: Emergency Disconnect Hotkey
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("Test Suite 5: Emergency Disconnect Hotkey");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        try
        {
            EmergencyDisconnectHotkeyTest.RunAllTests();
            passedTests++;
            Console.WriteLine("✓ Emergency Disconnect Hotkey Tests: PASSED");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Emergency Disconnect Hotkey Tests: FAILED - {ex.Message}");
        }
        totalTests++;
        Console.WriteLine();

        // Test 6: Restart Consistency
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("Test Suite 6: Restart Consistency");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        try
        {
            await RestartConsistencyTest.RunAllTestsAsync();
            passedTests++;
            Console.WriteLine("✓ Restart Consistency Tests: PASSED");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Restart Consistency Tests: FAILED - {ex.Message}");
        }
        totalTests++;
        Console.WriteLine();

        // Test 8: Remote Desktop Safe Mode
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("Test Suite 8: Remote Desktop Safe Mode");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        try
        {
            await SafeModeRemoteDesktopTest.RunTests();
            passedTests++;
            Console.WriteLine("✓ Remote Desktop Safe Mode Tests: PASSED");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Remote Desktop Safe Mode Tests: FAILED - {ex.Message}");
        }
        totalTests++;
        Console.WriteLine();

        // Test 9: Remote Desktop Error Handling
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("Test Suite 9: Remote Desktop Error Handling");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        try
        {
            await ErrorHandlingRemoteDesktopTest.RunAllTestsAsync();
            passedTests++;
            Console.WriteLine("✓ Remote Desktop Error Handling Tests: PASSED");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Remote Desktop Error Handling Tests: FAILED - {ex.Message}");
        }
        totalTests++;
        Console.WriteLine();

        // Test 10: Performance Optimization
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("Test Suite 10: Performance Optimization");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        try
        {
            await PerformanceOptimizationTest.RunAllTestsAsync();
            passedTests++;
            Console.WriteLine("✓ Performance Optimization Tests: PASSED");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Performance Optimization Tests: FAILED - {ex.Message}");
        }
        totalTests++;
        Console.WriteLine();

        // Test 11: Remote Desktop Integration (requires full service context)
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("Test Suite 11: Remote Desktop Integration");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("⚠ Integration tests require full application context");
        Console.WriteLine("⚠ Run RemoteDesktopIntegrationTest manually with service injection");
        Console.WriteLine("✓ Remote Desktop Integration Tests: SKIPPED (manual test available)");
        totalTests++;
        Console.WriteLine();

        // Test 12: Watchdog Service (requires services to be initialized)
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("Test Suite 12: Watchdog Service");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("⚠ Watchdog tests require full application context");
        Console.WriteLine("⚠ Run WatchdogTest.TestWatchdogServiceAsync() manually if needed");
        Console.WriteLine("✓ Watchdog Service Tests: SKIPPED (manual test available)");
        totalTests++;
        Console.WriteLine();

        // Summary
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    Test Summary                            ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine($"Total Test Suites: {totalTests}");
        Console.WriteLine($"Passed: {passedTests}");
        Console.WriteLine($"Failed: {totalTests - passedTests}");
        Console.WriteLine($"Success Rate: {(passedTests * 100.0 / totalTests):F1}%");
        Console.WriteLine();

        if (passedTests == totalTests)
        {
            Console.WriteLine("✓ ALL TESTS PASSED!");
        }
        else
        {
            Console.WriteLine($"⚠ {totalTests - passedTests} test suite(s) failed");
        }
    }
}
