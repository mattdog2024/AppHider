@echo off
echo Forcing all AppHider processes to exit...
taskkill /F /IM AppHider.exe /T
timeout /t 2 /nobreak >nul
echo.
echo All AppHider processes terminated.
echo You can now run the new version.
echo.
pause
