using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Tenurix.Management.Client.Api;
using Tenurix.Management.Client.Models;
using Tenurix.Management.Views.Windows;

namespace Tenurix.Management.Views.Pages;

public partial class ListingsPage : Page
{
    private readonly TenurixApiClient _api;
    private List<ListingDto> _all = new();

    public ListingsPage(TenurixApiClient api)
    {
        InitializeComponent();
        _api = api;

        Loaded += async (_, __) => await LoadAllAsync();
    }

    private string SelectedStatus()
    {
        var item = StatusFilter.SelectedItem as ComboBoxItem;
        return item?.Content?.ToString() ?? "Active";
    }

    private void UpdateCount(IEnumerable<ListingDto> items)
    {
        var list = items?.ToList() ?? new List<ListingDto>();
        CountText.Text = $"Showing {list.Count} of {_all.Count} listings";
    }

    private List<ListingDto> ApplyFilters()
    {
        var status = SelectedStatus();
        var q = (QueryBox.Text ?? "").Trim();

        IEnumerable<ListingDto> filtered = _all;

        // Status filter
        if (!status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(x =>
                (x.ListingStatus ?? "").Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        // Text search
        if (!string.IsNullOrWhiteSpace(q))
        {
            filtered = filtered.Where(x =>
                (x.Address ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.ListingId.ToString() == q ||
                x.PropertyId.ToString() == q
            );
        }

        return filtered.ToList();
    }

    private async System.Threading.Tasks.Task LoadAllAsync()
    {
        ErrorText.Text = "";
        try
        {
            _all = await _api.GetAllListingsAsync();
            var filtered = ApplyFilters();
            ListingsGrid.ItemsSource = filtered;
            UpdateCount(filtered);
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ListingsGrid.ItemsSource = null;
            CountText.Text = "Showing 0 of 0 listings";
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadAllAsync();
    }

    private void View_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ListingDto row) return;
        var win = new PropertySubmissionReviewWindow(_api, row.PropertyId)
        {
            Owner = Window.GetWindow(this)
        };
        win.ShowDialog();
    }

    private void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_all == null || _all.Count == 0 && ListingsGrid == null) return;
        try
        {
            var filtered = ApplyFilters();
            ListingsGrid.ItemsSource = filtered;
            UpdateCount(filtered);
        }
        catch { }
    }

    private void Search_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        try
        {
            var filtered = ApplyFilters();
            ListingsGrid.ItemsSource = filtered;
            UpdateCount(filtered);
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
        }
    }

    private async void ToggleStatus_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ListingDto row) return;

        var action = row.ListingStatus?.Equals("Active", StringComparison.OrdinalIgnoreCase) == true
            ? "deactivate" : "activate";

        var result = MessageBox.Show(
            $"Are you sure you want to {action} this listing?\n\n{row.Address}",
            "Confirm Status Change",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            await _api.ToggleListingStatusAsync(row.ListingId);
            await LoadAllAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to {action} listing:\n" + ex.Message);
        }
    }
}
