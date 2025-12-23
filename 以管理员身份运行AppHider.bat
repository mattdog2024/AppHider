@echo off
echo ========================================
echo 以管理员身份运行 AppHider
echo ========================================
echo.
echo 正在请求管理员权限...
echo.

:: 检查是否已经是管理员
net session >nul 2>&1
if %errorLevel% == 0 (
    echo 已经是管理员权限，启动程序...
    start "" "%~dp0AppHider.exe"
) else (
    echo 请求管理员权限...
    powershell -Command "Start-Process '%~dp0AppHider.exe' -Verb RunAs"
)

echo.
echo 完成！
timeout /t 2 >nul
