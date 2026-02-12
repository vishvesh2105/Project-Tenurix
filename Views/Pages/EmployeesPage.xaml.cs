using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Tenurix.Management.Client.Api;
using Tenurix.Management.Models.Auth;

namespace Tenurix.Management.Views.Pages;

public partial class EmployeesPage : Page
{
    private sealed class EmployeeRow
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string RoleName { get; set; } = "";
        public bool IsActive { get; set; }
        public string Status => IsActive ? "Active" : "Inactive";
    }

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
        // If you have a bottom Reset button:
        if (ResetPwBtn != null) ResetPwBtn.IsEnabled = canManageUsers;
    }

    private async void EmployeesPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ReloadEmployees();
    }

    private static bool IsEmployeeRole(string? roleName)
    {
        return roleName != null && (
            roleName.Equals("Manager", StringComparison.OrdinalIgnoreCase) ||
            roleName.Equals("AssistantManager", StringComparison.OrdinalIgnoreCase) ||
            roleName.Equals("TeamLead", StringComparison.OrdinalIgnoreCase) ||
            roleName.Equals("Staff", StringComparison.OrdinalIgnoreCase)
        );
    }

    private async Task ReloadEmployees()
    {
        try
        {
            var employees = await _api.GetEmployeesAsync();

            // Safety filter: show only employee roles
            var filtered = employees
                .Where(e =>
                    e.RoleName.Equals("Manager", StringComparison.OrdinalIgnoreCase) ||
                    e.RoleName.Equals("AssistantManager", StringComparison.OrdinalIgnoreCase) ||
                    e.RoleName.Equals("TeamLead", StringComparison.OrdinalIgnoreCase) ||
                    e.RoleName.Equals("Staff", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Grid.ItemsSource = filtered;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to load employees:\n" + ex.Message);
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

        // 1) If clicked from a row button, take CommandParameter
        if (sender is Button btn && btn.CommandParameter is int idFromRow)
        {
            userId = idFromRow;
        }
        else
        {
            // 2) If clicked from the bottom button, use selected row in grid
            if (Grid.SelectedItem is not Tenurix.Management.Client.Models.EmployeeDto selected)
            {
                MessageBox.Show("Please select an employee row first.");
                return;
            }

            userId = selected.UserId;
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
