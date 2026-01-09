using System;
using System.Threading.Tasks;
using AppHider.Utils;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting Remote Desktop Management Tests...");
        
        try
        {
            var testsPassed = await RemoteDesktopTestRunner.RunAllTestsAsync();
            Console.WriteLine($"Tests completed. Result: {(testsPassed ? "PASSED" : "FAILED")}");
            Environment.Exit(testsPassed ? 0 : 1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Critical error running tests: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}