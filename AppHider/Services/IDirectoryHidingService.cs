namespace AppHider.Services;

/// <summary>
/// Service for hiding the application installation directory from normal file browsing.
/// </summary>
public interface IDirectoryHidingService
{
    /// <summary>
    /// Hides the installation directory by setting hidden and system attributes.
    /// </summary>
    /// <returns>True if the directory was successfully hidden, false otherwise.</returns>
    Task<bool> HideInstallationDirectoryAsync();

    /// <summary>
    /// Unhides the installation directory by removing hidden and system attributes.
    /// </summary>
    /// <returns>True if the directory was successfully unhidden, false otherwise.</returns>
    Task<bool> UnhideInstallationDirectoryAsync();

    /// <summary>
    /// Checks if the installation directory is currently hidden.
    /// </summary>
    /// <returns>True if the directory is hidden, false otherwise.</returns>
    Task<bool> IsInstallationDirectoryHiddenAsync();
}
