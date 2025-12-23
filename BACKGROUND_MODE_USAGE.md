# Background Mode Usage Guide

## Overview

The AppHider application now supports background mode, allowing it to run invisibly without showing any windows until activated by a hotkey.

## Features Implemented

### 1. Windowless Startup (Requirement 8.2)

The application can start in background mode without displaying any visible window or system tray icon.

**How to start in background mode:**
```bash
AppHider.exe --background
# or
AppHider.exe -b
```

When started in background mode:
- No login window is shown
- No main window is shown
- A hidden window is created for message processing (required for hotkeys)
- The application runs silently in the background

### 2. Hotkey to Show Interface (Requirement 8.4)

When running in background mode, pressing the configured hotkey will show the interface.

**Default hotkey:** `Ctrl + Alt + F9`

**What happens when hotkey is pressed:**
1. If not authenticated, the login window appears first
2. After successful authentication, the main window is shown
3. The window is activated and brought to the foreground

**Customizing the hotkey:**
- The hotkey can be configured in the main window's settings
- The configuration is saved and loaded from settings file
- If settings fail to load, the default hotkey is used

### 3. Closing Window Returns to Background (Requirement 8.5)

When the main window is closed in background mode, it doesn't exit the application - it just hides the window.

**Behavior:**
- Clicking the X button hides the window instead of closing it
- The application continues running in the background
- Pressing the hotkey again will show the window
- All settings and state are preserved

**To actually exit the application:**
- Use Task Manager to end the process
- Or implement an "Exit" menu option (future enhancement)

## Implementation Details

### App.xaml.cs Changes

1. **Service Initialization:** Services are now stored as class fields to be reused when showing/hiding windows

2. **Background Mode Detection:** Checks for `--background` or `-b` command-line arguments

3. **StartBackgroundMode():** 
   - Creates a hidden window for message processing
   - Initializes HotkeyManager with the hidden window
   - Registers the hotkey to show the main window
   - Registers lock screen hook for privacy mode

4. **ShowMainWindowFromBackground():**
   - Shows login window if not authenticated
   - Creates or shows the main window
   - Handles window closing event to hide instead of close

5. **MainWindow_Closing():**
   - Cancels the close event in background mode
   - Hides the window instead

### Normal Mode vs Background Mode

**Normal Mode (default):**
- Shows login window on startup
- Shows main window after authentication
- Closing window exits the application

**Background Mode (--background flag):**
- No windows shown on startup
- Hotkey shows login window (if needed) then main window
- Closing window hides it and returns to background
- Application continues running

## Testing

### Manual Testing

1. **Test background startup:**
   ```bash
   AppHider.exe --background
   ```
   - Verify no window appears
   - Check Task Manager to confirm process is running

2. **Test hotkey show:**
   - Press `Ctrl + Alt + F9` (or configured hotkey)
   - Verify login window appears (if first time)
   - Verify main window appears after authentication

3. **Test window hide:**
   - Close the main window
   - Verify window disappears but process still runs
   - Press hotkey again to verify window reappears

4. **Test normal mode:**
   ```bash
   AppHider.exe
   ```
   - Verify login window appears immediately
   - Verify main window appears after authentication

### Automated Testing

The `BackgroundModeTest.cs` file contains basic tests for background mode functionality.

## Requirements Validation

✅ **Requirement 8.2:** Application starts in background without displaying any visible window
- Implemented via `--background` command-line flag
- Hidden window created for message processing only

✅ **Requirement 8.4:** Hotkey displays the graphical interface from background mode
- Implemented via `ShowMainWindowFromBackground()` method
- Hotkey registered in `StartBackgroundMode()`

✅ **Requirement 8.5:** Closing the graphical interface returns to invisible background mode
- Implemented via `MainWindow_Closing()` event handler
- Window is hidden instead of closed when in background mode

## Future Enhancements

1. **System Tray Icon (Optional):**
   - Add a system tray icon for easier access
   - Right-click menu with Show/Hide/Exit options

2. **Exit Command:**
   - Add a proper exit option in the UI
   - Implement graceful shutdown

3. **Startup Configuration:**
   - Save background mode preference in settings
   - Auto-start in background mode on Windows startup

4. **Multiple Hotkeys:**
   - Different hotkeys for different actions
   - Hotkey to toggle privacy mode without showing window

## Notes

- The hidden window is necessary for Windows message processing (required for hotkeys)
- The HotkeyManager requires a window handle to register global hotkeys
- Authentication state is preserved when hiding/showing windows
- All services are initialized once and reused throughout the application lifecycle
