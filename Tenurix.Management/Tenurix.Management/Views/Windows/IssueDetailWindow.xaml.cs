using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using Tenurix.Management.Client.Api;
using Tenurix.Management.Client.Models;

namespace Tenurix.Management.Views.Windows
{
    public partial class IssueDetailWindow : Window
    {
        private readonly TenurixApiClient _api;
        private readonly int _issueId;
        private IssueDetailDto? _detail;

        public IssueDetailWindow(TenurixApiClient api, int issueId)
        {
            InitializeComponent();
            _api = api;
            _issueId = issueId;
            Loaded += IssueDetailWindow_Loaded;
        }

        private async void IssueDetailWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _detail = await _api.GetIssueDetailAsync(_issueId);

                // Status badge
                StatusText.Text = _detail.Status ?? "Unknown";
                StatusBadge.Background = (_detail.Status ?? "") switch
                {
                    "Submitted"  => new SolidColorBrush(Color.FromRgb(30, 64, 175)),   // blue
                    "InProgress" => new SolidColorBrush(Color.FromRgb(161, 98, 7)),    // amber
                    "Resolved"   => new SolidColorBrush(Color.FromRgb(22, 101, 52)),   // green
                    _            => new SolidColorBrush(Color.FromRgb(75, 85, 99))     // grey
                };

                // Who filed it
                FiledByText.Text      = _detail.FiledByName ?? "N/A";
                FiledByEmailText.Text = _detail.FiledByEmail ?? "";
                DateText.Text = _detail.CreatedAt.HasValue
                    ? _detail.CreatedAt.Value.ToString("MMM dd, yyyy  h:mm tt")
                    : "N/A";

                // Property & landlord
                PropertyText.Text     = _detail.PropertyAddress ?? "N/A";
                LandlordNameText.Text = _detail.LandlordName ?? "N/A";
                LandlordText.Text     = _detail.LandlordEmail ?? "";

                // Issue type & description
                TitleText.Text = _detail.IssueType ?? _detail.Title ?? "N/A";
                DescText.Text  = _detail.Description ?? "No description provided.";

                // Internal note (show only if present)
                if (!string.IsNullOrWhiteSpace(_detail.InternalNote))
                {
                    NoteText.Text        = _detail.InternalNote;
                    NoteBorder.Visibility = Visibility.Visible;
                }

                // Hide image button if no image
                if (string.IsNullOrWhiteSpace(_detail.ImageUrl))
                    ViewImageButton.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load issue:\n" + ex.Message);
                Close();
            }
        }

        private void ViewImage_Click(object sender, RoutedEventArgs e)
        {
            if (_detail == null) return;

            if (string.IsNullOrWhiteSpace(_detail.ImageUrl))
            {
                MessageBox.Show("No image uploaded for this issue.");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _detail.ImageUrl,
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show("Unable to open image.");
            }
        }

        private async void InProgress_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to mark this issue as In Progress?", "Confirm Status Change", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _api.UpdateIssueStatusAsync(_issueId, "InProgress");
                MessageBox.Show("Status updated to In Progress.");
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Update failed:\n" + ex.Message);
            }
        }

        private async void Resolved_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to mark this issue as Resolved?", "Confirm Status Change", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _api.UpdateIssueStatusAsync(_issueId, "Resolved");
                MessageBox.Show("Status updated to Resolved.");
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Update failed:\n" + ex.Message);
            }
        }
    }
}
