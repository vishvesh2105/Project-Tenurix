using System.Windows;

namespace Tenurix.Management.Views;

public partial class LoginWindow : Window
{

    public LoginWindow()
    {
        InitializeComponent();
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        LoginBtn.IsEnabled = false;
        LoginBtn.Content = "Signing in...";

        try
        {
            var email = EmailBox.Text.Trim();
            var pass = PasswordBox.Password;

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
