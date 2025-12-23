@echo off
chcp 65001 >nul
echo ========================================
echo 强制退出 AppHider 程序
echo ========================================
echo.

echo 正在查找 AppHider 进程...
tasklist | find /I "AppHider.exe" >nul

if %errorlevel% equ 0 (
    echo 找到 AppHider 进程，正在强制退出...
    echo.
    echo 注意：AppHider 有看门狗保护，会自动重启
    echo 正在终止所有 AppHider 进程（包括看门狗）...
    echo.
    
    REM 终止所有 AppHider 进程（包括主进程和看门狗）
    taskkill /F /IM AppHider.exe /T
    
    REM 等待一下确保进程完全终止
    timeout /t 2 /nobreak >nul
    
    REM 再次检查是否还有进程
    tasklist | find /I "AppHider.exe" >nul
    if %errorlevel% equ 0 (
        echo.
        echo ⚠ 警告：仍有 AppHider 进程在运行
        echo 看门狗可能已重启程序
        echo 请再次运行此脚本，或重启电脑
    ) else (
        echo.
        echo ✓ AppHider 已成功退出！
    )
) else (
    echo 未找到 AppHider 进程，程序可能已经退出
)

echo.
echo ========================================
echo 按任意键退出...
pause >nul
