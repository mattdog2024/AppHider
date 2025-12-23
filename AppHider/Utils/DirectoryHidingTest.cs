using System.Diagnostics;
using System.IO;
using AppHider.Services;

namespace AppHider.Utils;

/// <summary>
/// Test utility for verifying directory hiding functionality.
/// This is a manual test utility to verify Requirement 9.5.
/// </summary>
public static class DirectoryHidingTest
{
    /// <summary>
    /// Tests the directory hiding functionality by hiding and then unhiding the installation directory.
    /// </summary>
    public static async Task RunTestAsync()
    {
        Debug.WriteLine("=== Directory Hiding Test ===");
        Console.WriteLine("=== Directory Hiding Test ===");

        var service = new DirectoryHidingService();

        // Test 1: Check initial state
        Debug.WriteLine("\nTest 1: Checking initial directory state...");
        Console.WriteLine("\nTest 1: Checking initial directory state...");
        var initiallyHidden = await service.IsInstallationDirectoryHiddenAsync();
        Debug.WriteLine($"Directory initially hidden: {initiallyHidden}");
        Console.WriteLine($"Directory initially hidden: {initiallyHidden}");

        // Test 2: Hide the directory
        Debug.WriteLine("\nTest 2: Hiding installation directory...");
        Console.WriteLine("\nTest 2: Hiding installation directory...");
        var hideSuccess = await service.HideInstallationDirectoryAsync();
        Debug.WriteLine($"Hide operation success: {hideSuccess}");
        Console.WriteLine($"Hide operation success: {hideSuccess}");

        if (hideSuccess)
        {
            // Verify it's hidden
            var isHidden = await service.IsInstallationDirectoryHiddenAsync();
            Debug.WriteLine($"Directory is now hidden: {isHidden}");
            Console.WriteLine($"Directory is now hidden: {isHidden}");

            // Get the installation directory path
            var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
            var installDir = Path.GetDirectoryName(executablePath);
            
            if (!string.IsNullOrEmpty(installDir))
            {
                var attributes = File.GetAttributes(installDir);
                Debug.WriteLine($"Directory attributes: {attributes}");
                Console.WriteLine($"Directory attributes: {attributes}");
                
                var hasHidden = (attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
                var hasSystem = (attributes & FileAttributes.System) == FileAttributes.System;
                Debug.WriteLine($"Has Hidden attribute: {hasHidden}");
                Debug.WriteLine($"Has System attribute: {hasSystem}");
                Console.WriteLine($"Has Hidden attribute: {hasHidden}");
                Console.WriteLine($"Has System attribute: {hasSystem}");
            }
        }

        // Test 3: Unhide the directory
        Debug.WriteLine("\nTest 3: Unhiding installation directory...");
        Console.WriteLine("\nTest 3: Unhiding installation directory...");
        var unhideSuccess = await service.UnhideInstallationDirectoryAsync();
        Debug.WriteLine($"Unhide operation success: {unhideSuccess}");
        Console.WriteLine($"Unhide operation success: {unhideSuccess}");

        if (unhideSuccess)
        {
            // Verify it's not hidden
            var isHidden = await service.IsInstallationDirectoryHiddenAsync();
            Debug.WriteLine($"Directory is now hidden: {isHidden}");
            Console.WriteLine($"Directory is now hidden: {isHidden}");

            // Get the installation directory path
            var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
            var installDir = Path.GetDirectoryName(executablePath);
            
            if (!string.IsNullOrEmpty(installDir))
            {
                var attributes = File.GetAttributes(installDir);
                Debug.WriteLine($"Directory attributes: {attributes}");
                Console.WriteLine($"Directory attributes: {attributes}");
            }
        }

        Debug.WriteLine("\n=== Test Complete ===");
        Console.WriteLine("\n=== Test Complete ===");
    }
}
