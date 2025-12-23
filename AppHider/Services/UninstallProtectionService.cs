using System.Diagnostics;
using System.IO;

namespace AppHider.Services;

/// <summary>
/// Service that provides anti-uninstall protection by keeping files locked
/// and requiring password authentication for uninstallation.
/// </summary>
public class UninstallProtectionService
{
    private readonly IAuthenticationService _authService;
    private readonly IAutoStartupService _autoStartupService;
    private readonly IDirectoryHidingService? _directoryHidingService;
    private FileStream? _lockFileStream;
    private readonly string _lockFilePath;
    private readonly object _lock = new object();

    public UninstallProtectionService(
        IAuthenticationService authService, 
        IAutoStartupService autoStartupService,
        IDirectoryHidingService? directoryHidingService = null)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _autoStartupService = autoStartupService ?? throw new ArgumentNullException(nameof(autoStartupService));
        _directoryHidingService = directoryHidingService;
        
        // Create lock file in the application directory
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        _lockFilePath = Path.Combine(appDirectory, ".applock");
    }

    /// <summary>
    /// Starts file protection by creating and locking a file in the application directory.
    /// This prevents the directory from being deleted while the application is running.
    /// </summary>
    public void StartFileProtection()
    {
        lock (_lock)
        {
            if (_lockFileStream != null)
            {
                Debug.WriteLine("File protection is already active.");
                return;
            }

            try
            {
                // Create and lock a file to prevent directory deletion
                _lockFileStream = new FileStream(
                    _lockFilePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None, // Exclusive access - no other process can access
                    4096,
                    FileOptions.DeleteOnClose); // Auto-delete when closed

                Debug.WriteLine($"File protection started: {_lockFilePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting file protection: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// Stops file protection by releasing the locked file.
    /// </summary>
    public void StopFileProtection()
    {
        lock (_lock)
        {
            if (_lockFileStream != null)
            {
                try
                {
                    _lockFileStream.Close();
                    _lockFileStream.Dispose();
                    _lockFileStream = null;
                    Debug.WriteLine("File protection stopped.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error stopping file protection: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Validates if the user is authorized to uninstall the application.
    /// Requires password authentication.
    /// </summary>
    /// <param name="password">The password to validate</param>
    /// <returns>True if the password is correct and uninstall is authorized</returns>
    public async Task<bool> ValidateUninstallAuthorizationAsync(string password)
    {
        try
        {
            var isValid = await _authService.ValidatePasswordAsync(password);
            
            if (isValid)
            {
                Debug.WriteLine("Uninstall authorization granted.");
            }
            else
            {
                Debug.WriteLine("Uninstall authorization denied - invalid password.");
            }

            return isValid;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error validating uninstall authorization: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Prepares the application for uninstallation by releasing all locks.
    /// Should only be called after successful authentication.
    /// </summary>
    public async Task PrepareForUninstallAsync()
    {
        Debug.WriteLine("Preparing for uninstall...");
        
        // Stop file protection
        StopFileProtection();

        // Unhide installation directory
        if (_directoryHidingService != null)
        {
            try
            {
                var success = await _directoryHidingService.UnhideInstallationDirectoryAsync();
                if (success)
                {
                    Debug.WriteLine("Installation directory unhidden successfully.");
                }
                else
                {
                    Debug.WriteLine("Failed to unhide installation directory.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error unhiding installation directory: {ex.Message}");
            }
        }

        // Unregister auto-startup
        try
        {
            var success = await _autoStartupService.UnregisterAutoStartupAsync();
            if (success)
            {
                Debug.WriteLine("Auto-startup unregistered successfully.");
            }
            else
            {
                Debug.WriteLine("Failed to unregister auto-startup.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error unregistering auto-startup: {ex.Message}");
        }
        
        Debug.WriteLine("Application prepared for uninstall.");
    }

    /// <summary>
    /// Gets whether file protection is currently active.
    /// </summary>
    public bool IsFileProtectionActive
    {
        get
        {
            lock (_lock)
            {
                return _lockFileStream != null;
            }
        }
    }
}
