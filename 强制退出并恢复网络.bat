@echo off
chcp 65001 >nul
echo ========================================
echo 强制退出 AppHider 并恢复网络
echo ========================================
echo.
echo 请以管理员权限运行此脚本！
echo.

echo [步骤 1/6] 正在强制退出 AppHider...
taskkill /F /IM AppHider.exe 2>nul
if %errorlevel% equ 0 (
    echo ✓ AppHider 已退出
) else (
    echo ℹ AppHider 未运行或已退出
)
echo.

echo [步骤 2/6] 正在移除防火墙规则...
netsh advfirewall firewall delete rule name="AppHider_Privacy_Block_Out" 2>nul
netsh advfirewall firewall delete rule name="AppHider_Privacy_Block_In" 2>nul
echo ✓ 防火墙规则已清除
echo.

echo [步骤 3/6] 正在启用所有网络适配器...
wmic path win32_networkadapter where "NetConnectionID is not null" call enable 2>nul
echo ✓ 网络适配器已启用
echo.

echo [步骤 4/6] 正在重置为 DHCP...
for /f "tokens=1,2,*" %%a in ('netsh interface show interface ^| findstr /R /C:"已连接"') do (
    netsh interface ip set address name="%%c" dhcp 2>nul
    netsh interface ip set dns name="%%c" dhcp 2>nul
)
echo ✓ DHCP 已重置
echo.

echo [步骤 5/6] 正在启动 DNS 服务...
net start Dnscache 2>nul
echo ✓ DNS 服务已启动
echo.

echo [步骤 6/6] 正在刷新网络配置...
ipconfig /release >nul 2>&1
ipconfig /renew >nul 2>&1
ipconfig /flushdns >nul 2>&1
echo ✓ 网络配置已刷新
echo.

echo ========================================
echo ✓ 完成！AppHider 已退出，网络已恢复
echo ========================================
echo.
echo 按任意键退出...
pause >nul
