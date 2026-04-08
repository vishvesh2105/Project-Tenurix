using System.Windows;
using Tenurix.Management.Client.Api;
using Tenurix.Management.Services;

namespace Tenurix.Management.Views;

public partial class LoginWindow : Window
{
    private readonly TenurixApiClient _api = new TenurixApiClient(AppConfig.ApiBaseUrl);

    public LoginWindow()
    {
        InitializeComponent();
    }

    private void TogglePassword_Click(object sender, RoutedEventArgs e)
    {
        if (PasswordVisible.Visibility == Visibility.Collapsed)
        {
            PasswordVisible.Text = PasswordBox.Password;
            PasswordVisible.Visibility = Visibility.Visible;
            PasswordBox.Visibility = Visibility.Collapsed;
            TogglePasswordBtn.Content = "\uED1A";
            TogglePasswordBtn.ToolTip = "Hide password";
        }
        else
        {
            PasswordBox.Password = PasswordVisible.Text;
            PasswordBox.Visibility = Visibility.Visible;
            PasswordVisible.Visibility = Visibility.Collapsed;
            TogglePasswordBtn.Content = "\uE7B3";
            TogglePasswordBtn.ToolTip = "Show password";
        }
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        LoginBtn.IsEnabled = false;
        LoginBtn.Content = "Signing in...";

        try
        {
            var email = EmailBox.Text.Trim();
            var pass = PasswordVisible.Visibility == Visibility.Visible
                ? PasswordVisible.Text
                : PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass))
            {
                ErrorText.Text = "Please enter your email and password.";
                return;
            }

            // Step 1: Validate credentials — triggers 2FA email
            var twoFa = await _api.LoginAsync(email, pass);

            // Open the verification window
            var verifyWindow = new VerificationWindow(_api, email, pass, twoFa.MaskedEmail);
            verifyWindow.Show();

            this.Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
        }
        finally
        {
            LoginBtn.IsEnabled = true;
            LoginBtn.Content = "Sign In";
        }
    }
}
