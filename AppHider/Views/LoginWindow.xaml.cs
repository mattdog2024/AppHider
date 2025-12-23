using System.Windows;
using System.Windows.Input;
using AppHider.Services;

namespace AppHider.Views;

public partial class LoginWindow : Window
{
    private readonly IAuthenticationService _authService;
    private bool _isFirstRun;

    public bool IsAuthenticated { get; private set; }

    public LoginWindow(IAuthenticationService authService)
    {
        InitializeComponent();
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        
        InitializeUI();
        
        // Set focus to password box
        Loaded += (s, e) => PasswordBox.Focus();
        
        // Handle Enter key
        PasswordBox.KeyDown += PasswordBox_KeyDown;
        ConfirmPasswordBox.KeyDown += PasswordBox_KeyDown;
    }

    private void InitializeUI()
    {
        _isFirstRun = _authService.IsFirstRun;

        if (_isFirstRun)
        {
            // First run - show password setup
            Title = "App Hider - Setup";
            InstructionText.Text = "Create a password:";
            ConfirmPasswordBox.Visibility = Visibility.Visible;
            LoginButton.Content = "Set Password";
        }
        else
        {
            // Normal login
            Title = "App Hider - Login";
            InstructionText.Text = "Enter your password:";
            ConfirmPasswordBox.Visibility = Visibility.Collapsed;
            LoginButton.Content = "Login";
            
            // Check if locked out
            CheckLockoutStatus();
        }
    }

    private void CheckLockoutStatus()
    {
        if (_authService.LockoutEndTime.HasValue)
        {
            var remainingTime = _authService.LockoutEndTime.Value - DateTime.UtcNow;
            if (remainingTime.TotalSeconds > 0)
            {
                // Still locked out
                LoginButton.IsEnabled = false;
                StatusText.Text = $"Account locked. Try again in {(int)remainingTime.TotalSeconds} seconds.";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                
                // Start countdown timer
                StartLockoutCountdown(remainingTime);
            }
        }
    }

    private async void StartLockoutCountdown(TimeSpan remainingTime)
    {
        while (remainingTime.TotalSeconds > 0)
        {
            await Task.Delay(1000);
            remainingTime = remainingTime.Subtract(TimeSpan.FromSeconds(1));
            
            if (remainingTime.TotalSeconds > 0)
            {
                StatusText.Text = $"Account locked. Try again in {(int)remainingTime.TotalSeconds} seconds.";
            }
            else
            {
                // Lockout ended
                StatusText.Text = "";
                LoginButton.IsEnabled = true;
            }
        }
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && LoginButton.IsEnabled)
        {
            LoginButton_Click(sender, e);
        }
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        // Clear status text when user starts typing
        if (!string.IsNullOrEmpty(StatusText.Text) && 
            !StatusText.Text.Contains("locked"))
        {
            StatusText.Text = "";
        }
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        var password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(password))
        {
            StatusText.Text = "Password cannot be empty.";
            StatusText.Foreground = System.Windows.Media.Brushes.Red;
            return;
        }

        if (_isFirstRun)
        {
            // First run - set password
            await HandleFirstRunSetup(password);
        }
        else
        {
            // Normal login
            await HandleLogin(password);
        }
    }

    private async Task HandleFirstRunSetup(string password)
    {
        var confirmPassword = ConfirmPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(confirmPassword))
        {
            StatusText.Text = "Please confirm your password.";
            StatusText.Foreground = System.Windows.Media.Brushes.Red;
            return;
        }

        if (password != confirmPassword)
        {
            StatusText.Text = "Passwords do not match.";
            StatusText.Foreground = System.Windows.Media.Brushes.Red;
            ConfirmPasswordBox.Clear();
            return;
        }

        if (password.Length < 4)
        {
            StatusText.Text = "Password must be at least 4 characters.";
            StatusText.Foreground = System.Windows.Media.Brushes.Red;
            return;
        }

        try
        {
            // Disable button during processing
            LoginButton.IsEnabled = false;
            StatusText.Text = "Setting up...";
            StatusText.Foreground = System.Windows.Media.Brushes.Blue;

            await _authService.SetPasswordAsync(password);

            IsAuthenticated = true;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            StatusText.Foreground = System.Windows.Media.Brushes.Red;
            LoginButton.IsEnabled = true;
        }
    }

    private async Task HandleLogin(string password)
    {
        try
        {
            // Disable button during processing
            LoginButton.IsEnabled = false;
            StatusText.Text = "Authenticating...";
            StatusText.Foreground = System.Windows.Media.Brushes.Blue;

            var isValid = await _authService.ValidatePasswordAsync(password);

            if (isValid)
            {
                IsAuthenticated = true;
                DialogResult = true;
                Close();
            }
            else
            {
                // Check if locked out
                if (_authService.LockoutEndTime.HasValue)
                {
                    var remainingTime = _authService.LockoutEndTime.Value - DateTime.UtcNow;
                    if (remainingTime.TotalSeconds > 0)
                    {
                        StatusText.Text = $"Too many failed attempts. Account locked for {(int)remainingTime.TotalSeconds} seconds.";
                        StatusText.Foreground = System.Windows.Media.Brushes.Red;
                        PasswordBox.Clear();
                        StartLockoutCountdown(remainingTime);
                        return;
                    }
                }

                // Show failed attempts
                var attemptsRemaining = 3 - _authService.FailedAttempts;
                if (attemptsRemaining > 0)
                {
                    StatusText.Text = $"Invalid password. {attemptsRemaining} attempt(s) remaining.";
                }
                else
                {
                    StatusText.Text = "Invalid password.";
                }
                
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                PasswordBox.Clear();
                PasswordBox.Focus();
                LoginButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            StatusText.Foreground = System.Windows.Media.Brushes.Red;
            LoginButton.IsEnabled = true;
        }
    }
}
