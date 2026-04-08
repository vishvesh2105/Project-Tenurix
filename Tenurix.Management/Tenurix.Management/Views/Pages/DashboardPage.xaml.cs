using System;
using System.Windows;
using System.Windows.Controls;
using Tenurix.Management.Client.Api;
using Tenurix.Management.Views.Windows;

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
        LoadingOverlay.Visibility = Visibility.Visible;
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
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void ViewProp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is null) return;

        try
        {
            dynamic row = fe.DataContext;
            int propertyId = (int)row.PropertyId;

            var win = new PropertySubmissionReviewWindow(_api, propertyId)
            {
                Owner = Window.GetWindow(this)
            };
            win.ShowDialog();
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}"); }
    }

    private void ViewApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is null) return;

        try
        {
            dynamic row = fe.DataContext;
            int appId = (int)row.ApplicationId;

            var win = new LeaseApplicationReviewWindow(_api, appId)
            {
                Owner = Window.GetWindow(this)
            };
            win.ShowDialog();
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}"); }
    }

    private void ViewIssue_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is null) return;

        try
        {
            dynamic row = fe.DataContext;
            int issueId = (int)row.IssueId;

            var win = new IssueDetailWindow(_api, issueId)
            {
                Owner = Window.GetWindow(this)
            };
            win.ShowDialog();
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}"); }
    }
}
