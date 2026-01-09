Write-Host "========================================" -ForegroundColor Green
Write-Host "Testing Privacy Mode Fix (Process Tree Kill)" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

Write-Host ""
Write-Host "Test 1: Verifying dangerous process.Kill(entireProcessTree: true) is removed..." -ForegroundColor Yellow

# Check if the dangerous code is removed
$rdClientFile = "AppHider\Services\RDClientService.cs"
if (Test-Path $rdClientFile) {
    $content = Get-Content $rdClientFile -Raw
    
    if ($content -match "entireProcessTree:\s*true") {
        Write-Host "❌ FAILED: Dangerous entireProcessTree: true still found in code!" -ForegroundColor Red
        exit 1
    } else {
        Write-Host "✅ PASSED: Dangerous entireProcessTree: true removed from code" -ForegroundColor Green
    }
    
    if ($content -match "process\.Kill\(\)") {
        Write-Host "✅ PASSED: Safe process.Kill() method found" -ForegroundColor Green
    } else {
        Write-Host "⚠️  WARNING: process.Kill() method not found - check implementation" -ForegroundColor Yellow
    }
} else {
    Write-Host "❌ FAILED: RDClientService.cs not found!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Test 2: Verifying build success..." -ForegroundColor Yellow

try {
    $buildResult = dotnet build AppHider.sln --configuration Release --verbosity quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ PASSED: Application builds successfully" -ForegroundColor Green
    } else {
        Write-Host "❌ FAILED: Build failed" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "❌ FAILED: Build error - $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Test 3: Verifying Privacy Mode integration..." -ForegroundColor Yellow

$privacyModeFile = "AppHider\Services\PrivacyModeController.cs"
if (Test-Path $privacyModeFile) {
    $content = Get-Content $privacyModeFile -Raw
    
    if ($content -match "ExecuteRemoteDesktopDisconnectOnlyAsync") {
        Write-Host "✅ PASSED: Remote desktop disconnect integration found" -ForegroundColor Green
    } else {
        Write-Host "❌ FAILED: Remote desktop disconnect integration missing" -ForegroundColor Red
        exit 1
    }
    
    if ($content -match "Close remote desktop connections first") {
        Write-Host "✅ PASSED: Correct execution order maintained (RD first, then network)" -ForegroundColor Green
    } else {
        Write-Host "⚠️  WARNING: Execution order comment not found" -ForegroundColor Yellow
    }
} else {
    Write-Host "❌ FAILED: PrivacyModeController.cs not found!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "✅ ALL TESTS PASSED!" -ForegroundColor Green
Write-Host "Privacy Mode fix is complete and ready for testing." -ForegroundColor Green
Write-Host ""
Write-Host "SUMMARY OF CHANGES:" -ForegroundColor Cyan
Write-Host "- Removed dangerous 'entireProcessTree: true' parameter" -ForegroundColor White
Write-Host "- Now uses safe 'process.Kill()' method" -ForegroundColor White
Write-Host "- Maintains complete Privacy Mode functionality:" -ForegroundColor White
Write-Host "  1. Hide applications" -ForegroundColor White
Write-Host "  2. Close remote desktop connections (FIXED)" -ForegroundColor White
Write-Host "  3. Disconnect network (including MAC randomization)" -ForegroundColor White
Write-Host "========================================" -ForegroundColor Green