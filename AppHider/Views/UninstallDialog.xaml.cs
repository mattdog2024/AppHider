using System.Windows;
using AppHider.Services;

namespace AppHider.Views;

/// <summary>
/// Dialog for authorizing application uninstallation with password.
/// </summary>
public partial class UninstallDialog : Window
{
    private readonly UninstallProtectionService _uninstallProtection;
    
    public bool IsAuthorized { get; private set; }

    public UninstallDialog(UninstallProtectionService uninstallProtection)
    {
        InitializeComponent();
        _uninstallProtection = uninstallProtection ?? throw new ArgumentNullException(nameof(uninstallProtection));
        
        // Focus password box
        Loaded += (s, e) => PasswordBox.Focus();
        
        // Handle Enter key in password box
        PasswordBox.KeyDown += (s, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                AuthorizeButton_Click(s, e);
            }
        };
    }

    private async void AuthorizeButton_Click(object sender, RoutedEventArgs e)
    {
        var password = PasswordBox.Password;

        if (string.IsNullOrEmpty(password))
        {
            ShowError("Please enter your password.");
            return;
        }

        try
        {
            // Disable button during validation
            var button = sender as System.Windows.Controls.Button;
            if (button != null)
            {
                button.IsEnabled = false;
            }

            var isValid = await _uninstallProtection.ValidateUninstallAuthorizationAsync(password);

            if (isValid)
            {
                IsAuthorized = true;
                
                // Prepare for uninstall
                await _uninstallProtection.PrepareForUninstallAsync();
                
                MessageBox.Show(
                    "Authorization successful.\n\nFile protection and auto-startup have been disabled. You may now uninstall the application.",
                    "Uninstall Authorized",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                
                DialogResult = true;
                Close();
            }
            else
            {
                ShowError("Invalid password. Please try again.");
                PasswordBox.Clear();
                PasswordBox.Focus();
            }

            if (button != null)
            {
                button.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            ShowError($"Error: {ex.Message}");
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        IsAuthorized = false;
        DialogResult = false;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
