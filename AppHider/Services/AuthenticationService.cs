using System.Security.Cryptography;
using System.Text;
using AppHider.Models;

namespace AppHider.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly ISettingsService _settingsService;
    private AppSettings? _currentSettings;
    private int _failedAttempts;
    private DateTime? _lockoutEndTime;
    private bool _isAuthenticated;

    public AuthenticationService(ISettingsService settingsService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _failedAttempts = 0;
        _lockoutEndTime = null;
        _isAuthenticated = false;
        
        // Load settings synchronously during initialization using Task.Run to avoid deadlock
        _currentSettings = Task.Run(async () => await _settingsService.LoadSettingsAsync()).Result;
    }

    public bool IsFirstRun => string.IsNullOrEmpty(_currentSettings?.PasswordHash);

    public bool IsAuthenticated => _isAuthenticated;

    public int FailedAttempts => _failedAttempts;

    public DateTime? LockoutEndTime => _lockoutEndTime;

    public async Task<bool> ValidatePasswordAsync(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        // Load settings if not already loaded
        if (_currentSettings == null)
        {
            _currentSettings = await _settingsService.LoadSettingsAsync();
        }

        // Check if we're in lockout period
        if (_lockoutEndTime.HasValue)
        {
            if (DateTime.UtcNow < _lockoutEndTime.Value)
            {
                // Still locked out
                return false;
            }
            else
            {
                // Lockout period has ended, reset
                _lockoutEndTime = null;
                _failedAttempts = 0;
            }
        }

        // First run - no password set yet
        if (IsFirstRun)
        {
            return false;
        }

        // Hash the provided password and compare
        var hashedPassword = HashPassword(password);
        var isValid = hashedPassword == _currentSettings.PasswordHash;

        if (isValid)
        {
            // Reset failed attempts on successful login
            _failedAttempts = 0;
            _lockoutEndTime = null;
            _isAuthenticated = true;
        }
        else
        {
            // Increment failed attempts
            _failedAttempts++;
            _isAuthenticated = false;

            // Check if we need to trigger lockout (3 consecutive failures)
            if (_failedAttempts >= 3)
            {
                _lockoutEndTime = DateTime.UtcNow.AddSeconds(60);
            }
        }

        return isValid;
    }

    public async Task SetPasswordAsync(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Password cannot be null or empty", nameof(password));
        }

        // Load settings if not already loaded
        if (_currentSettings == null)
        {
            _currentSettings = await _settingsService.LoadSettingsAsync();
        }

        // Hash and store the password
        _currentSettings.PasswordHash = HashPassword(password);
        
        // Save settings
        await _settingsService.SaveSettingsAsync(_currentSettings);

        // Mark as authenticated after setting password
        _isAuthenticated = true;
        _failedAttempts = 0;
        _lockoutEndTime = null;
    }

    public async Task<bool> ChangePasswordAsync(string oldPassword, string newPassword)
    {
        if (string.IsNullOrEmpty(oldPassword))
        {
            throw new ArgumentException("Old password cannot be null or empty", nameof(oldPassword));
        }

        if (string.IsNullOrEmpty(newPassword))
        {
            throw new ArgumentException("New password cannot be null or empty", nameof(newPassword));
        }

        // Load settings if not already loaded
        if (_currentSettings == null)
        {
            _currentSettings = await _settingsService.LoadSettingsAsync();
        }

        // Verify the old password
        var oldPasswordHash = HashPassword(oldPassword);
        if (oldPasswordHash != _currentSettings.PasswordHash)
        {
            return false;
        }

        // Set the new password
        _currentSettings.PasswordHash = HashPassword(newPassword);
        
        // Save settings
        await _settingsService.SaveSettingsAsync(_currentSettings);

        return true;
    }

    /// <summary>
    /// Hashes a password using SHA-256
    /// </summary>
    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var hashBytes = sha256.ComputeHash(passwordBytes);
        
        // Convert to hex string
        var sb = new StringBuilder();
        foreach (var b in hashBytes)
        {
            sb.Append(b.ToString("x2"));
        }
        
        return sb.ToString();
    }
}
