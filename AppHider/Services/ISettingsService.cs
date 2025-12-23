using AppHider.Models;

namespace AppHider.Services;

public interface ISettingsService
{
    Task<AppSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
    string SettingsFilePath { get; }
}
