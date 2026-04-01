using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualBasic;
using Tenurix.Management.Client.Api;
using Tenurix.Management.Client.Models;
using Tenurix.Management.Views.Windows;

namespace Tenurix.Management.Views.Pages;

public partial class LeaseApplicationsPage : Page
{
    private readonly TenurixApiClient _api;

    public LeaseApplicationsPage(TenurixApiClient api)
    {
        _api = api;
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
        if (Grid == null) return;
        try
        {
            Grid.ItemsSource = await _api.GetLeaseApplicationsAsync(SelectedStatus());
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to load lease applications. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await Reload();

    private async void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => await Reload();

    private void View_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not LeaseApplicationDto row) return;
        var win = new LeaseApplicationReviewWindow(_api, row.ApplicationId)
        {
            Owner = Window.GetWindow(this)
        };
        win.ShowDialog();

        // If the review window approved the application, switch to Approved filter
        // so the "Edit & Send Agreement" button is immediately visible
        if (win.DialogResult == true)
            StatusFilter.SelectedIndex = 2; // triggers SelectionChanged → Reload
    }

    private async void Approve_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not LeaseApplicationDto row) return;

        try
        {
            await _api.ApproveLeaseApplicationAsync(row.ApplicationId, note: "Approved by management.");

            // Switch filter to "Approved" so the Edit & Send Agreement button is visible
            StatusFilter.SelectedIndex = 2; // triggers SelectionChanged → Reload automatically
        }
        catch (Exception ex)
        {
            MessageBox.Show("Approve failed. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void Reject_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not LeaseApplicationDto row) return;

        var reason = Interaction.InputBox("Reason for rejection:", "Reject Lease Application", "");
        if (string.IsNullOrWhiteSpace(reason)) return;

        try
        {
            await _api.RejectLeaseApplicationAsync(row.ApplicationId, reason);
            await Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Reject failed. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void EditSend_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not LeaseApplicationDto row) return;

        if (!row.LeaseId.HasValue)
        {
            MessageBox.Show(
                "No lease record found for this application. Please re-approve the application to generate the lease.",
                "Lease Not Found",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var win = new LeaseEditSendWindow(_api, row)
        {
            Owner = Window.GetWindow(this)
        };
        win.ShowDialog();
    }
}
