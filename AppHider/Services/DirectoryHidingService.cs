using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace AppHider.Services;

/// <summary>
/// Service for hiding the application installation directory from normal file browsing.
/// Implements Requirement 9.5: Hide installation directory using hidden and system attributes.
/// </summary>
public class DirectoryHidingService : IDirectoryHidingService
{
    private readonly string _installationDirectory;

    public DirectoryHidingService()
    {
        // Get the directory where the executable is located
        var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(executablePath))
        {
            throw new InvalidOperationException("Could not determine executable path");
        }

        _installationDirectory = Path.GetDirectoryName(executablePath) 
            ?? throw new InvalidOperationException("Could not determine installation directory");
    }

    /// <summary>
    /// Hides the installation directory by setting hidden and system attributes.
    /// Requirement 9.5: Hide installation directory from normal file browsing.
    /// </summary>
    public async Task<bool> HideInstallationDirectoryAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                // Check if we're running as administrator
                if (!IsRunningAsAdministrator())
                {
                    Debug.WriteLine("Directory hiding requires administrator privileges.");
                    return false;
                }

                // Check if directory exists
                if (!Directory.Exists(_installationDirectory))
                {
                    Debug.WriteLine($"Installation directory does not exist: {_installationDirectory}");
                    return false;
                }

                // Get current attributes
                var currentAttributes = File.GetAttributes(_installationDirectory);

                // Add Hidden and System attributes
                var newAttributes = currentAttributes | FileAttributes.Hidden | FileAttributes.System;

                // Set the new attributes
                File.SetAttributes(_installationDirectory, newAttributes);

                Debug.WriteLine($"Installation directory hidden successfully: {_installationDirectory}");
                Debug.WriteLine($"Attributes set: {newAttributes}");

                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"Unauthorized access when hiding directory: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error hiding installation directory: {ex.Message}");
                return false;
            }
        });
    }

    /// <summary>
    /// Unhides the installation directory by removing hidden and system attributes.
    /// </summary>
    public async Task<bool> UnhideInstallationDirectoryAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                // Check if we're running as administrator
                if (!IsRunningAsAdministrator())
                {
                    Debug.WriteLine("Directory unhiding requires administrator privileges.");
                    return false;
                }

                // Check if directory exists
                if (!Directory.Exists(_installationDirectory))
                {
                    Debug.WriteLine($"Installation directory does not exist: {_installationDirectory}");
                    return false;
                }

                // Get current attributes
                var currentAttributes = File.GetAttributes(_installationDirectory);

                // Remove Hidden and System attributes
                var newAttributes = currentAttributes & ~FileAttributes.Hidden & ~FileAttributes.System;

                // Set the new attributes
                File.SetAttributes(_installationDirectory, newAttributes);

                Debug.WriteLine($"Installation directory unhidden successfully: {_installationDirectory}");
                Debug.WriteLine($"Attributes set: {newAttributes}");

                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"Unauthorized access when unhiding directory: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error unhiding installation directory: {ex.Message}");
                return false;
            }
        });
    }

    /// <summary>
    /// Checks if the installation directory is currently hidden.
    /// </summary>
    public async Task<bool> IsInstallationDirectoryHiddenAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                // Check if directory exists
                if (!Directory.Exists(_installationDirectory))
                {
                    Debug.WriteLine($"Installation directory does not exist: {_installationDirectory}");
                    return false;
                }

                // Get current attributes
                var attributes = File.GetAttributes(_installationDirectory);

                // Check if both Hidden and System attributes are set
                var isHidden = (attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
                var isSystem = (attributes & FileAttributes.System) == FileAttributes.System;

                return isHidden && isSystem;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking directory hidden status: {ex.Message}");
                return false;
            }
        });
    }

    /// <summary>
    /// Checks if the current process is running with administrator privileges.
    /// </summary>
    private bool IsRunningAsAdministrator()
    {
        try
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
