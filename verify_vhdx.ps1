
$ErrorActionPreference = "Stop"

function Test-Type {
    param([string]$AssemblyPath, [string]$TypeName, [string[]]$ExpectedMethods)
    
    Write-Host "Testing type: $TypeName" -ForegroundColor Cyan
    
    try {
        $assembly = [System.Reflection.Assembly]::LoadFrom($AssemblyPath)
        $type = $assembly.GetType($TypeName)
        
        if ($null -eq $type) {
            Write-Error "Type $TypeName not found in assembly."
            return $false
        }
        
        Write-Host "  [+] Type found" -ForegroundColor Green
        
        $missingMethods = @()
        foreach ($method in $ExpectedMethods) {
            if ($null -eq $type.GetMethod($method)) {
                $missingMethods += $method
            }
        }
        
        if ($missingMethods.Count -gt 0) {
            Write-Error "  [-] Missing methods: $($missingMethods -join ', ')"
            return $false
        }
        
        Write-Host "  [+] All methods found" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Error "  [-] Verification failed: $_"
        return $false
    }
}

$appPath = "d:\kaifa\apphier\AppHider\bin\Release\net9.0-windows\AppHider.dll"
# Fallback to Debug if Release doesn't exist or we haven't built yet
if (-not (Test-Path $appPath)) {
    $appPath = "d:\kaifa\apphier\AppHider\bin\Debug\net9.0-windows\AppHider.dll"
}

if (-not (Test-Path $appPath)) {
    Write-Warning "Binary not found at $appPath. Please build the project first."
    exit 1
}

Write-Host "Verifying types in $appPath" -ForegroundColor Yellow

$vhdxResult = Test-Type -AssemblyPath $appPath -TypeName "AppHider.Services.VHDXManager" -ExpectedMethods @("MountVHDXAsync", "DismountVHDXAsync")
$logResult = Test-Type -AssemblyPath $appPath -TypeName "AppHider.Services.LogCleaner" -ExpectedMethods @("CleanAllLogsAsync", "CleanRDPLogsAsync")
$settingsResult = Test-Type -AssemblyPath $appPath -TypeName "AppHider.Models.AppSettings" -ExpectedMethods @("get_IsVHDXEnabled", "get_VHDXPath")

if ($vhdxResult -and $logResult -and $settingsResult) {
    Write-Host "`nAll verification checks passed!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`nVerification failed." -ForegroundColor Red
    exit 1
}
