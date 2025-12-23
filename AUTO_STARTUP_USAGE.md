# Auto-Startup Feature

## Overview

The auto-startup feature automatically registers the AppHider application to start with Windows using the Task Scheduler. This ensures the application is always running in the background to provide privacy protection.

## Implementation Details

### Task Scheduler Registration

The application uses Windows Task Scheduler to register itself for auto-startup with the following characteristics:

- **Task Name**: `SystemMaintenanceTask` (non-descriptive for stealth, as per Requirement 8.3)
- **Task Path**: `\Microsoft\Windows\SystemMaintenanceTask`
- **Trigger**: User logon (ONLOGON)
- **Run Level**: Highest privileges (HIGHEST)
- **Command**: Application executable with `--background` flag

### Key Features

1. **Automatic Registration**: The application automatically registers itself for auto-startup on first successful authentication.

2. **Background Mode**: When started via Task Scheduler, the application runs in background mode without showing any windows.

3. **Administrator Privileges**: The application runs with highest privileges to ensure it can perform network control and other system-level operations.

4. **Stealth**: Uses a non-descriptive task name to avoid easy identification.

5. **Uninstall Integration**: Auto-startup is automatically unregistered when the user authorizes uninstallation.

## Service Interface

### IAutoStartupService

```csharp
public interface IAutoStartupService
{
    Task<bool> RegisterAutoStartupAsync();
    Task<bool> UnregisterAutoStartupAsync();
    Task<bool> IsAutoStartupRegisteredAsync();
}
```

### Methods

- **RegisterAutoStartupAsync()**: Registers the application for auto-startup. Requires administrator privileges.
- **UnregisterAutoStartupAsync()**: Removes the auto-startup registration. Requires administrator privileges.
- **IsAutoStartupRegisteredAsync()**: Checks if auto-startup is currently registered.

## Usage

### Automatic Registration

The application automatically registers itself for auto-startup when:
1. The user successfully authenticates for the first time
2. Auto-startup is not already registered

### Manual Unregistration

Auto-startup is automatically unregistered when:
1. The user authorizes uninstallation through the UninstallDialog
2. The PrepareForUninstallAsync() method is called

### Checking Status

To check if auto-startup is registered:

```csharp
var autoStartupService = new AutoStartupService();
var isRegistered = await autoStartupService.IsAutoStartupRegisteredAsync();
```

## Requirements Satisfied

- **Requirement 8.1**: Application registers itself to start automatically with Windows using Task Scheduler with highest privileges
- **Requirement 8.3**: Uses a non-descriptive process/task name to avoid easy identification

## Technical Notes

### Administrator Privileges

The application must be running with administrator privileges to register or unregister auto-startup. The service checks for administrator privileges before attempting any operations.

### Task Scheduler Command

The service uses the `schtasks.exe` command-line tool to interact with Windows Task Scheduler:

- **Create Task**: `schtasks /Create /TN "path\name" /TR "executable --background" /SC ONLOGON /RL HIGHEST /F`
- **Delete Task**: `schtasks /Delete /TN "path\name" /F`
- **Query Task**: `schtasks /Query /TN "path\name"`

### Background Mode

When started via Task Scheduler, the application receives the `--background` command-line argument, which triggers background mode operation:
- No visible windows on startup
- Hotkey can be used to show the interface
- Closing the window returns to background mode instead of exiting

## Testing

To test the auto-startup feature:

1. **Check Registration Status**:
   ```csharp
   var isRegistered = await autoStartupService.IsAutoStartupRegisteredAsync();
   Console.WriteLine($"Auto-startup registered: {isRegistered}");
   ```

2. **Verify Task Scheduler**:
   - Open Task Scheduler (taskschd.msc)
   - Navigate to `Microsoft\Windows\`
   - Look for `SystemMaintenanceTask`
   - Verify it's configured to run at logon with highest privileges

3. **Test Auto-Start**:
   - Log out and log back in
   - The application should start automatically in background mode
   - Press the configured hotkey to verify the application is running

## Security Considerations

1. **Stealth**: The task name is non-descriptive to avoid easy identification
2. **Privileges**: Runs with highest privileges to ensure full functionality
3. **Background Mode**: Starts without visible windows to remain hidden
4. **Uninstall Protection**: Requires password authentication to unregister

## Troubleshooting

### Auto-Startup Not Working

1. **Check Administrator Privileges**: Ensure the application is running as administrator
2. **Check Task Scheduler**: Verify the task exists in Task Scheduler
3. **Check Logs**: Review debug output for error messages
4. **Manual Registration**: Try manually registering using the service

### Task Already Exists

If the task already exists, the `/F` flag forces overwrite, so this should not be an issue.

### Permission Denied

If you get permission denied errors, ensure:
1. The application is running as administrator
2. User Account Control (UAC) is not blocking the operation
3. The user has permission to create scheduled tasks
