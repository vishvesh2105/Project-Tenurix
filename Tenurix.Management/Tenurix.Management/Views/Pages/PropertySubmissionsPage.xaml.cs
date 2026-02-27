using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualBasic; // for InputBox
using Tenurix.Management.Client.Api;
using Tenurix.Management.Client.Models;
using Tenurix.Management.Models.Auth;
using Tenurix.Management.Views.Windows;


namespace Tenurix.Management.Views.Pages;

public partial class PropertySubmissionsPage : Page
{
    private readonly TenurixApiClient _api;
    private readonly LoginResponse? _session;
    private readonly bool _isManager;

    public PropertySubmissionsPage(TenurixApiClient api, LoginResponse? session = null)
    {
        _api = api;
        _session = session;

        // Manager / AssistantManager can approve/reject any status
        var role = session?.RoleName ?? "";
        _isManager = role.Equals("Manager", StringComparison.OrdinalIgnoreCase)
                  || role.Equals("AssistantManager", StringComparison.OrdinalIgnoreCase);

        InitializeComponent();

        Loaded += async (_, __) => await Reload();
    }

    private string SelectedStatus()
    {
        var item = StatusFilter.SelectedItem as ComboBoxItem;
        return item?.Content?.ToString() ?? "All";
    }

    private async System.Threading.Tasks.Task Reload()
    {
        if (Grid == null) return; // guard: called before InitializeComponent finishes
        try
        {
            var submissions = await _api.GetPropertySubmissionsAsync(SelectedStatus());

            // Set approve/reject visibility per item based on role + status
            foreach (var s in submissions)
            {
                var isPending = s.SubmissionStatus == "Pending" || s.SubmissionStatus == "OnHold";
                // Manager can approve/reject any status; Staff only Pending/OnHold
                s.CanApproveReject = _isManager || isPending;
            }

            Grid.ItemsSource = submissions;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to load submissions:\n" + ex.Message);
        }
    }

    private async void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => await Reload();

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await Reload();

    private void View_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not PropertySubmissionDto row) return;

        var win = new PropertySubmissionReviewWindow(_api, row.PropertyId)
        {
            Owner = Window.GetWindow(this)
        };

        win.ShowDialog();
    }

    private async void Assign_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not PropertySubmissionDto row)
        {
            MessageBox.Show("Select a submission row first.");
            return;
        }

        try
        {
            // You must have this client method already (from employees page):
            // If your method name differs, rename it here to match.
            var employees = await _api.GetEmployeesAsync();

            var list = string.Join(
                Environment.NewLine,
                employees.Select(x => $"{x.UserId} - {x.Email} ({x.RoleName})"));

            var input = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter Employee UserId to assign:\n\n" + list,
                "Assign Property Submission",
                "");

            if (!int.TryParse(input, out var employeeId))
                return;

            await _api.AssignPropertySubmissionAsync(row.PropertyId, employeeId);
            await Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Assign failed:\n" + ex.Message);
        }
    }

    private async void Approve_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not PropertySubmissionDto row) return;

        try
        {
            await _api.ApprovePropertySubmissionAsync(row.PropertyId, note: "Approved by management.");
            await Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Approve failed:\n" + ex.Message);
        }
    }

    private async void Reject_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not PropertySubmissionDto row) return;

        var reason = Interaction.InputBox("Reason for rejection:", "Reject Submission", "");
        if (string.IsNullOrWhiteSpace(reason)) return;

        try
        {
            await _api.RejectPropertySubmissionAsync(row.PropertyId, reason);
            await Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Reject failed:\n" + ex.Message);
        }
    }
}
