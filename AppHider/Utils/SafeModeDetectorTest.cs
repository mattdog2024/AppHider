using System;
using System.IO;

namespace AppHider.Utils;

/// <summary>
/// Simple test class to verify SafeModeDetector functionality.
/// This can be run manually to test safe mode detection.
/// </summary>
public static class SafeModeDetectorTest
{
    public static void RunTests()
    {
        Console.WriteLine("=== SafeModeDetector Tests ===\n");

        TestCommandLineDetection();
        TestFlagFileDetection();
        TestFlagFileCreationAndDeletion();
        TestPriority();

        Console.WriteLine("\n=== All Tests Completed ===");
    }

    private static void TestCommandLineDetection()
    {
        Console.WriteLine("Test 1: Command-line argument detection");
        
        // Test with --safe-mode
        var args1 = new[] { "--safe-mode" };
        var result1 = SafeModeDetector.DetectSafeMode(args1);
        Console.WriteLine($"  Args: [--safe-mode] => {result1} (Expected: True)");
        
        // Test with /safe-mode
        var args2 = new[] { "/safe-mode" };
        var result2 = SafeModeDetector.DetectSafeMode(args2);
        Console.WriteLine($"  Args: [/safe-mode] => {result2} (Expected: True)");
        
        // Test with mixed case
        var args3 = new[] { "--SAFE-MODE" };
        var result3 = SafeModeDetector.DetectSafeMode(args3);
        Console.WriteLine($"  Args: [--SAFE-MODE] => {result3} (Expected: True)");
        
        // Test without safe mode arg
        var args4 = new[] { "--other-arg" };
        var result4 = SafeModeDetector.DetectSafeMode(args4);
        Console.WriteLine($"  Args: [--other-arg] => {result4} (Expected: False)");
        
        // Test with empty args
        var args5 = Array.Empty<string>();
        var result5 = SafeModeDetector.DetectSafeMode(args5);
        Console.WriteLine($"  Args: [] => {result5} (Expected: False)");
        
        Console.WriteLine();
    }

    private static void TestFlagFileDetection()
    {
        Console.WriteLine("Test 2: Flag file detection");
        
        // Ensure flag file doesn't exist
        try
        {
            SafeModeDetector.DeleteFlagFile();
        }
        catch { }
        
        // Test without flag file
        var result1 = SafeModeDetector.DetectSafeMode(Array.Empty<string>());
        Console.WriteLine($"  No flag file => {result1} (Expected: False)");
        
        // Create flag file
        SafeModeDetector.CreateFlagFile();
        Console.WriteLine($"  Flag file created at: {SafeModeDetector.GetFlagFilePath()}");
        
        // Test with flag file
        var result2 = SafeModeDetector.DetectSafeMode(Array.Empty<string>());
        Console.WriteLine($"  With flag file => {result2} (Expected: True)");
        
        // Clean up
        SafeModeDetector.DeleteFlagFile();
        Console.WriteLine($"  Flag file deleted");
        
        Console.WriteLine();
    }

    private static void TestFlagFileCreationAndDeletion()
    {
        Console.WriteLine("Test 3: Flag file creation and deletion");
        
        var flagPath = SafeModeDetector.GetFlagFilePath();
        
        // Ensure clean state
        try
        {
            SafeModeDetector.DeleteFlagFile();
        }
        catch { }
        
        // Verify file doesn't exist
        var exists1 = File.Exists(flagPath);
        Console.WriteLine($"  Before creation: File exists = {exists1} (Expected: False)");
        
        // Create file
        SafeModeDetector.CreateFlagFile();
        var exists2 = File.Exists(flagPath);
        Console.WriteLine($"  After creation: File exists = {exists2} (Expected: True)");
        
        // Delete file
        SafeModeDetector.DeleteFlagFile();
        var exists3 = File.Exists(flagPath);
        Console.WriteLine($"  After deletion: File exists = {exists3} (Expected: False)");
        
        Console.WriteLine();
    }

    private static void TestPriority()
    {
        Console.WriteLine("Test 4: Command-line argument priority over flag file");
        
        // Ensure flag file doesn't exist
        try
        {
            SafeModeDetector.DeleteFlagFile();
        }
        catch { }
        
        // Test command-line arg without flag file
        var result1 = SafeModeDetector.DetectSafeMode(new[] { "--safe-mode" });
        Console.WriteLine($"  Command-line only => {result1} (Expected: True)");
        
        // Create flag file
        SafeModeDetector.CreateFlagFile();
        
        // Test with both (command-line should still work)
        var result2 = SafeModeDetector.DetectSafeMode(new[] { "--safe-mode" });
        Console.WriteLine($"  Both present => {result2} (Expected: True)");
        
        // Test with only flag file
        var result3 = SafeModeDetector.DetectSafeMode(Array.Empty<string>());
        Console.WriteLine($"  Flag file only => {result3} (Expected: True)");
        
        // Clean up
        SafeModeDetector.DeleteFlagFile();
        
        Console.WriteLine();
    }
}
