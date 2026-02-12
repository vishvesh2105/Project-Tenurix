using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Tenurix.Management.Client.Api;
using Tenurix.Management.Client.Models.Landlords;

namespace Tenurix.Management.Views.Pages;

public partial class LandlordsPage : Page
{
    private readonly TenurixApiClient _api;

    public LandlordsPage(TenurixApiClient api)
    {
        InitializeComponent();
        _api = api;
        LandlordsGrid.ItemsSource = new List<LandlordSearchDto>();

        Loaded += async (_, __) =>
        {
            try
            {
                ErrorText.Text = "";
                LandlordsGrid.ItemsSource = await _api.SearchLandlordsAsync("");
            }
            catch (Exception ex)
            {
                ErrorText.Text = ex.Message;
            }
        };
    }

    private async void Search_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        ErrorText.Text = "";
        try
        {
            var q = QueryBox.Text.Trim();
            var landlords = await _api.SearchLandlordsAsync(q);
            LandlordsGrid.ItemsSource = landlords;
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

        try
        {
            ListingsGrid.ItemsSource =
                await _api.GetLandlordListingsAsync(landlord.UserId);

            LeasesGrid.ItemsSource =
                await _api.GetLandlordLeasesAsync(landlord.UserId);

            IssuesGrid.ItemsSource =
                await _api.GetLandlordIssuesAsync(landlord.UserId);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to load landlord portfolio:\n" + ex.Message);
        }
    }





}
