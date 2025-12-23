namespace AppHider.Services;

public interface IAuthenticationService
{
    bool IsFirstRun { get; }
    bool IsAuthenticated { get; }
    Task<bool> ValidatePasswordAsync(string password);
    Task SetPasswordAsync(string password);
    Task<bool> ChangePasswordAsync(string oldPassword, string newPassword);
    int FailedAttempts { get; }
    DateTime? LockoutEndTime { get; }
}
