using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualBasic; // for InputBox
using Tenurix.Management.Client.Api;
using Tenurix.Management.Client.Models;

namespace Tenurix.Management.Views.Pages;

public partial class PropertySubmissionsPage : Page
{
    private readonly TenurixApiClient _api;

    public PropertySubmissionsPage(TenurixApiClient api)
    {
        InitializeComponent();
        _api = api;

        Loaded += async (_, __) => await Reload();
    }

    private async System.Threading.Tasks.Task Reload()
    {
        try
        {
            Grid.ItemsSource = await _api.GetPropertySubmissionsAsync("Pending");
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to load submissions:\n" + ex.Message);
        }
    }


    private async void Refresh_Click(object sender, RoutedEventArgs e) => await Reload();

    private void View_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not PropertySubmissionDto row) return;

        var msg =
            $"PropertyId: {row.PropertyId}\n" +
            $"Landlord: {row.LandlordEmail}\n" +
            $"Address: {row.AddressLine1}, {row.City}, {row.Province} {row.PostalCode}\n" +
            $"Type: {row.PropertyType}\n" +
            $"Beds: {row.Bedrooms}\n" +
            $"Baths: {row.Bathrooms}\n" +
            $"Rent: {row.RentAmount}\n" +
            $"MediaUrl: {row.MediaUrl}";

        MessageBox.Show(msg, "Submission Details");
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
