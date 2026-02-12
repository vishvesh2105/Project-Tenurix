using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualBasic;
using Tenurix.Management.Client.Api;
using Tenurix.Management.Client.Models;

namespace Tenurix.Management.Views.Pages;

public partial class LeaseApplicationsPage : Page
{
    private readonly TenurixApiClient _api;

    public LeaseApplicationsPage(TenurixApiClient api)
    {
        InitializeComponent();
        _api = api;

        Loaded += async (_, __) => await Reload();
    }



    private async System.Threading.Tasks.Task Reload()
    {
        try
        {
            Grid.ItemsSource = await _api.GetLeaseApplicationsAsync("Pending");
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to load lease applications:\n" + ex.Message);
        }
    }

    private LeaseApplicationDto? Selected() => Grid.SelectedItem as LeaseApplicationDto;

    private async void Approve_Click(object sender, RoutedEventArgs e)
    {
        var row = Selected();
        if (row == null)
        {
            MessageBox.Show("Please select an application first.");
            return;
        }

        try
        {
            await _api.ApproveLeaseApplicationAsync(row.ApplicationId, note: "Approved by management.");
            await Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Approve failed:\n" + ex.Message);
        }
    }

    private async void Reject_Click(object sender, RoutedEventArgs e)
    {
        var row = Selected();
        if (row == null)
        {
            MessageBox.Show("Please select an application first.");
            return;
        }

        var reason = Interaction.InputBox("Reason for rejection:", "Reject Lease Application", "");
        if (string.IsNullOrWhiteSpace(reason)) return;

        try
        {
            await _api.RejectLeaseApplicationAsync(row.ApplicationId, reason);
            await Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Reject failed:\n" + ex.Message);
        }
    }
}
