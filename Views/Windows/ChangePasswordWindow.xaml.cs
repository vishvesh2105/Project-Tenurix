using System;
using System.Windows;
using Tenurix.Management.Client.Api;

namespace Tenurix.Management.Views.Windows;

public partial class ChangePasswordWindow : Window
{
    private readonly TenurixApiClient _api;

    public ChangePasswordWindow(TenurixApiClient api)
    {
        InitializeComponent();
        _api = api;
    }

    private async void UpdatePassword_Click(object sender, RoutedEventArgs e)
    {
        var current = CurrentPwdBox.Password;
        var next = NewPwdBox.Password;
        var confirm = ConfirmPwdBox.Password;

        if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(next))
        {
            MessageBox.Show("Please fill all fields.");
            return;
        }

        if (next != confirm)
        {
            MessageBox.Show("New password and confirm password do not match.");
            return;
        }

        try
        {
            await _api.ChangePasswordAsync(current, next);
            MessageBox.Show("Password updated successfully.");
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Change password failed:\n" + ex.Message);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
