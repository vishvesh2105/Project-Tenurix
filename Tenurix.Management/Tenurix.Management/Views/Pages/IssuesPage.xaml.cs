using System;
using System.Windows;
using System.Windows.Controls;
using Tenurix.Management.Client.Api;
using Tenurix.Management.Client.Models;

namespace Tenurix.Management.Views.Pages;

public partial class IssuesPage : Page
{
    private readonly TenurixApiClient _api;

    public IssuesPage(TenurixApiClient api)
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
        LoadingOverlay.Visibility = Visibility.Visible;
        EmptyState.Visibility = Visibility.Collapsed;
        try
        {
            var rows = await _api.GetIssuesAsync(SelectedStatus());
            Grid.ItemsSource = rows;
            EmptyState.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to load issues. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await Reload();

    private async void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => await Reload();

    private void ViewDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        var row = fe.DataContext as IssueDto;
        if (row == null) return;

        var win = new Tenurix.Management.Views.Windows.IssueDetailWindow(_api, row.IssueId)
        {
            Owner = Window.GetWindow(this)
        };
        win.ShowDialog();
    }

    private async void MarkInProgress_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not IssueDto row) return;

        try
        {
            await _api.UpdateIssueStatusAsync(row.IssueId, "InProgress");
            await Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to update status. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void MarkResolved_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not IssueDto row) return;

        try
        {
            await _api.UpdateIssueStatusAsync(row.IssueId, "Resolved");
            await Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to update status. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
