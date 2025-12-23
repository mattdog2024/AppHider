@echo off
echo ========================================
echo AppHider 错误日志查看工具
echo ========================================
echo.

echo 正在查看最新的日志文件...
echo.

cd /d "%APPDATA%\AppHider"
if exist AppHider_*.log (
    for /f "delims=" %%i in ('dir /b /o-d AppHider_*.log') do (
        echo 最新日志文件: %%i
        echo.
        echo 最后100行内容:
        echo ----------------------------------------
        powershell -Command "Get-Content '%%i' | Select-Object -Last 100"
        goto :done
    )
) else (
    echo 未找到日志文件！
    echo 日志应该在: %APPDATA%\AppHider\
)

:done
echo.
echo ========================================
echo 按任意键退出...
pause >nul
