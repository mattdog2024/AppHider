# Watchdog and Uninstall Protection Usage Guide

## Overview

This document describes the watchdog service and uninstall protection features implemented in AppHider.

## Watchdog Service

### Purpose
The watchdog service monitors the main application process and automatically restarts it if it crashes or is terminated unexpectedly.

### How It Works

1. **Process Monitoring**: When the application starts, it launches a separate watchdog process that monitors the main process.

2. **Heartbeat Mechanism**: The main process sends heartbeat signals to the watchdog every 5 seconds via named pipes.

3. **Failure Detection**: The watchdog detects failures in two ways:
   - The main process exits (detected via process monitoring)
   - Heartbeat timeout (no heartbeat received for 15 seconds)

4. **Automatic Restart**: When a failure is detected, the watchdog automatically restarts the main application in background mode.

### Implementation Details

- **Watchdog Process**: Started with `--watchdog-mode` command-line argument
- **Communication**: Uses Windows Named Pipes (`AppHider_Watchdog_Pipe`)
- **Heartbeat Interval**: 5 seconds
- **Heartbeat Timeout**: 15 seconds
- **Restart Mode**: Application is restarted in background mode (`--background`)

### Code Structure

```
AppHider/Services/WatchdogService.cs
├── StartWatchdogAsync()      - Starts the watchdog process
├── StopWatchdogAsync()        - Stops the watchdog process
├── SendHeartbeatsAsync()      - Sends periodic heartbeats
├── RunWatchdogModeAsync()     - Watchdog monitoring loop
└── RestartMainProcessAsync()  - Restarts the main process
```

### Testing

To manually test the watchdog:

1. Start the application normally
2. The watchdog will start automatically
3. Kill the main process via Task Manager
4. The watchdog should restart the application within a few seconds
5. Check the debug output for watchdog messages

### Limitations

- The watchdog itself can be terminated (no recursive watchdog protection)
- Requires administrator privileges to restart the application
- May not work if the executable is deleted or moved

## Uninstall Protection

### Purpose
Prevents unauthorized uninstallation by requiring password authentication and keeping application files locked.

### How It Works

1. **File Locking**: On startup, the application creates and locks a file (`.applock`) in its directory with exclusive access.

2. **Directory Protection**: While the lock file is held, the application directory cannot be deleted.

3. **Password Authentication**: To uninstall, users must:
   - Click "Authorize Uninstall" in the main window
   - Enter their password in the uninstall dialog
   - If authenticated, file protection is released

4. **Cleanup**: After successful authentication, the application releases all file locks and can be uninstalled normally.

### Implementation Details

- **Lock File**: `.applock` in application directory
- **File Options**: `FileShare.None` (exclusive access) + `DeleteOnClose`
- **Authentication**: Uses the same password system as the main application
- **Auto-cleanup**: Lock file is automatically deleted when the application exits

### Code Structure

```
AppHider/Services/UninstallProtectionService.cs
├── StartFileProtection()                    - Creates and locks the file
├── StopFileProtection()                     - Releases the file lock
├── ValidateUninstallAuthorizationAsync()    - Validates password
└── PrepareForUninstall()                    - Releases locks for uninstall

AppHider/Views/UninstallDialog.xaml[.cs]
└── Dialog for password authentication
```

### Usage Flow

1. **Normal Operation**:
   - Application starts → File protection activated
   - Lock file prevents directory deletion
   - Application runs normally

2. **Uninstall Process**:
   - User clicks "Authorize Uninstall" button
   - Uninstall dialog appears
   - User enters password
   - If correct: File protection released, uninstall can proceed
   - If incorrect: Protection remains active

3. **Application Exit**:
   - File protection automatically released
   - Lock file automatically deleted

### Testing

To test uninstall protection:

1. Start the application
2. Try to delete the application directory → Should fail (file in use)
3. Click "Authorize Uninstall" in the main window
4. Enter correct password
5. Try to delete the application directory → Should succeed

### Security Considerations

- **Password Required**: Uninstall requires the same password used to access the application
- **File Lock**: Prevents casual deletion but can be bypassed by:
  - Terminating the process forcefully
  - Booting into Safe Mode
  - Using specialized file unlocking tools
- **Not Foolproof**: This is a deterrent, not absolute protection

## Integration with Main Application

### Startup Sequence

```
App.OnStartup()
├── Check for --watchdog-mode argument
│   └── If yes: Run watchdog mode and exit
├── Initialize services
├── Start watchdog service
└── Start file protection
```

### Shutdown Sequence

```
App.OnExit()
├── Stop file protection
├── Stop watchdog service
└── Clean up resources
```

### Command-Line Arguments

- `--watchdog-mode`: Run as watchdog process (internal use)
- `--background` or `-b`: Start in background mode
- `--safe-mode`: Enable safe mode (network simulation)

## Troubleshooting

### Watchdog Issues

**Problem**: Watchdog doesn't restart the application
- Check if the watchdog process is running in Task Manager
- Check debug output for error messages
- Ensure the application has write access to its directory

**Problem**: Multiple watchdog processes
- This can happen if the application crashes during startup
- Manually kill extra watchdog processes
- Restart the application

### Uninstall Protection Issues

**Problem**: Can't delete application directory
- This is expected behavior when protection is active
- Use "Authorize Uninstall" to release protection
- Or terminate the application first

**Problem**: Lock file remains after exit
- This shouldn't happen (file has DeleteOnClose flag)
- If it does, manually delete `.applock` file
- Check for application crashes during shutdown

## Development Notes

### Safe Mode Compatibility

Both watchdog and uninstall protection work normally in safe mode. Safe mode only affects network operations.

### Testing in Development

When testing, you may want to temporarily disable these features:

```csharp
// In App.xaml.cs, comment out:
// await _watchdogService.StartWatchdogAsync();
// _uninstallProtection.StartFileProtection();
```

### Debugging

Enable debug output to see watchdog and protection messages:
- Visual Studio: Debug → Windows → Output
- Look for messages starting with "Watchdog" or "File protection"

## Future Enhancements

Possible improvements:
- Recursive watchdog (watchdog monitoring watchdog)
- Registry-based protection
- Service-based implementation for better persistence
- Encrypted communication between main process and watchdog
- Multiple lock files in different locations
