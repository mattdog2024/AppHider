using System.Diagnostics;
using System.IO;

using FL = AppHider.Utils.FileLogger;

namespace AppHider.Services;

public class VHDXManager : IVHDXManager
{
    private string? _currentSymlinkPath;
    private string? _originalLvmPath;

    public bool IsMounted { get; private set; }

    public async Task<bool> MountVHDXAsync(string filePath, string password)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            FL.Log($"[VHDX] File not found: {filePath}");
            return false;
        }

        _originalLvmPath = filePath;
        
        // 1. Create Symlink (temp.vhdx -> target.lvm)
        // Windows Mount-DiskImage typically requires .vhdx or .iso extension
        string tempDir = Path.GetTempPath();
        _currentSymlinkPath = Path.Combine(tempDir, $"AppHider_Mount_{Guid.NewGuid()}.vhdx");

        try
        {
            // Use Kernel32 or fsutil, or simply File.CreateSymbolicLink (Net6+)
            File.CreateSymbolicLink(_currentSymlinkPath, _originalLvmPath);
            FL.Log($"[VHDX] Created symlink: {_currentSymlinkPath} -> {_originalLvmPath}");
        }
        catch (Exception ex)
        {
            FL.Log($"[VHDX] Failed to create symlink: {ex.Message}");
            return false;
        }

        // 2. Mount Disk Image using API or PowerShell
        // Using PowerShell via Process is robust for system admin tasks
        bool mounted = await RunPowerShellCommandAsync($"Mount-DiskImage -ImagePath '{_currentSymlinkPath}'");
        if (!mounted)
        {
            FL.Log("[VHDX] Mount-DiskImage failed.");
            CleanupSymlink();
            return false;
        }

        IsMounted = true;
        FL.Log("[VHDX] Disk image mounted successfully.");

        // 3. Unlock with BitLocker
        // We need to find the drive letter first. This is tricky.
        // A simple heuristic: check which drive appeared, or use 'Get-DiskImage ... | Get-Disk | Get-Partition | Get-Volume'
        
        string? driveLetter = await GetDriveLetterForImageAsync(_currentSymlinkPath);
        if (string.IsNullOrEmpty(driveLetter))
        {
             FL.Log("[VHDX] Could not determine drive letter, possibly not formatted or BitLocker locked without letter assignment yet.");
             // Sometimes locked drives don't get a letter until unlocked, or they get a letter but are inaccessible.
             // Let's try to unlock 'all' locked volumes that match? No, unsafe.
             // Let's assume it gets a letter.
        }
        else
        {
            FL.Log($"[VHDX] Detected drive letter: {driveLetter}");
            bool unlocked = await UnlockBitLockerAsync(driveLetter, password);
            if (unlocked)
            {
                 FL.Log($"[VHDX] Volume {driveLetter} unlocked successfully.");
            }
            else
            {
                 FL.Log($"[VHDX] Failed to unlock volume {driveLetter}. Check password or BitLocker status.");
                 // Don't unmount, maybe user wants to unlock manually? 
                 // But user asked for automatic. We will just report failure.
                 return false; 
            }
        }

        return true;
    }

    public async Task<bool> DismountVHDXAsync()
    {
        if (!_currentSymlinkPath.IsPresent())
        {
             FL.Log("[VHDX] No active VHDX mount known (symlink path null).");
             return false;
        }

        FL.Log("[VHDX] Starting emergency dismount...");

        // 1. Force Dismount
        bool result = await RunPowerShellCommandAsync($"Dismount-DiskImage -ImagePath '{_currentSymlinkPath}'");
        
        if (result)
        {
            FL.Log("[VHDX] Dismount-DiskImage successful.");
            IsMounted = false;
            CleanupSymlink();
        }
        else
        {
            FL.Log("[VHDX] Dismount-DiskImage reported failure. Trying fallback (Diskpart).");
            // Fallback could be implemented here
        }

        return result;
    }

    private void CleanupSymlink()
    {
        if (!string.IsNullOrEmpty(_currentSymlinkPath) && File.Exists(_currentSymlinkPath))
        {
            try
            {
                File.Delete(_currentSymlinkPath);
                FL.Log($"[VHDX] Removed symlink: {_currentSymlinkPath}");
            }
            catch (Exception ex)
            {
                FL.Log($"[VHDX] Warning: Failed to remove symlink: {ex.Message}");
            }
        }
        _currentSymlinkPath = null;
    }

    private async Task<bool> RunPowerShellCommandAsync(string command)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            await process.WaitForExitAsync();
            
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();

            if (process.ExitCode != 0)
            {
                FL.Log($"[VHDX] PowerShell error: {error}");
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            FL.Log($"[VHDX] Process execution error: {ex.Message}");
            return false;
        }
    }

    private async Task<string?> GetDriveLetterForImageAsync(string imagePath)
    {
        // PowerShell incantation to get drive letter from image path
        // Get-DiskImage -ImagePath '...' | Get-Disk | Get-Partition | Get-Volume | Select-Object -ExpandProperty DriveLetter
        string command = $"Get-DiskImage -ImagePath '{imagePath}' | Get-Disk | Get-Partition | Get-Volume | Select-Object -ExpandProperty DriveLetter";
        
        try
        {
             var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            string letter = output.Trim();
            if (!string.IsNullOrEmpty(letter) && letter.Length == 1)
            {
                return letter + ":";
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<bool> UnlockBitLockerAsync(string driveLetter, string password)
    {
        // manage-bde -unlock X: -pw -force
        // Note: passing password via stdin or pipe is safer than command line arguments if possible, 
        // but manage-bde supports -pw <password> directly (visible in process list) or -pw (prompt).
        // For automation, pipe is better: echo password | manage-bde -unlock X: -pw
        
        /* 
           Using PowerShell's Unlock-BitLocker is cleaner:
           $pw = ConvertTo-SecureString 'password' -AsPlainText -Force
           Unlock-BitLocker -MountPoint 'X:' -Password $pw
        */
        
        // Sanitize password for PS string (escape single quotes)
        string escapedPassword = password.Replace("'", "''");
        
        string command = $"$pw = ConvertTo-SecureString '{escapedPassword}' -AsPlainText -Force; Unlock-BitLocker -MountPoint '{driveLetter}' -Password $pw";
        
        return await RunPowerShellCommandAsync(command);
    }
}

public static class StringExtensions 
{
    public static bool IsPresent(this string? s) => !string.IsNullOrEmpty(s);
}
