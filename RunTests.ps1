# PowerShell script to run remote desktop tests
Write-Host "Starting Remote Desktop Management Tests..." -ForegroundColor Green

try {
    # Start the application with test parameter
    $process = Start-Process -FilePath ".\AppHider\bin\Debug\net8.0-windows\win-x64\AppHider.exe" -ArgumentList "--test-remote-desktop" -Wait -PassThru -WindowStyle Normal
    
    Write-Host "Test process completed with exit code: $($process.ExitCode)" -ForegroundColor Yellow
    
    if ($process.ExitCode -eq 0) {
        Write-Host "✓ All tests PASSED!" -ForegroundColor Green
    } else {
        Write-Host "✗ Some tests FAILED!" -ForegroundColor Red
    }
    
    exit $process.ExitCode
}
catch {
    Write-Host "Error running tests: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}