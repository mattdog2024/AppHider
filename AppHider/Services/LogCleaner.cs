using System.IO;
using System.Security;
using Microsoft.Win32;
using FL = AppHider.Utils.FileLogger;

namespace AppHider.Services;

public class LogCleaner : ILogCleaner
{
    public async Task CleanAllLogsAsync()
    {
        FL.Log("[CLEANER] Starting comprehensive log cleaning...");
        
        var t1 = CleanRDPLogsAsync();
        var t2 = CleanSystemLogsAsync();
        
        await Task.WhenAll(t1, t2);
        
        FL.Log("[CLEANER] Comprehensive log cleaning completed.");
    }

    public Task CleanRDPLogsAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                FL.Log("[CLEANER] Cleaning RDP logs...");

                // 1. Delete Default.rdp in Documents
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string defaultRdpPath = Path.Combine(documentsPath, "Default.rdp");
                if (File.Exists(defaultRdpPath))
                {
                    File.Delete(defaultRdpPath);
                    FL.Log($"[CLEANER] Deleted: {defaultRdpPath}");
                }

                // 2. Clear Registry Keys for MSTSC
                // HKCU\Software\Microsoft\Terminal Server Client
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Terminal Server Client", true))
                {
                    if (key != null)
                    {
                        // Clear "Default" (Last connected IPs)
                        try 
                        {
                            key.DeleteSubKeyTree("Default");
                            FL.Log("[CLEANER] Deleted HKCU\\Software\\Microsoft\\Terminal Server Client\\Default");
                        }
                        catch (ArgumentException) { /* Key doesn't exist */ }

                        // Clear "Servers" (Saved connections)
                        try
                        {
                            key.DeleteSubKeyTree("Servers");
                            FL.Log("[CLEANER] Deleted HKCU\\Software\\Microsoft\\Terminal Server Client\\Servers");
                        }
                        catch (ArgumentException) { /* Key doesn't exist */ }
                    }
                }
                FL.Log("[CLEANER] RDP logs cleaned.");
            }
            catch (Exception ex)
            {
                FL.Log($"[CLEANER] Error cleaning RDP logs: {ex.Message}");
            }
        });
    }

    public Task CleanSystemLogsAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                FL.Log("[CLEANER] Cleaning System logs...");

                // 1. Clear Run History
                // HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU", true))
                {
                    if (key != null)
                    {
                        string[] valueNames = key.GetValueNames();
                        foreach (string valueName in valueNames)
                        {
                            key.DeleteValue(valueName);
                        }
                        FL.Log("[CLEANER] Cleared RunMRU");
                    }
                }

                // 2. Clear Explorer Address Bar History (TypedPaths)
                // HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\TypedPaths
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\TypedPaths", true))
                {
                    if (key != null)
                    {
                        string[] valueNames = key.GetValueNames();
                        foreach (string valueName in valueNames)
                        {
                            key.DeleteValue(valueName);
                        }
                        FL.Log("[CLEANER] Cleared Explorer TypedPaths");
                    }
                }

                // 2. Clear Recent Files (Recent Docs)
                string recentPath = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
                if (Directory.Exists(recentPath))
                {
                    var files = Directory.GetFiles(recentPath);
                    foreach (var file in files)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch { /* Ignore used files */ }
                    }
                    FL.Log($"[CLEANER] Cleared {files.Length} files from Recent Documents");
                }

                FL.Log("[CLEANER] System logs cleaned.");
            }
            catch (Exception ex)
            {
                FL.Log($"[CLEANER] Error cleaning System logs: {ex.Message}");
            }
        });
    }
}
