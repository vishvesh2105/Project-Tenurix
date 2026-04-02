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
        ErrorText.Text = "";
        CreateBtn.IsEnabled = false;
        CancelBtn.IsEnabled = false;

        try
        {
            var fullName = FullNameBox.Text.Trim();
            var email = EmailBox.Text.Trim();
            var phone = PhoneBox.Text.Trim();
            var address = AddressBox.Text.Trim();
            var role = (RoleBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            var tempPass = TempPassBox.Password;

            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(role) ||
                string.IsNullOrWhiteSpace(tempPass))
            {
                ErrorText.Text = "Name, email, position, and password are required.";
                return;
            }

            await _api.CreateEmployeeAsync(fullName, email, phone, address, role, tempPass);

            MessageBox.Show("Employee created successfully!");
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
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
