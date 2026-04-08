using System;
using System.Windows;
using Tenurix.Management.Client.Api;
using Tenurix.Management.Models.Auth;
using Tenurix.Management.Client.Models;

namespace Tenurix.Management.Views.Windows;

public partial class EmployeeProfileWindow : Window
{
    private readonly TenurixApiClient _api;
    private readonly int _userId;

    public EmployeeProfileWindow(TenurixApiClient api, LoginResponse session, int userId, EmployeeDto? employee)
    {
        InitializeComponent();
        _api = api;
        _userId = userId;

        // Set header from basic info while loading full details
        if (employee != null)
        {
            HeaderName.Text = employee.FullName;
            HeaderSub.Text = $"{employee.Email} • {employee.RoleName}";
            StatusText.Text = employee.IsActive ? "Active" : "Inactive";
        }

        Loaded += async (_, _) => await LoadDetails();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private async System.Threading.Tasks.Task LoadDetails()
    {
        try
        {
            var detail = await _api.GetEmployeeDetailAsync(_userId);

            // Header
            HeaderName.Text = detail.FullName;
            HeaderSub.Text = $"{detail.Email} • {detail.RoleName}";
            StatusText.Text = detail.IsActive ? "Active" : "Inactive";

            if (!detail.IsActive)
            {
                StatusBadge.Background = (System.Windows.Media.Brush)FindResource("Panel2");
                StatusText.Foreground = (System.Windows.Media.Brush)FindResource("TextMuted");
            }

            // Details
            DetailName.Text = detail.FullName;
            DetailEmail.Text = detail.Email;
            DetailPhone.Text = string.IsNullOrWhiteSpace(detail.Phone) ? "Not provided" : detail.Phone;
            DetailAddress.Text = string.IsNullOrWhiteSpace(detail.Address) ? "Not provided" : detail.Address;
            DetailRole.Text = detail.RoleName;

            // Properties
            PropertiesGrid.ItemsSource = detail.AssignedProperties;
            PropCountText.Text = detail.AssignedProperties.Count == 0
                ? "No properties assigned to this employee."
                : $"{detail.AssignedProperties.Count} propert{(detail.AssignedProperties.Count == 1 ? "y" : "ies")} assigned";
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to load employee details:\n" + ex.Message);
        }
    }
}
