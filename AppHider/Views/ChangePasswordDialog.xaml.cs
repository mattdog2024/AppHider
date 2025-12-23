using System.Windows;
using System.Windows.Input;
using AppHider.Services;

namespace AppHider.Views;

public partial class ChangePasswordDialog : Window
{
    private readonly IAuthenticationService _authService;

    public ChangePasswordDialog(IAuthenticationService authService)
    {
        InitializeComponent();
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        
        // Set focus to current password box
        Loaded += (s, e) => CurrentPasswordBox.Focus();
    }

    private void ConfirmNewPasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ChangeButton.IsEnabled)
        {
            ChangeButton_Click(sender, e);
        }
    }

    private async void ChangeButton_Click(object sender, RoutedEventArgs e)
    {
        var currentPassword = CurrentPasswordBox.Password;
        var newPassword = NewPasswordBox.Password;
        var confirmNewPassword = ConfirmNewPasswordBox.Password;

        // Validate inputs
        if (string.IsNullOrWhiteSpace(currentPassword))
        {
            StatusText.Text = "Please enter your current password.";
            StatusText.Foreground = System.Windows.Media.Brushes.Red;
            CurrentPasswordBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            StatusText.Text = "Please enter a new password.";
            StatusText.Foreground = System.Windows.Media.Brushes.Red;
            NewPasswordBox.Focus();
            return;
        }

        if (newPassword.Length < 4)
        {
            StatusText.Text = "New password must be at least 4 characters.";
            StatusText.Foreground = System.Windows.Media.Brushes.Red;
            NewPasswordBox.Focus();
            return;
        }

        if (newPassword != confirmNewPassword)
        {
            StatusText.Text = "New passwords do not match.";
            StatusText.Foreground = System.Windows.Media.Brushes.Red;
            ConfirmNewPasswordBox.Clear();
            ConfirmNewPasswordBox.Focus();
            return;
        }

        if (currentPassword == newPassword)
        {
            StatusText.Text = "New password must be different from current password.";
            StatusText.Foreground = System.Windows.Media.Brushes.Red;
            NewPasswordBox.Clear();
            ConfirmNewPasswordBox.Clear();
            NewPasswordBox.Focus();
            return;
        }

        try
        {
            // Disable button during processing
            ChangeButton.IsEnabled = false;
            StatusText.Text = "Changing password...";
            StatusText.Foreground = System.Windows.Media.Brushes.Blue;

            var success = await _authService.ChangePasswordAsync(currentPassword, newPassword);

            if (success)
            {
                MessageBox.Show(
                    "Password changed successfully!",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                
                DialogResult = true;
                Close();
            }
            else
            {
                StatusText.Text = "Current password is incorrect.";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                CurrentPasswordBox.Clear();
                CurrentPasswordBox.Focus();
                ChangeButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            StatusText.Foreground = System.Windows.Media.Brushes.Red;
            ChangeButton.IsEnabled = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
