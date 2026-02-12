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

        try
        {
            var email = EmailBox.Text.Trim();
            var pass = PasswordBox.Password;

            var session = await _api.LoginAsync(email, pass);

            _api.SetToken(session.Token);


            var shell = new ShellWindow(_api, session);
            shell.Show();


            this.Close();

        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
        }
        finally
        {
            LoginBtn.IsEnabled = true;
        }
    }
}
