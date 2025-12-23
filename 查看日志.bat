@echo off
chcp 65001 >nul
echo ========================================
echo AppHider Log Viewer
echo ========================================
echo.
echo Opening log folder...
echo.

set LOGDIR=%APPDATA%\AppHider

if exist "%LOGDIR%" (
    echo Log folder: %LOGDIR%
    echo.
    echo Opening folder in Explorer...
    explorer "%LOGDIR%"
    echo.
    echo Please find the most recent AppHider_*.log file
    echo and open it with Notepad to view the logs.
    echo.
    echo Look for these important log entries:
    echo   [STARTUP] - Application startup
    echo   [WINDOW] - Window creation/showing
    echo   [HOTKEY] - Hotkey registration and presses
    echo   [AUTH] - Authentication
    echo.
) else (
    echo Log folder does not exist yet: %LOGDIR%
    echo.
    echo The log folder will be created when you run AppHider.
    echo.
)

echo ========================================
pause
