using System.Diagnostics;
using AppHider.Services;

namespace AppHider.Utils;

/// <summary>
/// Simple test class for verifying watchdog and uninstall protection functionality.
/// This is not a formal unit test but a manual verification tool.
/// </summary>
public static class WatchdogTest
{
    /// <summary>
    /// Tests the watchdog service by starting and stopping it.
    /// </summary>
    public static async Task TestWatchdogServiceAsync()
    {
        Debug.WriteLine("=== Testing Watchdog Service ===");
        
        var watchdog = new WatchdogService();
        
        try
        {
            // Test 1: Start watchdog
            Debug.WriteLine("Test 1: Starting watchdog...");
            await watchdog.StartWatchdogAsync();
            
            if (watchdog.IsWatchdogRunning)
            {
                Debug.WriteLine("✓ Watchdog started successfully");
            }
            else
            {
                Debug.WriteLine("✗ Watchdog failed to start");
                return;
            }
            
            // Wait a bit to let heartbeats flow
            await Task.Delay(10000);
            
            // Test 2: Check if watchdog is still running
            Debug.WriteLine("Test 2: Checking if watchdog is still running...");
            if (watchdog.IsWatchdogRunning)
            {
                Debug.WriteLine("✓ Watchdog is still running");
            }
            else
            {
                Debug.WriteLine("✗ Watchdog stopped unexpectedly");
            }
            
            // Test 3: Stop watchdog
            Debug.WriteLine("Test 3: Stopping watchdog...");
            await watchdog.StopWatchdogAsync();
            
            if (!watchdog.IsWatchdogRunning)
            {
                Debug.WriteLine("✓ Watchdog stopped successfully");
            }
            else
            {
                Debug.WriteLine("✗ Watchdog failed to stop");
            }
            
            Debug.WriteLine("=== Watchdog Service Test Complete ===");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"✗ Test failed with exception: {ex.Message}");
            Debug.WriteLine(ex.StackTrace);
        }
    }
    
    /// <summary>
    /// Tests the uninstall protection service.
    /// </summary>
    public static async Task TestUninstallProtectionAsync(IAuthenticationService authService, IAutoStartupService autoStartupService)
    {
        Debug.WriteLine("=== Testing Uninstall Protection Service ===");
        
        var uninstallProtection = new UninstallProtectionService(authService, autoStartupService);
        
        try
        {
            // Test 1: Start file protection
            Debug.WriteLine("Test 1: Starting file protection...");
            uninstallProtection.StartFileProtection();
            
            if (uninstallProtection.IsFileProtectionActive)
            {
                Debug.WriteLine("✓ File protection started successfully");
            }
            else
            {
                Debug.WriteLine("✗ File protection failed to start");
                return;
            }
            
            // Test 2: Try to validate with wrong password
            Debug.WriteLine("Test 2: Testing with invalid password...");
            var result = await uninstallProtection.ValidateUninstallAuthorizationAsync("wrongpassword");
            
            if (!result)
            {
                Debug.WriteLine("✓ Invalid password correctly rejected");
            }
            else
            {
                Debug.WriteLine("✗ Invalid password was accepted (this should not happen)");
            }
            
            // Test 3: Stop file protection
            Debug.WriteLine("Test 3: Stopping file protection...");
            uninstallProtection.StopFileProtection();
            
            if (!uninstallProtection.IsFileProtectionActive)
            {
                Debug.WriteLine("✓ File protection stopped successfully");
            }
            else
            {
                Debug.WriteLine("✗ File protection failed to stop");
            }
            
            Debug.WriteLine("=== Uninstall Protection Service Test Complete ===");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"✗ Test failed with exception: {ex.Message}");
            Debug.WriteLine(ex.StackTrace);
        }
    }
}
