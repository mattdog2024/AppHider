# Directory Hiding Usage Guide

## Overview

The Directory Hiding feature implements **Requirement 9.5** by hiding the application's installation directory from normal file browsing. This is achieved by setting both the `Hidden` and `System` file attributes on the installation directory.

## How It Works

### Automatic Hiding on Startup

The application automatically attempts to hide its installation directory when it starts:

1. The `DirectoryHidingService` is initialized during application startup
2. The service sets the `Hidden` and `System` attributes on the installation directory
3. This makes the directory invisible in Windows Explorer unless "Show hidden files" AND "Show protected operating system files" are both enabled

### Automatic Unhiding on Uninstall

When the user initiates an uninstall (after password authentication):

1. The `UninstallProtectionService` calls the directory hiding service
2. The directory is unhidden by removing the `Hidden` and `System` attributes
3. This allows the uninstaller to access and remove the directory

## Technical Details

### File Attributes Set

- **Hidden**: Makes the directory invisible in normal file browsing
- **System**: Marks the directory as a system directory, providing additional protection

### Requirements

- **Administrator Privileges**: Setting system attributes requires administrator rights
- The application must be running with elevated privileges for this feature to work

## Testing the Feature

### Manual Test

You can test the directory hiding functionality using the `DirectoryHidingTest` utility:

```csharp
// In your code or a test harness
await DirectoryHidingTest.RunTestAsync();
```

This will:
1. Check the initial state of the directory
2. Hide the directory and verify the attributes
3. Unhide the directory and verify the attributes are removed

### Verification Steps

1. **Before Hiding**:
   - Navigate to the installation directory in Windows Explorer
   - The directory should be visible

2. **After Hiding**:
   - The directory should disappear from Windows Explorer
   - To see it, you must enable both:
     - View → Show → Hidden items
     - View → Options → View tab → Uncheck "Hide protected operating system files"

3. **After Unhiding**:
   - The directory should be visible again in normal browsing

## API Reference

### IDirectoryHidingService

```csharp
public interface IDirectoryHidingService
{
    // Hides the installation directory
    Task<bool> HideInstallationDirectoryAsync();
    
    // Unhides the installation directory
    Task<bool> UnhideInstallationDirectoryAsync();
    
    // Checks if the directory is currently hidden
    Task<bool> IsInstallationDirectoryHiddenAsync();
}
```

### DirectoryHidingService

The implementation automatically detects the installation directory from the running executable's location.

## Security Considerations

1. **Not Foolproof**: This is an obfuscation technique, not true security
2. **Requires Admin Rights**: The feature only works when running as administrator
3. **Easily Reversible**: Users with admin rights can unhide the directory
4. **Combined Protection**: Works best when combined with other protection mechanisms like:
   - File locking (UninstallProtectionService)
   - Watchdog process
   - Password protection

## Troubleshooting

### Directory Not Hiding

**Symptom**: The directory remains visible after startup

**Possible Causes**:
1. Application not running with administrator privileges
2. Antivirus software blocking attribute changes
3. Directory permissions preventing attribute modification

**Solution**:
- Ensure the application is running as administrator
- Check the Debug output for error messages
- Verify the application manifest requires administrator elevation

### Directory Not Unhiding on Uninstall

**Symptom**: Directory remains hidden after uninstall preparation

**Possible Causes**:
1. UninstallProtectionService not receiving the DirectoryHidingService instance
2. Administrator privileges lost during uninstall process

**Solution**:
- Verify the service is properly injected in App.xaml.cs
- Check Debug output for error messages during uninstall preparation

## Implementation Notes

### Integration Points

1. **App.xaml.cs**: Service initialization and automatic hiding on startup
2. **UninstallProtectionService**: Automatic unhiding during uninstall preparation
3. **DirectoryHidingTest**: Manual testing utility

### Code Example

```csharp
// Initialize the service
var directoryHidingService = new DirectoryHidingService();

// Hide the directory
var success = await directoryHidingService.HideInstallationDirectoryAsync();
if (success)
{
    Console.WriteLine("Directory hidden successfully");
}

// Check if hidden
var isHidden = await directoryHidingService.IsInstallationDirectoryHiddenAsync();
Console.WriteLine($"Directory is hidden: {isHidden}");

// Unhide the directory
success = await directoryHidingService.UnhideInstallationDirectoryAsync();
if (success)
{
    Console.WriteLine("Directory unhidden successfully");
}
```

## Related Requirements

- **Requirement 9.5**: Hide installation directory from normal file browsing
- **Requirement 9.4**: Require password authentication before uninstallation
- **Requirement 9.3**: Protect files by keeping them in use

## See Also

- [Watchdog Usage Guide](WATCHDOG_USAGE.md)
- [Auto Startup Usage Guide](AUTO_STARTUP_USAGE.md)
- [Safe Mode Usage Guide](SAFE_MODE_USAGE.md)
