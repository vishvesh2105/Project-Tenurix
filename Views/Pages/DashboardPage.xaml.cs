using System;
using System.Windows;
using System.Windows.Controls;
using Tenurix.Management.Client.Api;

namespace Tenurix.Management.Views.Pages;

public partial class DashboardPage : Page
{
    private readonly TenurixApiClient _api;

    public DashboardPage(TenurixApiClient api)
    {
        InitializeComponent();
        _api = api;

        Loaded += DashboardPage_Loaded;
    }

    private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var d = await _api.GetDashboardAsync();

            PropCountText.Text = d.PendingPropertySubmissions.ToString();
            LeaseCountText.Text = d.PendingLeaseApplications.ToString();
            IssueCountText.Text = d.OpenIssues.ToString();
            EmpCountText.Text = d.ActiveEmployees.ToString();

            RecentPropsGrid.ItemsSource = d.RecentPropertySubmissions;
            RecentAppsGrid.ItemsSource = d.RecentLeaseApplications;
            RecentIssuesGrid.ItemsSource = d.RecentIssues;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Dashboard load failed:\n" + ex.Message);
        }
    }
}
