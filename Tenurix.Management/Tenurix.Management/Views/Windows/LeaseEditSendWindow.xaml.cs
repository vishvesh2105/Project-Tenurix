using System;
using System.Windows;
using Tenurix.Management.Client.Api;
using Tenurix.Management.Client.Models;

namespace Tenurix.Management.Views.Windows;

public partial class LeaseEditSendWindow : Window
{
    private readonly TenurixApiClient _api;
    private readonly int _leaseId;
    private readonly LeaseApplicationDto _app;

    public LeaseEditSendWindow(TenurixApiClient api, LeaseApplicationDto app)
    {
        _api = api;
        _app = app;
        _leaseId = app.LeaseId!.Value;

        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TenantText.Text      = _app.ApplicantName;
        TenantEmailText.Text = _app.ApplicantEmail;
        PropertyText.Text    = _app.PropertyAddress;

        // Pre-fill current values from the application
        StartDatePicker.SelectedDate = _app.LeaseStartDate ?? DateTime.Today;
        EndDatePicker.SelectedDate   = _app.LeaseEndDate   ?? DateTime.Today.AddYears(1);

        // RentBox will remain blank — management must confirm the value
        RentBox.Text = "";
        RentBox.Focus();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!Validate(out var rent, out var start, out var end)) return;

        SetBusy(true);
        try
        {
            await _api.UpdateLeaseTermsAsync(_leaseId, rent, start, end);
            ShowSuccess("Lease terms saved and PDF regenerated successfully.");
        }
        catch (Exception ex)
        {
            ShowError($"Save failed: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        // If a rent value is filled in, save first then send
        bool hasPendingEdit = !string.IsNullOrWhiteSpace(RentBox.Text)
                              || StartDatePicker.SelectedDate.HasValue
                              || EndDatePicker.SelectedDate.HasValue;

        if (hasPendingEdit && !string.IsNullOrWhiteSpace(RentBox.Text))
        {
            if (!Validate(out var rent, out var start, out var end)) return;

            SetBusy(true);
            try
            {
                await _api.UpdateLeaseTermsAsync(_leaseId, rent, start, end);
            }
            catch (Exception ex)
            {
                ShowError($"Save failed before send: {ex.Message}");
                SetBusy(false);
                return;
            }
        }
        else
        {
            SetBusy(true);
        }

        try
        {
            await _api.SendLeaseToTenantAsync(_leaseId);
            MessageBox.Show(
                $"Lease agreement has been sent to {_app.ApplicantName} ({_app.ApplicantEmail}).\n\nThey will receive an email with a link to review and sign.",
                "Agreement Sent",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Send failed: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private bool Validate(out decimal rent, out DateTime start, out DateTime end)
    {
        rent  = 0;
        start = DateTime.MinValue;
        end   = DateTime.MinValue;

        if (!decimal.TryParse(RentBox.Text.Trim(), out rent) || rent <= 0)
        {
            ShowError("Please enter a valid rent amount greater than zero.");
            RentBox.Focus();
            return false;
        }

        if (!StartDatePicker.SelectedDate.HasValue)
        {
            ShowError("Please select a lease start date.");
            return false;
        }

        if (!EndDatePicker.SelectedDate.HasValue)
        {
            ShowError("Please select a lease end date.");
            return false;
        }

        start = StartDatePicker.SelectedDate.Value;
        end   = EndDatePicker.SelectedDate.Value;

        if (end <= start)
        {
            ShowError("Lease end date must be after the start date.");
            return false;
        }

        StatusMsg.Visibility = Visibility.Collapsed;
        return true;
    }

    private void SetBusy(bool busy)
    {
        SaveBtn.IsEnabled = !busy;
        SendBtn.IsEnabled = !busy;
    }

    private void ShowError(string message)
    {
        StatusMsg.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC2626"));
        StatusMsg.Text       = message;
        StatusMsg.Visibility = Visibility.Visible;
    }

    private void ShowSuccess(string message)
    {
        StatusMsg.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#15803D"));
        StatusMsg.Text       = message;
        StatusMsg.Visibility = Visibility.Visible;
    }
}
