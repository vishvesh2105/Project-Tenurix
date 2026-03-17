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
    private sealed class EmployeeRow
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string RoleName { get; set; } = "";
        public bool IsActive { get; set; }
        public string Status => IsActive ? "Active" : "Inactive";
    }

    public Visibility CanViewAttendanceVisibility
    {
        get
        {
            // Permission-based
            if (Has("ATTENDANCE_VIEW_ALL")) return Visibility.Visible;

            // Role fallback (for demo / if DB permissions not wired yet)
            var role = _session.RoleName ?? "";
            if (role.Equals("Manager", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("TeamLead", StringComparison.OrdinalIgnoreCase))
                return Visibility.Visible;

            return Visibility.Collapsed;
        }
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
        // Refresh bindings for visibility properties
        DataContext = this;
    }

    private void ViewEmployee_Click(object sender, RoutedEventArgs e)
    {
        if (!Has("ATTENDANCE_VIEW_ALL"))
        {
            MessageBox.Show("You do not have permission to view employee attendance.");
            return;
        }

        if (sender is not Button btn || btn.CommandParameter is not int userId)
            return;

        var selected = Grid.ItemsSource is IEnumerable<Tenurix.Management.Client.Models.EmployeeDto> rows2
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

        // Get userId from the button's CommandParameter
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
