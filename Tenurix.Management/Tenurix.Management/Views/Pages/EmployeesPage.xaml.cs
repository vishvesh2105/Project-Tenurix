using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Tenurix.Management.Client.Api;
using Tenurix.Management.Models.Auth;
using Tenurix.Management.Views.Windows;

namespace Tenurix.Management.Views.Pages;

public partial class EmployeesPage : Page
{
    private readonly TenurixApiClient _api;
    private readonly LoginResponse _session;

    public EmployeesPage(TenurixApiClient api, LoginResponse session)
    {
        InitializeComponent();
        _api = api;
        _session = session;

        Loaded += EmployeesPage_Loaded;

        ApplyPermissions();
    }

    private bool Has(string permissionKey)
    {
        return _session.Permissions != null &&
               _session.Permissions.Any(p =>
                   string.Equals(p, permissionKey, StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyPermissions()
    {
        bool canManageUsers = Has("MANAGE_USERS");
        CreateBtn.IsEnabled = canManageUsers;
        DataContext = this;
    }

    private void ViewEmployee_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not int userId)
            return;

        var selected = EmployeeGrid.ItemsSource is IEnumerable<Tenurix.Management.Client.Models.EmployeeDto> rows2
            ? rows2.FirstOrDefault(x => x.UserId == userId)
            : null;

        var win = new EmployeeProfileWindow(_api, _session, userId, selected)
        {
            Owner = Window.GetWindow(this)
        };
        win.ShowDialog();
    }

    private async void EmployeesPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ReloadEmployees();
    }

    private async Task ReloadEmployees()
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        EmptyState.Visibility = Visibility.Collapsed;
        try
        {
            var employees = await _api.GetEmployeesAsync();

            var filtered = employees
                .Where(e =>
                    e.RoleName.Equals("Manager", StringComparison.OrdinalIgnoreCase) ||
                    e.RoleName.Equals("AssistantManager", StringComparison.OrdinalIgnoreCase) ||
                    e.RoleName.Equals("TeamLead", StringComparison.OrdinalIgnoreCase) ||
                    e.RoleName.Equals("Staff", StringComparison.OrdinalIgnoreCase))
                .ToList();

            EmployeeGrid.ItemsSource = filtered;
            ResultCount.Text = $"Showing {filtered.Count} employee(s)";
            EmptyState.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to load employees:\n" + ex.Message);
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        if (!Has("MANAGE_USERS"))
        {
            MessageBox.Show("You do not have permission to create employees.");
            return;
        }

        var win = new CreateEmployeeWindow(_api)
        {
            Owner = Window.GetWindow(this)
        };

        if (win.ShowDialog() == true)
            await ReloadEmployees();
    }

    private async void ResetPassword_Click(object sender, RoutedEventArgs e)
    {
        if (!Has("MANAGE_USERS"))
        {
            MessageBox.Show("You do not have permission to reset passwords.");
            return;
        }

        int userId = 0;

        if (sender is Button btn && btn.CommandParameter is int idFromRow)
        {
            userId = idFromRow;
        }
        else
        {
            MessageBox.Show("Please use the Reset Password button on an employee card.");
            return;
        }

        try
        {
            var temp = await _api.ResetEmployeePasswordAsync(userId);
            MessageBox.Show($"Temporary password:\n{temp}\n\nUser must change password on next login.");
            await ReloadEmployees();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to reset password:\n" + ex.Message);
        }
    }
}
