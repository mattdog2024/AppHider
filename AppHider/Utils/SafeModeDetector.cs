using System.IO;

namespace AppHider.Utils;

/// <summary>
/// Detects and manages safe mode configuration for the application.
/// Safe mode prevents actual network modifications during development.
/// </summary>
public static class SafeModeDetector
{
    private const string SAFE_MODE_FLAG_FILE = "safe_mode.flag";
    private static readonly string AppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AppHider"
    );

    /// <summary>
    /// Detects if safe mode should be enabled based on flag file or command-line arguments.
    /// </summary>
    /// <param name="commandLineArgs">Command-line arguments passed to the application</param>
    /// <returns>True if safe mode should be enabled, false otherwise</returns>
    public static bool DetectSafeMode(string[] commandLineArgs)
    {
        // Check command-line argument first (highest priority)
        if (HasSafeModeCommandLineArg(commandLineArgs))
        {
            return true;
        }

        // Check for flag file
        if (SafeModeFlagFileExists())
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if the --safe-mode command-line argument is present.
    /// </summary>
    private static bool HasSafeModeCommandLineArg(string[] args)
    {
        return args.Any(arg => 
            arg.Equals("--safe-mode", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("/safe-mode", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if the safe mode flag file exists in the AppData directory.
    /// </summary>
    private static bool SafeModeFlagFileExists()
    {
        try
        {
            var flagFilePath = GetFlagFilePath();
            return File.Exists(flagFilePath);
        }
        catch
        {
            // If we can't check the file, assume safe mode is off
            return false;
        }
    }

    /// <summary>
    /// Gets the full path to the safe mode flag file.
    /// </summary>
    public static string GetFlagFilePath()
    {
        // Ensure directory exists
        Directory.CreateDirectory(AppDataPath);
        return Path.Combine(AppDataPath, SAFE_MODE_FLAG_FILE);
    }

    /// <summary>
    /// Creates the safe mode flag file.
    /// </summary>
    public static void CreateFlagFile()
    {
        try
        {
            var flagFilePath = GetFlagFilePath();
            File.WriteAllText(flagFilePath, $"Safe mode enabled at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to create safe mode flag file", ex);
        }
    }

    /// <summary>
    /// Deletes the safe mode flag file.
    /// </summary>
    public static void DeleteFlagFile()
    {
        try
        {
            var flagFilePath = GetFlagFilePath();
            if (File.Exists(flagFilePath))
            {
                File.Delete(flagFilePath);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to delete safe mode flag file", ex);
        }
    }
}
