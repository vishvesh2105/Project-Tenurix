using System;
using System.Windows;
using Tenurix.Management.Client.Api;
using Tenurix.Management.Client.Models;

namespace Tenurix.Management.Views.Windows
{
    public partial class LeaseApplicationReviewWindow : Window
    {
        private readonly TenurixApiClient _api;
        private readonly int _applicationId;
        private LeaseApplicationDetailDto? _detail;

        public LeaseApplicationReviewWindow(TenurixApiClient api, int applicationId)
        {
            InitializeComponent();
            _api = api;
            _applicationId = applicationId;
            Loaded += LeaseApplicationReviewWindow_Loaded;
        }

        private async void LeaseApplicationReviewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _detail = await _api.GetLeaseApplicationDetailAsync(_applicationId);

                // Header
                ApplicantText.Text      = _detail.ApplicantName ?? "N/A";
                ApplicantEmailText.Text = _detail.ApplicantEmail ?? "N/A";
                StatusText.Text         = _detail.Status ?? "Pending";

                // Personal info
                PhoneText.Text          = _detail.Phone ?? "N/A";
                DobText.Text            = _detail.DateOfBirth.HasValue
                                            ? _detail.DateOfBirth.Value.ToString("MMM dd, yyyy")
                                            : "N/A";
                CurrentAddressText.Text = _detail.CurrentAddress ?? "N/A";

                // Employment
                EmploymentStatusText.Text = _detail.EmploymentStatus ?? "N/A";
                IncomeText.Text           = _detail.AnnualIncome.HasValue
                                            ? $"${_detail.AnnualIncome.Value:N0}/yr"
                                            : "N/A";
                EmployerText.Text         = _detail.EmployerName ?? "N/A";
                JobTitleText.Text         = _detail.JobTitle ?? "N/A";

                // Household
                OccupantsText.Text  = _detail.NumberOfOccupants?.ToString() ?? "1";
                PetsText.Text       = (_detail.HasPets == true) ? "Yes" : "No";
                PetDetailsText.Text = _detail.PetDetails ?? "—";

                // Emergency contact
                EmergencyNameText.Text     = _detail.EmergencyContactName ?? "N/A";
                EmergencyPhoneText.Text    = _detail.EmergencyContactPhone ?? "N/A";
                EmergencyRelationText.Text = _detail.EmergencyContactRelation ?? "—";

                // Reference
                RefNameText.Text     = _detail.ReferenceName ?? "—";
                RefPhoneText.Text    = _detail.ReferencePhone ?? "—";
                RefRelationText.Text = _detail.ReferenceRelation ?? "—";

                // Lease details
                ListingText.Text   = _detail.ListingTitle ?? "N/A";
                StartDateText.Text = _detail.LeaseStartDate.HasValue
                                        ? _detail.LeaseStartDate.Value.ToString("MMM dd, yyyy")
                                        : "N/A";
                EndDateText.Text   = _detail.LeaseEndDate.HasValue
                                        ? _detail.LeaseEndDate.Value.ToString("MMM dd, yyyy")
                                        : "N/A";

                NotesText.Text = string.IsNullOrWhiteSpace(_detail.AdditionalNotes)
                    ? (string.IsNullOrWhiteSpace(_detail.Note)
                        ? "No notes on this application."
                        : _detail.Note)
                    : _detail.AdditionalNotes;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load lease application:\n" + ex.Message);
                Close();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private async void Reject_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ReasonBox.Text))
            {
                MessageBox.Show("Reject reason is required.");
                return;
            }

            var result = MessageBox.Show("Are you sure you want to reject this application?", "Confirm Rejection", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _api.RejectLeaseApplicationAsync(_applicationId, ReasonBox.Text.Trim());
                MessageBox.Show("Rejected.");
                if (Application.Current.MainWindow is ShellWindow shell)
                    shell.ShowToast("Application rejected successfully");
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Reject failed:\n" + ex.Message);
                if (Application.Current.MainWindow is ShellWindow shell2)
                    shell2.ShowToast("Failed to reject application", true);
            }
        }

        private async void Approve_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to approve this application?", "Confirm Approval", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _api.ApproveLeaseApplicationAsync(_applicationId);
                MessageBox.Show(
                    "Application approved! A lease record has been created.\n\nThe filter will switch to 'Approved' so you can use the 'Edit & Send Agreement' button to set lease terms and send the agreement to the tenant.",
                    "Approved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                if (Application.Current.MainWindow is ShellWindow shell)
                    shell.ShowToast("Application approved successfully");
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Approve failed:\n" + ex.Message);
                if (Application.Current.MainWindow is ShellWindow shell2)
                    shell2.ShowToast("Failed to approve application", true);
            }
        }
    }
}
