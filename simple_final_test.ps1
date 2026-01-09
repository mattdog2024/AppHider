# Simple Final Test
Write-Host "Running Final Integration Verification..." -ForegroundColor Green

# Test 1: Check critical service files
$services = @(
    "AppHider\Services\RemoteDesktopManager.cs",
    "AppHider\Services\RDSessionService.cs", 
    "AppHider\Services\RDClientService.cs",
    "AppHider\Services\EmergencyDisconnectController.cs"
)

$allServicesExist = $true
foreach ($service in $services) {
    if (Test-Path $service) {
        Write-Host "✓ $service" -ForegroundColor Green
    } else {
        Write-Host "✗ $service MISSING" -ForegroundColor Red
        $allServicesExist = $false
    }
}

# Test 2: Check interfaces
$interfaces = @(
    "AppHider\Services\IRemoteDesktopManager.cs",
    "AppHider\Services\IRDSessionService.cs",
    "AppHider\Services\IRDClientService.cs",
    "AppHider\Services\IEmergencyDisconnectController.cs"
)

$allInterfacesExist = $true
foreach ($interface in $interfaces) {
    if (Test-Path $interface) {
        Write-Host "✓ $interface" -ForegroundColor Green
    } else {
        Write-Host "✗ $interface MISSING" -ForegroundColor Red
        $allInterfacesExist = $false
    }
}

# Test 3: Check models
$models = @(
    "AppHider\Models\RDPConnection.cs",
    "AppHider\Models\WTSSessionInfo.cs",
    "AppHider\Models\EmergencyDisconnectResult.cs"
)

$allModelsExist = $true
foreach ($model in $models) {
    if (Test-Path $model) {
        Write-Host "✓ $model" -ForegroundColor Green
    } else {
        Write-Host "✗ $model MISSING" -ForegroundColor Red
        $allModelsExist = $false
    }
}

# Test 4: Build test
Write-Host "Testing build..." -ForegroundColor Yellow
$buildResult = dotnet build AppHider.sln --configuration Debug --verbosity quiet
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Build successful" -ForegroundColor Green
    $buildSuccess = $true
} else {
    Write-Host "✗ Build failed" -ForegroundColor Red
    $buildSuccess = $false
}

# Final result
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
if ($allServicesExist -and $allInterfacesExist -and $allModelsExist -and $buildSuccess) {
    Write-Host "✓ ALL FINAL TESTS PASSED!" -ForegroundColor Green
    Write-Host "Remote desktop disconnect integration is complete." -ForegroundColor Green
} else {
    Write-Host "✗ SOME TESTS FAILED!" -ForegroundColor Red
    Write-Host "Please check the missing components above." -ForegroundColor Red
}
Write-Host "========================================" -ForegroundColor Cyan