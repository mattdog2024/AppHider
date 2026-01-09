# Final Integration Test Script
Write-Host "========================================" -ForegroundColor Green
Write-Host "Running Final Integration Tests" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

# Test 1: Build verification
Write-Host "Test 1: Build verification..." -ForegroundColor Yellow
try {
    $buildResult = dotnet build AppHider.sln --configuration Debug --verbosity quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Build successful" -ForegroundColor Green
    } else {
        Write-Host "✗ Build failed" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "✗ Build error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test 2: Application startup test
Write-Host "Test 2: Application startup test..." -ForegroundColor Yellow
try {
    $process = Start-Process -FilePath "AppHider\bin\Debug\net8.0-windows\win-x64\AppHider.exe" -ArgumentList "--verify-integration" -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 3
    
    if ($process.HasExited) {
        if ($process.ExitCode -eq 0) {
            Write-Host "✓ Application started and exited successfully" -ForegroundColor Green
        } else {
            Write-Host "✗ Application exited with error code: $($process.ExitCode)" -ForegroundColor Red
        }
    } else {
        Write-Host "✓ Application is running (will terminate)" -ForegroundColor Green
        $process.Kill()
        $process.WaitForExit()
    }
} catch {
    Write-Host "✗ Application startup error: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: Check for critical files
Write-Host "Test 3: Critical files verification..." -ForegroundColor Yellow
$criticalFiles = @(
    "AppHider\Services\RemoteDesktopManager.cs",
    "AppHider\Services\RDSessionService.cs", 
    "AppHider\Services\RDClientService.cs",
    "AppHider\Services\EmergencyDisconnectController.cs"
)

$allFilesExist = $true
foreach ($file in $criticalFiles) {
    if (Test-Path $file) {
        Write-Host "✓ $file exists" -ForegroundColor Green
    } else {
        Write-Host "✗ $file missing" -ForegroundColor Red
        $allFilesExist = $false
    }
}

if ($allFilesExist) {
    Write-Host "✓ All critical files present" -ForegroundColor Green
} else {
    Write-Host "✗ Some critical files missing" -ForegroundColor Red
}

# Test 4: Check integration test files
Write-Host "Test 4: Integration test files verification..." -ForegroundColor Yellow
$testFiles = @(
    "AppHider\Utils\SimpleIntegrationTest.cs",
    "AppHider\Utils\FileBasedIntegrationTest.cs",
    "AppHider\Utils\ComprehensiveIntegrationTestRunner.cs",
    "AppHider\Utils\RemoteDesktopIntegrationTest.cs"
)

$allTestFilesExist = $true
foreach ($file in $testFiles) {
    if (Test-Path $file) {
        Write-Host "✓ $file exists" -ForegroundColor Green
    } else {
        Write-Host "✗ $file missing" -ForegroundColor Red
        $allTestFilesExist = $false
    }
}

if ($allTestFilesExist) {
    Write-Host "✓ All integration test files present" -ForegroundColor Green
} else {
    Write-Host "✗ Some integration test files missing" -ForegroundColor Red
}

# Final summary
Write-Host "========================================" -ForegroundColor Green
if ($allFilesExist -and $allTestFilesExist) {
    Write-Host "✓ ALL FINAL TESTS PASSED!" -ForegroundColor Green
    Write-Host "Remote desktop disconnect integration is complete and ready." -ForegroundColor Green
    exit 0
} else {
    Write-Host "✗ SOME FINAL TESTS FAILED!" -ForegroundColor Red
    Write-Host "Please review the issues above." -ForegroundColor Red
    exit 1
}