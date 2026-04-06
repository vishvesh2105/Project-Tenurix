using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Tenurix.Management.Client.Api;
using Tenurix.Management.Client.Models.Landlords;
using Tenurix.Management.Views.Windows;

namespace Tenurix.Management.Views.Pages;

public partial class LandlordsPage : Page
{
    private readonly TenurixApiClient _api;
    private int? _selectedLandlordId;

    public LandlordsPage(TenurixApiClient api)
    {
        InitializeComponent();
        _api = api;
        LandlordsGrid.ItemsSource = new List<LandlordSearchDto>();

        // Placeholder logic
        QueryBox.TextChanged += (_, __) =>
        {
            QueryPlaceholder.Visibility = string.IsNullOrEmpty(QueryBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        };

        Loaded += async (_, __) =>
        {
            try
            {
                ErrorText.Text = "";
                var results = await _api.SearchLandlordsAsync("");
                LandlordsGrid.ItemsSource = results;
                ResultsHeader.Text = $"All Landlords ({results.Count})";
            }
            catch (Exception ex)
            {
                ErrorText.Text = ex.Message;
            }
        };
    }

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        try
        {
            var q = QueryBox.Text.Trim();
            var landlords = await _api.SearchLandlordsAsync(q);
            LandlordsGrid.ItemsSource = landlords;
            ResultsHeader.Text = string.IsNullOrWhiteSpace(q)
                ? $"All Landlords ({landlords.Count})"
                : $"Results ({landlords.Count})";
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
        }
    }

    private async void LandlordsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LandlordsGrid.SelectedItem is not LandlordSearchDto landlord)
            return;

        _selectedLandlordId = landlord.UserId;

        // Update landlord info header
        LandlordNameText.Text  = landlord.FullName ?? "—";
        LandlordEmailText.Text = landlord.Email ?? "—";
        LandlordInfoPanel.Visibility     = Visibility.Visible;
        NoSelectionPlaceholder.Visibility = Visibility.Collapsed;
        PortfolioScroll.Visibility        = Visibility.Visible;

        // Clear previous data
        PropsItems.ItemsSource  = null;
        LeasesItems.ItemsSource = null;

        // Properties
        try
        {
            var props = await _api.GetLandlordPropertiesAsync(landlord.UserId);
            PropsItems.ItemsSource = props;
            PropsCountBadge.Text = $"({props.Count})";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load landlord properties: {ex.Message}");
            PropsCountBadge.Text = "(error)";
        }

        // Leases
        try
        {
            var leases = await _api.GetLandlordLeasesAsync(landlord.UserId);
            LeasesItems.ItemsSource = leases;
            LeasesCountBadge.Text = $"({leases.Count})";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load landlord leases: {ex.Message}");
            LeasesCountBadge.Text = "(error)";
        }
    }

    // ─── Open full property details ───
    private void ViewPropertyDetails_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not LandlordPropertyDto p) return;

        var win = new PropertySubmissionReviewWindow(_api, p.PropertyId)
        {
            Owner = Window.GetWindow(this)
        };
        win.ShowDialog();
    }

    // ─── Request New ID from Landlord ───
    private async void RequestNewId_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedLandlordId == null) return;

        var result = MessageBox.Show(
            "This will send a request to the landlord asking them to upload a new ID document.\n\nDo you want to proceed?",
            "Request New ID",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            RequestIdBtn.IsEnabled = false;
            await _api.RequestLandlordDocumentAsync(_selectedLandlordId.Value, "ID_PROOF", "Management has requested you to upload a new ID document for verification.");
            MessageBox.Show(
                "ID request sent successfully!\n\nThe landlord will see a notification on their properties page to upload a new ID.",
                "Request Sent",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Request Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            RequestIdBtn.IsEnabled = true;
        }
    }
}
