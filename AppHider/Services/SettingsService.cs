using System.IO;
using AppHider.Models;
using Newtonsoft.Json;

namespace AppHider.Services;

public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Include,
        DefaultValueHandling = DefaultValueHandling.Include
    };

    public SettingsService()
    {
        // Store settings in AppData\Local\AppHider
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appDataPath, "AppHider");
        
        // Ensure directory exists
        if (!Directory.Exists(appFolder))
        {
            Directory.CreateDirectory(appFolder);
        }
        
        _settingsFilePath = Path.Combine(appFolder, "settings.json");
    }

    public string SettingsFilePath => _settingsFilePath;

    public async Task<AppSettings> LoadSettingsAsync()
    {
        try
        {
            // If file doesn't exist, return default settings
            if (!File.Exists(_settingsFilePath))
            {
                return CreateDefaultSettings();
            }

            // Read and deserialize the file
            var json = await File.ReadAllTextAsync(_settingsFilePath);
            
            // Handle empty or whitespace-only files
            if (string.IsNullOrWhiteSpace(json))
            {
                return CreateDefaultSettings();
            }

            var settings = JsonConvert.DeserializeObject<AppSettings>(json, JsonSettings);
            
            // Handle corrupted/null deserialization
            if (settings == null)
            {
                return CreateDefaultSettings();
            }

            return settings;
        }
        catch (JsonException)
        {
            // Handle corrupted JSON by returning defaults
            return CreateDefaultSettings();
        }
        catch (IOException)
        {
            // Handle file access issues by returning defaults
            return CreateDefaultSettings();
        }
        catch (UnauthorizedAccessException)
        {
            // Handle permission issues by returning defaults
            return CreateDefaultSettings();
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        try
        {
            // Update last modified timestamp
            settings.LastModified = DateTime.UtcNow;

            // Create backup of existing settings if file exists
            if (File.Exists(_settingsFilePath))
            {
                var backupPath = _settingsFilePath + ".backup";
                File.Copy(_settingsFilePath, backupPath, overwrite: true);
            }

            // Serialize to JSON
            var json = JsonConvert.SerializeObject(settings, JsonSettings);

            // Write to file
            await File.WriteAllTextAsync(_settingsFilePath, json);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            // Re-throw with more context
            throw new InvalidOperationException($"Failed to save settings to {_settingsFilePath}", ex);
        }
    }

    private static AppSettings CreateDefaultSettings()
    {
        return new AppSettings
        {
            PasswordHash = string.Empty,
            ToggleHotkey = new HotkeyConfig 
            { 
                Key = System.Windows.Input.Key.F9, 
                Modifiers = System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt 
            },
            MenuHotkey = new HotkeyConfig 
            { 
                Key = System.Windows.Input.Key.F10, 
                Modifiers = System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt 
            },
            EmergencyDisconnectHotkey = new HotkeyConfig 
            { 
                Key = System.Windows.Input.Key.F8, 
                Modifiers = System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt 
            },
            HiddenApplicationNames = new List<string>(),
            OriginalNetworkSettings = null,
            SafeModeEnabled = false,
            LastModified = DateTime.UtcNow
        };
    }
}
