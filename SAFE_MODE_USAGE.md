# Safe Mode Usage Guide

## Overview

Safe Mode is a development feature that prevents the AppHider application from making actual network modifications. This is critical when developing or testing the application remotely, as actual network operations would disconnect your remote session.

## How Safe Mode Works

When Safe Mode is enabled:
- All network operations are **simulated** (not executed)
- Operations are logged to `%APPDATA%\AppHider\network_operations.log`
- The application behaves normally in all other aspects
- A visual indicator shows that Safe Mode is active

## Enabling Safe Mode

There are two ways to enable Safe Mode:

### Method 1: Command-Line Argument (Recommended for Testing)

Run the application with the `--safe-mode` or `/safe-mode` argument:

```cmd
AppHider.exe --safe-mode
```

or

```cmd
AppHider.exe /safe-mode
```

**Priority:** Command-line arguments have the highest priority and will override the flag file setting.

### Method 2: Flag File (Recommended for Development)

Create a flag file in the AppData directory:

**File Location:** `%APPDATA%\AppHider\safe_mode.flag`

**Full Path Example:** `C:\Users\YourUsername\AppData\Roaming\AppHider\safe_mode.flag`

**To Enable:**
1. Navigate to `%APPDATA%\AppHider\`
2. Create a file named `safe_mode.flag` (content doesn't matter)
3. Restart the application

**To Disable:**
1. Delete the `safe_mode.flag` file
2. Restart the application

**PowerShell Commands:**
```powershell
# Enable Safe Mode
New-Item -Path "$env:APPDATA\AppHider\safe_mode.flag" -ItemType File -Force

# Disable Safe Mode
Remove-Item "$env:APPDATA\AppHider\safe_mode.flag" -Force
```

## Verifying Safe Mode Status

When Safe Mode is enabled, you will see:
1. Console output: `=== SAFE MODE ENABLED ===`
2. Log entries prefixed with `[SAFE MODE]` in the network operations log
3. A visual indicator in the application UI (when implemented)

## Testing Safe Mode

Run the included test script to verify Safe Mode detection:

```powershell
powershell -ExecutionPolicy Bypass -File test_safe_mode.ps1
```

This will test:
- Flag file creation and deletion
- Flag file detection
- Command-line argument detection

## Implementation Details

### Code Components

1. **SafeModeDetector** (`AppHider/Utils/SafeModeDetector.cs`)
   - Detects safe mode from command-line args or flag file
   - Provides methods to create/delete flag file
   - Priority: Command-line > Flag file

2. **App.xaml.cs** (`AppHider/App.xaml.cs`)
   - Detects safe mode on application startup
   - Sets `App.IsSafeModeEnabled` static property
   - Logs safe mode status to console

3. **NetworkController** (`AppHider/Services/NetworkController.cs`)
   - Checks `IsSafeMode` property before executing network operations
   - Logs operations instead of executing them when in safe mode
   - Initializes from `App.IsSafeModeEnabled`

4. **PrivacyModeController** (`AppHider/Services/PrivacyModeController.cs`)
   - Exposes `IsSafeMode` property
   - Initializes from `App.IsSafeModeEnabled`

### Requirements Validation

This implementation satisfies:
- **Requirement 5.7:** Flag file detection on startup
- **Requirement 5.9:** Command-line parameter override
- **Requirement 5.5:** Network operation simulation
- **Requirement 5.6:** Operation logging

## Best Practices

1. **Always use Safe Mode when developing remotely** to avoid disconnecting your session
2. **Use command-line argument** for one-time testing
3. **Use flag file** for persistent development mode
4. **Check the log file** to verify operations are being simulated
5. **Remove flag file** before deploying to production

## Troubleshooting

### Safe Mode Not Activating

1. Check if the flag file exists: `Test-Path "$env:APPDATA\AppHider\safe_mode.flag"`
2. Verify command-line arguments are being passed correctly
3. Check console output for `=== SAFE MODE ENABLED ===` message
4. Review the network operations log for `[SAFE MODE]` entries

### Safe Mode Not Deactivating

1. Ensure the flag file is deleted: `Remove-Item "$env:APPDATA\AppHider\safe_mode.flag" -Force`
2. Restart the application
3. Verify no command-line arguments are being passed

## Example Workflow

### Development Workflow
```powershell
# 1. Enable Safe Mode for development
New-Item -Path "$env:APPDATA\AppHider\safe_mode.flag" -ItemType File -Force

# 2. Start the application (safe mode will be active)
.\AppHider\bin\Debug\net8.0-windows\AppHider.exe

# 3. Test privacy mode features (network operations will be simulated)

# 4. When ready to test actual network operations (on local machine only!)
Remove-Item "$env:APPDATA\AppHider\safe_mode.flag" -Force

# 5. Restart and test with real network operations
```

### Testing Workflow
```powershell
# Test with command-line argument (one-time)
.\AppHider\bin\Debug\net8.0-windows\AppHider.exe --safe-mode
```

## Log File Location

Network operations are logged to:
```
%APPDATA%\AppHider\network_operations.log
```

Example log entries in Safe Mode:
```
[2025-12-16 09:45:23] Starting network disable operation
[2025-12-16 09:45:23] [SAFE MODE] Would add firewall rules to block all inbound and outbound traffic
[2025-12-16 09:45:23] [SAFE MODE] Would disable DNS Client service
[2025-12-16 09:45:23] [SAFE MODE] Would modify IP address to 192.168.1.88 with subnet 255.255.255.0
[2025-12-16 09:45:23] [SAFE MODE] Would disable network adapter at device level
[2025-12-16 09:45:23] Network disable operation completed successfully
```
