using System.Windows;
using Tenurix.Management.Client.Api;

namespace Tenurix.Management.Views;

public partial class LoginWindow : Window
{
    private readonly TenurixApiClient _api =
        new TenurixApiClient("https://capstone-api-aryan.azurewebsites.net/");

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
            var result = await _api.LoginAsync(email, pass);
            var shell = new ShellWindow();
            shell.Show();
            this.Close();
        }
        catch (Exception ex) { ErrorText.Text = ex.Message; }
        finally { LoginBtn.IsEnabled = true; LoginBtn.Content = "Sign In"; }
    }
}
