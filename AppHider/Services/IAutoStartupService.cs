namespace AppHider.Services;

/// <summary>
/// Service for managing application auto-startup using Windows Task Scheduler.
/// </summary>
public interface IAutoStartupService
{
    /// <summary>
    /// Registers the application to start automatically at Windows startup.
    /// </summary>
    /// <returns>True if registration was successful, false otherwise.</returns>
    Task<bool> RegisterAutoStartupAsync();

    /// <summary>
    /// Unregisters the application from auto-startup.
    /// </summary>
    /// <returns>True if unregistration was successful, false otherwise.</returns>
    Task<bool> UnregisterAutoStartupAsync();

    /// <summary>
    /// Checks if auto-startup is currently registered.
    /// </summary>
    /// <returns>True if auto-startup is registered, false otherwise.</returns>
    Task<bool> IsAutoStartupRegisteredAsync();
}
