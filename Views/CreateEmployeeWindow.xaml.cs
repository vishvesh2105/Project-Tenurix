using System;
using System.Windows;
using System.Windows.Controls;
using Tenurix.Management.Client.Api;

namespace Tenurix.Management.Views;

public partial class CreateEmployeeWindow : Window
{
    private readonly TenurixApiClient _api;

    public CreateEmployeeWindow(TenurixApiClient api)
    {
        InitializeComponent();
        _api = api;

        RoleBox.SelectedIndex = 0;
    }

   

private async void Create_Click(object sender, RoutedEventArgs e)
{
    ErrorText.Text = "Step 1: Click received...";
    CreateBtn.IsEnabled = false;
    CancelBtn.IsEnabled = false;

    try
    {
        ErrorText.Text = "Step 2: Validating fields...";

        var fullName = FullNameBox.Text.Trim();
        var email = EmailBox.Text.Trim();
        var role = (RoleBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        var tempPass = TempPassBox.Password;

        if (string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(role) ||
            string.IsNullOrWhiteSpace(tempPass))
        {
            ErrorText.Text = "All fields are required.";
            return;
        }

        ErrorText.Text = "Step 3: Calling API...";
        await _api.CreateEmployeeAsync(fullName, email, role, tempPass);

        ErrorText.Text = "Step 4: Success!";
        MessageBox.Show("Employee created successfully!");
        DialogResult = true;
        Close();
    }
    catch (Exception ex)
    {
        ErrorText.Text = "FAILED: " + ex.Message;
    }
    finally
    {
        CreateBtn.IsEnabled = true;
        CancelBtn.IsEnabled = true;
    }
}

private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

}
