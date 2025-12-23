# PowerShell 版本 - 强制退出 AppHider
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "强制退出 AppHider 程序" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "正在查找 AppHider 进程..." -ForegroundColor Yellow

$process = Get-Process -Name "AppHider" -ErrorAction SilentlyContinue

if ($process) {
    Write-Host "找到 AppHider 进程 (PID: $($process.Id))，正在强制退出..." -ForegroundColor Yellow
    
    try {
        Stop-Process -Name "AppHider" -Force
        Write-Host ""
        Write-Host "✓ AppHider 已成功退出！" -ForegroundColor Green
    }
    catch {
        Write-Host ""
        Write-Host "✗ 退出失败: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "请以管理员权限运行此脚本" -ForegroundColor Red
    }
}
else {
    Write-Host "未找到 AppHider 进程，程序可能已经退出" -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "按任意键退出..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
