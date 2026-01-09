using System.Diagnostics;
using System.Management;
using System.Net.NetworkInformation;
using System.ServiceProcess;
using System.IO;
using AppHider.Models;
using AppHider.Utils;
using FL = AppHider.Utils.FileLogger;

namespace AppHider.Services;

public class NetworkController : INetworkController
{
    private NetworkBackup? _originalSettings;
    private readonly string _logFilePath;
    private const string PRIVACY_FIREWALL_RULE_NAME = "AppHider_Privacy_Block";

    public bool IsSafeMode { get; set; }

    public NetworkController()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AppHider"
        );
        Directory.CreateDirectory(appDataPath);
        _logFilePath = Path.Combine(appDataPath, "network_operations.log");
        
        // Initialize safe mode from application startup detection
        IsSafeMode = App.IsSafeModeEnabled;
        
        // Load saved network settings from configuration file asynchronously
        // Don't block the constructor - load in background
        Task.Run(async () =>
        {
            try
            {
                var settingsService = new SettingsService();
                var settings = await settingsService.LoadSettingsAsync();
                if (settings.OriginalNetworkSettings != null)
                {
                    _originalSettings = settings.OriginalNetworkSettings;
                    LogOperation($"Loaded saved network settings for adapter: {_originalSettings.AdapterName}");
                }
            }
            catch (Exception ex)
            {
                LogOperation($"Error loading saved network settings: {ex.Message}");
            }
        });
        // Don't wait - let it load in background
    }

    public async Task<NetworkState> GetCurrentStateAsync()
    {
        try
        {
            var state = new NetworkState();

            // Get network adapter status
            var adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(a => a.NetworkInterfaceType != NetworkInterfaceType.Loopback 
                         && a.OperationalStatus == OperationalStatus.Up)
                .ToList();

            state.IsEnabled = adapters.Any();

            // Get current IP address
            if (adapters.Any())
            {
                var adapter = adapters.First();
                var ipProps = adapter.GetIPProperties();
                var ipv4 = ipProps.UnicastAddresses
                    .FirstOrDefault(ip => ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                
                state.CurrentIpAddress = ipv4?.Address.ToString() ?? string.Empty;
            }

            // Check DNS service status
            try
            {
                using var dnsService = new ServiceController("Dnscache");
                state.DnsServiceRunning = dnsService.Status == ServiceControllerStatus.Running;
            }
            catch
            {
                state.DnsServiceRunning = false;
            }

            // Check firewall rules
            state.FirewallActive = await CheckFirewallRuleExistsAsync(PRIVACY_FIREWALL_RULE_NAME);

            return state;
        }
        catch (Exception ex)
        {
            LogOperation($"Error getting current network state: {ex.Message}");
            throw;
        }
    }

    public async Task DisableNetworkAsync()
    {
        LogOperation("Starting network disable operation");

        // Capture network adapter states before operation
        var beforeStates = await GetNetworkAdapterStatesAsync();

        try
        {
            // Save original settings before making changes
            await SaveOriginalSettingsAsync();

            // Step 1: Add firewall rules to block all traffic
            await AddFirewallBlockRulesAsync();

            // Step 2: Disable DNS Client service
            await DisableDnsServiceAsync();

            // Step 3: Modify IP address to 192.168.1.88
            await ModifyIpAddressAsync();

            // Step 4: Disable network adapter at device level
            await DisableNetworkAdapterAsync();

            // Capture network adapter states after operation
            var afterStates = await GetNetworkAdapterStatesAsync();

            // Log network adapter state changes
            FL.LogNetworkAdapterStates(beforeStates, afterStates, "DisableNetwork");

            LogOperation("Network disable operation completed successfully");
        }
        catch (Exception ex)
        {
            // Capture states even on failure for debugging
            var afterStates = await GetNetworkAdapterStatesAsync();
            FL.LogNetworkAdapterStates(beforeStates, afterStates, "DisableNetwork_Failed");
            
            FL.LogDetailedError("DisableNetwork", ex, "Failed to disable network during privacy mode activation");
            LogOperation($"Error during network disable: {ex.Message}");
            throw;
        }
    }

    private async Task AddFirewallBlockRulesAsync()
    {
        LogOperation("Adding firewall block rules");

        if (IsSafeMode)
        {
            LogOperation("[SAFE MODE] Would add firewall rules to block all inbound and outbound traffic");
            return;
        }

        try
        {
            // Block all outbound traffic
            await ExecuteNetshCommandAsync(
                $"advfirewall firewall add rule name=\"{PRIVACY_FIREWALL_RULE_NAME}_Out\" " +
                "dir=out action=block enable=yes profile=any"
            );

            // Block all inbound traffic
            await ExecuteNetshCommandAsync(
                $"advfirewall firewall add rule name=\"{PRIVACY_FIREWALL_RULE_NAME}_In\" " +
                "dir=in action=block enable=yes profile=any"
            );

            LogOperation("Firewall block rules added successfully");
        }
        catch (Exception ex)
        {
            LogOperation($"Error adding firewall rules: {ex.Message}");
            throw;
        }
    }

    private async Task DisableDnsServiceAsync()
    {
        LogOperation("Disabling DNS Client service");

        if (IsSafeMode)
        {
            LogOperation("[SAFE MODE] Would disable DNS Client service");
            return;
        }

        try
        {
            using var dnsService = new ServiceController("Dnscache");
            
            if (dnsService.Status == ServiceControllerStatus.Running)
            {
                dnsService.Stop();
                dnsService.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                LogOperation("DNS Client service stopped successfully");
            }
        }
        catch (Exception ex)
        {
            LogOperation($"Error disabling DNS service: {ex.Message}");
            // Don't throw - continue with other disable operations
        }
    }

    private async Task ModifyIpAddressAsync()
    {
        LogOperation("Modifying IP address to 192.168.1.88");

        if (IsSafeMode)
        {
            LogOperation("[SAFE MODE] Would modify IP address to 192.168.1.88 with subnet 255.255.255.0");
            return;
        }

        try
        {
            var adapter = GetPrimaryNetworkAdapter();
            if (adapter == null)
            {
                LogOperation("No primary network adapter found");
                return;
            }

            // Use netsh to set static IP
            await ExecuteNetshCommandAsync(
                $"interface ip set address name=\"{adapter.Name}\" " +
                "static 192.168.1.88 255.255.255.0 none"
            );

            LogOperation($"IP address modified successfully on adapter: {adapter.Name}");
        }
        catch (Exception ex)
        {
            LogOperation($"Error modifying IP address: {ex.Message}");
            throw;
        }
    }

    private async Task DisableNetworkAdapterAsync()
    {
        LogOperation("Disabling network adapter at device level");

        if (IsSafeMode)
        {
            LogOperation("[SAFE MODE] Would disable network adapter at device level");
            return;
        }

        try
        {
            var adapter = GetPrimaryNetworkAdapter();
            if (adapter == null)
            {
                LogOperation("No primary network adapter found");
                return;
            }

            // Use WMI to disable the adapter
            await DisableAdapterViaWmiAsync(adapter.Name);

            LogOperation($"Network adapter disabled successfully: {adapter.Name}");
        }
        catch (Exception ex)
        {
            LogOperation($"Error disabling network adapter: {ex.Message}");
            throw;
        }
    }

    private async Task DisableAdapterViaWmiAsync(string adapterName)
    {
        await Task.Run(() =>
        {
            try
            {
                var query = $"SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionID = '{adapterName}'";
                using var searcher = new ManagementObjectSearcher(query);
                
                foreach (ManagementObject adapter in searcher.Get())
                {
                    adapter.InvokeMethod("Disable", null);
                    LogOperation($"Disabled adapter via WMI: {adapterName}");
                }
            }
            catch (Exception ex)
            {
                LogOperation($"WMI disable failed: {ex.Message}");
                throw;
            }
        });
    }

    public async Task RestoreNetworkAsync()
    {
        LogOperation("========================================");
        LogOperation("Starting network restore operation");
        LogOperation("========================================");

        // Capture network adapter states before operation
        var beforeStates = await GetNetworkAdapterStatesAsync();

        // If _originalSettings is null, try to load from configuration file
        if (_originalSettings == null)
        {
            LogOperation("WARNING: No original settings in memory, attempting to load from configuration file...");
            try
            {
                var settingsService = new SettingsService();
                var settings = await settingsService.LoadSettingsAsync();
                if (settings.OriginalNetworkSettings != null)
                {
                    _originalSettings = settings.OriginalNetworkSettings;
                    LogOperation($"Loaded network settings from configuration: {_originalSettings.AdapterName}");
                }
                else
                {
                    LogOperation("ERROR: No saved network settings found in configuration file");
                    LogOperation("Cannot restore network - no backup available");
                    return;
                }
            }
            catch (Exception ex)
            {
                FL.LogDetailedError("LoadNetworkSettings", ex, "Failed to load network settings from configuration during restore");
                LogOperation($"ERROR: Failed to load network settings from configuration: {ex.Message}");
                LogOperation("Cannot restore network - no backup available");
                return;
            }
        }

        try
        {
            // Step 1: Randomize MAC address BEFORE enabling adapter
            // This is critical - MAC must be changed while adapter is disabled
            LogOperation("Step 1/5: Randomizing MAC address (adapter should be disabled)");
            await RandomizeMacAddressAsync();

            // Step 2: Enable network adapter (this will apply the new MAC address)
            LogOperation("Step 2/5: Enabling network adapter");
            await EnableNetworkAdapterAsync();

            // Step 3: Restore IP configuration
            LogOperation("Step 3/5: Restoring IP configuration");
            await RestoreIpConfigurationAsync();

            // Step 4: Enable DNS service
            LogOperation("Step 4/5: Enabling DNS service");
            await EnableDnsServiceAsync();

            // Step 5: Remove firewall rules
            LogOperation("Step 5/5: Removing firewall block rules");
            await RemoveFirewallBlockRulesAsync();

            // Capture network adapter states after operation
            var afterStates = await GetNetworkAdapterStatesAsync();

            // Log network adapter state changes
            FL.LogNetworkAdapterStates(beforeStates, afterStates, "RestoreNetwork");

            LogOperation("========================================");
            LogOperation("Network restore operation completed successfully");
            LogOperation("========================================");
        }
        catch (Exception ex)
        {
            // Capture states even on failure for debugging
            var afterStates = await GetNetworkAdapterStatesAsync();
            FL.LogNetworkAdapterStates(beforeStates, afterStates, "RestoreNetwork_Failed");
            
            FL.LogDetailedError("RestoreNetwork", ex, "Critical failure during network restore operation");
            LogOperation($"CRITICAL ERROR during network restore: {ex.Message}");
            LogOperation($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    private async Task RandomizeMacAddressAsync()
    {
        LogOperation("=== Starting MAC Address Randomization ===");

        if (IsSafeMode)
        {
            LogOperation("[SAFE MODE] Would randomize MAC address");
            return;
        }

        if (_originalSettings == null)
        {
            LogOperation("ERROR: No adapter information available for MAC randomization");
            return;
        }

        try
        {
            LogOperation($"Target adapter: {_originalSettings.AdapterName}");
            
            // Generate a random MAC address
            var random = new Random();
            var macBytes = new byte[6];
            random.NextBytes(macBytes);

            // Set the locally administered bit (bit 1 of first byte) to 1
            // and unicast bit (bit 0 of first byte) to 0
            // This ensures the MAC is valid and won't conflict with manufacturer addresses
            macBytes[0] = (byte)((macBytes[0] & 0xFE) | 0x02);

            // Convert to MAC address string format (no separators for registry)
            var newMac = string.Join("", macBytes.Select(b => b.ToString("X2")));
            
            LogOperation($"Generated new MAC address: {FormatMacAddress(newMac)}");
            LogOperation($"MAC address format for registry: {newMac}");
            LogOperation($"Locally administered bit set: {((macBytes[0] & 0x02) != 0)}");
            LogOperation($"Unicast bit clear: {((macBytes[0] & 0x01) == 0)}");

            // Set MAC address via registry
            await SetMacAddressViaRegistryAsync(_originalSettings.AdapterName, newMac);

            LogOperation("=== MAC Address Randomization Completed Successfully ===");
        }
        catch (Exception ex)
        {
            LogOperation($"ERROR: MAC address randomization failed: {ex.Message}");
            LogOperation($"Stack trace: {ex.StackTrace}");
            // Don't throw - MAC randomization failure shouldn't stop network restore
            LogOperation("Continuing with network restore despite MAC randomization failure");
        }
    }

    private string FormatMacAddress(string mac)
    {
        // Format MAC address as XX-XX-XX-XX-XX-XX
        return string.Join("-", Enumerable.Range(0, 6).Select(i => mac.Substring(i * 2, 2)));
    }

    private async Task SetMacAddressViaRegistryAsync(string adapterName, string newMac)
    {
        LogOperation($"Setting MAC address for adapter: {adapterName}");

        try
        {
            // Find the adapter using WMI
            var query = $"SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionID = '{adapterName}'";
            using var searcher = new ManagementObjectSearcher(query);
            
            string? interfaceDescription = null;
            string? adapterGuid = null;
            
            foreach (ManagementObject adapter in searcher.Get())
            {
                interfaceDescription = adapter["Description"]?.ToString();
                adapterGuid = adapter["GUID"]?.ToString();
                
                LogOperation($"Found adapter - Description: {interfaceDescription}, GUID: {adapterGuid}");
                break;
            }

            if (string.IsNullOrEmpty(interfaceDescription))
            {
                LogOperation("Could not find adapter description");
                throw new InvalidOperationException("Adapter not found");
            }

            // Create a temporary script file to execute with admin privileges
            var tempScriptPath = Path.Combine(Path.GetTempPath(), $"set_mac_{Guid.NewGuid()}.ps1");
            var tempOutputPath = Path.Combine(Path.GetTempPath(), $"set_mac_output_{Guid.NewGuid()}.txt");
            
            try
            {
                // Write PowerShell script to temp file
                var psScript = $@"
try {{
    $ErrorActionPreference = 'Stop'
    $output = @()
    $output += ""Starting MAC address change...""
    
    $regPath = 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{{4D36E972-E325-11CE-BFC1-08002BE10318}}'
    $adapters = Get-ChildItem $regPath -ErrorAction SilentlyContinue
    $found = $false
    
    foreach ($a in $adapters) {{
        try {{
            $desc = (Get-ItemProperty $a.PSPath -Name DriverDesc -ErrorAction SilentlyContinue).DriverDesc
            if ($desc -eq '{interfaceDescription}') {{
                $output += ""Found registry key: $($a.PSPath)""
                Set-ItemProperty -Path $a.PSPath -Name NetworkAddress -Value '{newMac}' -Type String -Force
                $output += ""MAC address set to: {newMac}""
                $found = $true
                break
            }}
        }} catch {{
            $output += ""Error checking adapter: $_""
        }}
    }}
    
    if (-not $found) {{
        $output += ""ERROR: Could not find adapter registry key for: {interfaceDescription}""
        $output | Out-File -FilePath '{tempOutputPath.Replace("\\", "\\\\")}' -Encoding UTF8
        exit 1
    }}
    
    $output += ""SUCCESS""
    $output | Out-File -FilePath '{tempOutputPath.Replace("\\", "\\\\")}' -Encoding UTF8
    exit 0
}} catch {{
    ""ERROR: $_"" | Out-File -FilePath '{tempOutputPath.Replace("\\", "\\\\")}' -Encoding UTF8
    exit 1
}}
";

                File.WriteAllText(tempScriptPath, psScript);
                LogOperation($"Created temp script: {tempScriptPath}");

                // Execute the script with admin privileges
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tempScriptPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",  // Run as administrator
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    
                    // Wait a moment for the output file to be written
                    await Task.Delay(500);
                    
                    // Read the output from the temp file
                    string output = "";
                    if (File.Exists(tempOutputPath))
                    {
                        output = File.ReadAllText(tempOutputPath);
                        LogOperation($"PowerShell output:\n{output}");
                    }
                    else
                    {
                        LogOperation("Warning: Output file not created");
                    }

                    if (process.ExitCode != 0 || !output.Contains("SUCCESS"))
                    {
                        throw new InvalidOperationException($"Failed to set MAC address. Exit code: {process.ExitCode}");
                    }

                    // Restart the adapter to apply the new MAC address
                    LogOperation("Restarting adapter to apply new MAC address...");
                    await RestartAdapterAsync(adapterName);
                    
                    LogOperation("MAC address set and adapter restarted successfully");
                }
                else
                {
                    throw new InvalidOperationException("Failed to start PowerShell process");
                }
            }
            finally
            {
                // Clean up temp files
                try
                {
                    if (File.Exists(tempScriptPath))
                        File.Delete(tempScriptPath);
                    if (File.Exists(tempOutputPath))
                        File.Delete(tempOutputPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
        catch (Exception ex)
        {
            LogOperation($"Error setting MAC address via registry: {ex.Message}");
            throw;
        }
    }

    private async Task RestartAdapterAsync(string adapterName)
    {
        try
        {
            // Disable the adapter
            await DisableAdapterViaWmiAsync(adapterName);
            
            // Wait a moment for the disable to complete
            await Task.Delay(2000);
            
            // Enable the adapter
            await EnableAdapterViaWmiAsync(adapterName);
            
            // Wait for the adapter to come back online
            await Task.Delay(3000);
            
            LogOperation($"Adapter {adapterName} restarted successfully");
        }
        catch (Exception ex)
        {
            LogOperation($"Error restarting adapter: {ex.Message}");
            throw;
        }
    }

    private async Task EnableNetworkAdapterAsync()
    {
        LogOperation("Enabling network adapter");

        if (IsSafeMode)
        {
            LogOperation("[SAFE MODE] Would enable network adapter");
            return;
        }

        if (_originalSettings == null || !_originalSettings.AdapterEnabled)
        {
            LogOperation("Adapter was not originally enabled, skipping");
            return;
        }

        try
        {
            await EnableAdapterViaWmiAsync(_originalSettings.AdapterName);
            LogOperation($"Network adapter enabled successfully: {_originalSettings.AdapterName}");
        }
        catch (Exception ex)
        {
            LogOperation($"Error enabling network adapter: {ex.Message}");
            throw;
        }
    }

    private async Task RestoreIpConfigurationAsync()
    {
        LogOperation("Restoring IP configuration");

        if (IsSafeMode)
        {
            LogOperation("[SAFE MODE] Would restore IP configuration");
            return;
        }

        if (_originalSettings == null)
        {
            LogOperation("No original settings to restore");
            return;
        }

        try
        {
            if (_originalSettings.DhcpEnabled)
            {
                // Restore DHCP
                await ExecuteNetshCommandAsync(
                    $"interface ip set address name=\"{_originalSettings.AdapterName}\" dhcp"
                );
                await ExecuteNetshCommandAsync(
                    $"interface ip set dns name=\"{_originalSettings.AdapterName}\" dhcp"
                );
                LogOperation("DHCP configuration restored");
            }
            else
            {
                // Restore static IP
                var gateway = string.IsNullOrEmpty(_originalSettings.DefaultGateway) 
                    ? "none" 
                    : _originalSettings.DefaultGateway;

                await ExecuteNetshCommandAsync(
                    $"interface ip set address name=\"{_originalSettings.AdapterName}\" " +
                    $"static {_originalSettings.IpAddress} {_originalSettings.SubnetMask} {gateway}"
                );

                // Restore DNS servers
                if (_originalSettings.DnsServers.Any())
                {
                    for (int i = 0; i < _originalSettings.DnsServers.Count; i++)
                    {
                        var dnsCommand = i == 0 ? "set dns" : "add dns";
                        await ExecuteNetshCommandAsync(
                            $"interface ip {dnsCommand} name=\"{_originalSettings.AdapterName}\" " +
                            $"static {_originalSettings.DnsServers[i]}"
                        );
                    }
                }

                LogOperation("Static IP configuration restored");
            }
        }
        catch (Exception ex)
        {
            LogOperation($"Error restoring IP configuration: {ex.Message}");
            throw;
        }
    }

    private async Task EnableDnsServiceAsync()
    {
        LogOperation("Enabling DNS Client service");

        if (IsSafeMode)
        {
            LogOperation("[SAFE MODE] Would enable DNS Client service");
            return;
        }

        try
        {
            using var dnsService = new ServiceController("Dnscache");
            
            if (dnsService.Status != ServiceControllerStatus.Running)
            {
                dnsService.Start();
                dnsService.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                LogOperation("DNS Client service started successfully");
            }
        }
        catch (Exception ex)
        {
            LogOperation($"Error enabling DNS service: {ex.Message}");
            // Don't throw - continue with other restore operations
        }
    }

    private async Task RemoveFirewallBlockRulesAsync()
    {
        LogOperation("Removing firewall block rules");

        if (IsSafeMode)
        {
            LogOperation("[SAFE MODE] Would remove firewall block rules");
            return;
        }

        try
        {
            // Remove outbound rule
            await ExecuteNetshCommandAsync(
                $"advfirewall firewall delete rule name=\"{PRIVACY_FIREWALL_RULE_NAME}_Out\""
            );

            // Remove inbound rule
            await ExecuteNetshCommandAsync(
                $"advfirewall firewall delete rule name=\"{PRIVACY_FIREWALL_RULE_NAME}_In\""
            );

            LogOperation("Firewall block rules removed successfully");
        }
        catch (Exception ex)
        {
            LogOperation($"Error removing firewall rules: {ex.Message}");
            // Don't throw - rules might not exist
        }
    }

    private async Task EnableAdapterViaWmiAsync(string adapterName)
    {
        await Task.Run(() =>
        {
            try
            {
                var query = $"SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionID = '{adapterName}'";
                using var searcher = new ManagementObjectSearcher(query);
                
                foreach (ManagementObject adapter in searcher.Get())
                {
                    adapter.InvokeMethod("Enable", null);
                    LogOperation($"Enabled adapter via WMI: {adapterName}");
                }
            }
            catch (Exception ex)
            {
                LogOperation($"WMI enable failed: {ex.Message}");
                throw;
            }
        });
    }

    public async Task EmergencyRestoreAsync()
    {
        LogOperation("========================================");
        LogOperation("!!! EMERGENCY RESTORE INITIATED !!!");
        LogOperation("========================================");

        // CRITICAL: Emergency restore must ALWAYS work, regardless of saved settings
        // We will try multiple methods to restore network connectivity

        var successCount = 0;
        var totalSteps = 5;

        try
        {
            // Step 1: Remove ALL firewall rules (not just ours)
            LogOperation("Emergency Step 1/5: Removing firewall rules...");
            try
            {
                await EmergencyRemoveFirewallRulesAsync();
                successCount++;
                LogOperation("✓ Firewall rules removed");
            }
            catch (Exception ex)
            {
                LogOperation($"✗ Firewall cleanup failed: {ex.Message}");
            }

            // Step 2: Enable ALL network adapters
            LogOperation("Emergency Step 2/5: Enabling all network adapters...");
            try
            {
                await EmergencyEnableAllAdaptersAsync();
                successCount++;
                LogOperation("✓ Network adapters enabled");
            }
            catch (Exception ex)
            {
                LogOperation($"✗ Adapter enable failed: {ex.Message}");
            }

            // Step 3: Reset ALL adapters to DHCP
            LogOperation("Emergency Step 3/5: Resetting to DHCP...");
            try
            {
                await EmergencyResetToDhcpAsync();
                successCount++;
                LogOperation("✓ DHCP reset completed");
            }
            catch (Exception ex)
            {
                LogOperation($"✗ DHCP reset failed: {ex.Message}");
            }

            // Step 4: Start DNS service
            LogOperation("Emergency Step 4/5: Starting DNS service...");
            try
            {
                await EmergencyStartDnsServiceAsync();
                successCount++;
                LogOperation("✓ DNS service started");
            }
            catch (Exception ex)
            {
                LogOperation($"✗ DNS service start failed: {ex.Message}");
            }

            // Step 5: Clear saved network settings to prevent future issues
            LogOperation("Emergency Step 5/5: Clearing saved settings...");
            try
            {
                var settingsService = new SettingsService();
                var settings = await settingsService.LoadSettingsAsync();
                settings.IsPrivacyModeActive = false;
                settings.OriginalNetworkSettings = null;
                await settingsService.SaveSettingsAsync(settings);
                successCount++;
                LogOperation("✓ Settings cleared");
            }
            catch (Exception ex)
            {
                LogOperation($"✗ Settings clear failed: {ex.Message}");
            }

            LogOperation("========================================");
            LogOperation($"Emergency restore completed: {successCount}/{totalSteps} steps successful");
            LogOperation("========================================");

            if (successCount >= 3)
            {
                LogOperation("Network should be restored. Please check your connection.");
            }
            else
            {
                LogOperation("WARNING: Some steps failed. Network may not be fully restored.");
                LogOperation("Please manually check: Control Panel > Network and Internet > Network Connections");
            }
        }
        catch (Exception ex)
        {
            LogOperation($"CRITICAL ERROR in emergency restore: {ex.Message}");
            LogOperation($"Stack trace: {ex.StackTrace}");
            LogOperation("Please manually restore network via Control Panel!");
        }
    }

    private async Task EmergencyRemoveFirewallRulesAsync()
    {
        LogOperation("Emergency: Removing all AppHider firewall rules");

        if (IsSafeMode)
        {
            LogOperation("[SAFE MODE] Would remove all firewall rules");
            return;
        }

        try
        {
            // Try to remove our specific rules - don't fail if they don't exist
            try
            {
                await ExecuteNetshCommandAsync(
                    $"advfirewall firewall delete rule name=\"{PRIVACY_FIREWALL_RULE_NAME}_Out\""
                );
            }
            catch { /* Ignore */ }

            try
            {
                await ExecuteNetshCommandAsync(
                    $"advfirewall firewall delete rule name=\"{PRIVACY_FIREWALL_RULE_NAME}_In\""
                );
            }
            catch { /* Ignore */ }

            LogOperation("Emergency firewall cleanup completed");
        }
        catch (Exception ex)
        {
            LogOperation($"Emergency firewall cleanup error: {ex.Message}");
            // Don't throw - continue with other recovery steps
        }
    }

    private async Task EmergencyEnableAllAdaptersAsync()
    {
        LogOperation("Emergency: Enabling all network adapters");

        if (IsSafeMode)
        {
            LogOperation("[SAFE MODE] Would enable all network adapters");
            return;
        }

        await Task.Run(() =>
        {
            try
            {
                var query = "SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionID IS NOT NULL";
                using var searcher = new ManagementObjectSearcher(query);
                
                foreach (ManagementObject adapter in searcher.Get())
                {
                    try
                    {
                        var name = adapter["NetConnectionID"]?.ToString() ?? "Unknown";
                        adapter.InvokeMethod("Enable", null);
                        LogOperation($"Emergency enabled adapter: {name}");
                        
                        // Small delay between adapters
                        System.Threading.Thread.Sleep(500);
                    }
                    catch (Exception ex)
                    {
                        LogOperation($"Failed to enable adapter: {ex.Message}");
                        // Continue with next adapter
                    }
                }
                
                LogOperation("Emergency adapter enable completed");
            }
            catch (Exception ex)
            {
                LogOperation($"Emergency adapter enable failed: {ex.Message}");
                // Don't throw - continue with other recovery steps
            }
        });
    }

    private async Task EmergencyResetToDhcpAsync()
    {
        LogOperation("Emergency: Resetting all adapters to DHCP");

        if (IsSafeMode)
        {
            LogOperation("[SAFE MODE] Would reset all adapters to DHCP");
            return;
        }

        try
        {
            var adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(a => a.NetworkInterfaceType != NetworkInterfaceType.Loopback 
                         && a.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .ToList();

            foreach (var adapter in adapters)
            {
                try
                {
                    LogOperation($"Resetting {adapter.Name} to DHCP...");
                    
                    await ExecuteNetshCommandAsync(
                        $"interface ip set address name=\"{adapter.Name}\" dhcp"
                    );
                    
                    await ExecuteNetshCommandAsync(
                        $"interface ip set dns name=\"{adapter.Name}\" dhcp"
                    );
                    
                    LogOperation($"✓ Emergency DHCP reset on: {adapter.Name}");
                }
                catch (Exception ex)
                {
                    LogOperation($"✗ Failed to reset {adapter.Name} to DHCP: {ex.Message}");
                    // Continue with next adapter
                }
            }
            
            LogOperation("Emergency DHCP reset completed");
        }
        catch (Exception ex)
        {
            LogOperation($"Emergency DHCP reset failed: {ex.Message}");
            // Don't throw - continue with other recovery steps
        }
    }

    private async Task EmergencyStartDnsServiceAsync()
    {
        LogOperation("Emergency: Starting DNS Client service");

        if (IsSafeMode)
        {
            LogOperation("[SAFE MODE] Would start DNS Client service");
            return;
        }

        try
        {
            using var dnsService = new ServiceController("Dnscache");
            
            if (dnsService.Status != ServiceControllerStatus.Running)
            {
                LogOperation("DNS service is not running, attempting to start...");
                dnsService.Start();
                dnsService.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                LogOperation("✓ Emergency DNS service started");
            }
            else
            {
                LogOperation("DNS service is already running");
            }
        }
        catch (Exception ex)
        {
            LogOperation($"✗ Emergency DNS start failed: {ex.Message}");
            // Don't throw - this is not critical
        }
    }

    public async Task SaveOriginalSettingsAsync()
    {
        LogOperation("Saving original network settings");

        try
        {
            _originalSettings = new NetworkBackup();

            var adapter = GetPrimaryNetworkAdapter();
            if (adapter == null)
            {
                LogOperation("No primary network adapter found to backup");
                return;
            }

            _originalSettings.AdapterName = adapter.Name;
            _originalSettings.AdapterEnabled = adapter.OperationalStatus == OperationalStatus.Up;

            var ipProps = adapter.GetIPProperties();

            // Save IP configuration
            var ipv4 = ipProps.UnicastAddresses
                .FirstOrDefault(ip => ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            
            if (ipv4 != null)
            {
                _originalSettings.IpAddress = ipv4.Address.ToString();
                _originalSettings.SubnetMask = ipv4.IPv4Mask.ToString();
            }

            // Save gateway
            var gateway = ipProps.GatewayAddresses.FirstOrDefault();
            if (gateway != null)
            {
                _originalSettings.DefaultGateway = gateway.Address.ToString();
            }

            // Save DNS servers
            _originalSettings.DnsServers = ipProps.DnsAddresses
                .Where(dns => dns.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(dns => dns.ToString())
                .ToList();

            // Check if DHCP is enabled
            _originalSettings.DhcpEnabled = ipProps.GetIPv4Properties()?.IsDhcpEnabled ?? false;

            // Save firewall rules (we'll track our own rules)
            _originalSettings.FirewallRules = new List<FirewallRuleBackup>();

            LogOperation($"Original network settings saved for adapter: {adapter.Name}");
            
            // CRITICAL: Save to settings file so it persists across restarts
            var settingsService = new SettingsService();
            var settings = await settingsService.LoadSettingsAsync();
            settings.OriginalNetworkSettings = _originalSettings;
            await settingsService.SaveSettingsAsync(settings);
            LogOperation("Network settings saved to configuration file");
        }
        catch (Exception ex)
        {
            LogOperation($"Error saving original settings: {ex.Message}");
            throw;
        }
    }

    private async Task<bool> CheckFirewallRuleExistsAsync(string ruleName)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall show rule name=\"{ruleName}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return process.ExitCode == 0 && !output.Contains("No rules match");
        }
        catch
        {
            return false;
        }
    }

    private void LogOperation(string message)
    {
        try
        {
            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
            
            if (IsSafeMode)
            {
                Console.WriteLine($"[SAFE MODE] {message}");
            }
        }
        catch
        {
            // Silently fail if logging fails
        }
    }

    private NetworkInterface? GetPrimaryNetworkAdapter()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(a => a.NetworkInterfaceType != NetworkInterfaceType.Loopback 
                     && a.NetworkInterfaceType != NetworkInterfaceType.Tunnel
                     && (a.OperationalStatus == OperationalStatus.Up || a.OperationalStatus == OperationalStatus.Down))
            .OrderByDescending(a => a.OperationalStatus == OperationalStatus.Up)
            .FirstOrDefault();
    }

    private async Task ExecuteNetshCommandAsync(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "netsh",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            Verb = "runas" // Run as administrator
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start netsh process");
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"netsh command failed: {error}");
        }

        LogOperation($"netsh command executed: {arguments}");
    }

    /// <summary>
    /// Gets the current state of all network adapters for logging purposes
    /// </summary>
    private async Task<Dictionary<string, bool>> GetNetworkAdapterStatesAsync()
    {
        var states = new Dictionary<string, bool>();
        
        try
        {
            await Task.Run(() =>
            {
                var adapters = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(a => a.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .ToList();

                foreach (var adapter in adapters)
                {
                    states[adapter.Name] = adapter.OperationalStatus == OperationalStatus.Up;
                }
            });
        }
        catch (Exception ex)
        {
            FL.LogDetailedError("GetNetworkAdapterStates", ex, "Failed to retrieve network adapter states");
        }

        return states;
    }
}
