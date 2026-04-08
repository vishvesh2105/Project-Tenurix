using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Tenurix.Management.Client.Api;
using Tenurix.Management.Client.Models;

namespace Tenurix.Management.Views.Windows
{
    // -----------------------------------------------------------------
    // Small view-model for each thumbnail in the gallery
    // -----------------------------------------------------------------
    public sealed class PhotoItem
    {
        public int Index { get; init; }
        public BitmapImage Image { get; init; } = null!;
        public string Url { get; init; } = "";
        public Brush BorderColor { get; set; } = Brushes.Transparent;
    }

    public partial class PropertySubmissionReviewWindow : Window
    {
        private readonly TenurixApiClient _api;
        private readonly int _propertyId;
        private PropertySubmissionDetailDto? _detail;

        // Photo galleries
        private List<PhotoItem> _propPhotos = new();
        private List<PhotoItem> _idPhotos   = new();
        private int _propIdx = 0;
        private int _idIdx   = 0;

        // Highlight colour for selected thumbnail
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

        private static readonly Brush HighlightBrush =
            new SolidColorBrush(Color.FromRgb(59, 130, 246)); // blue-500

        public PropertySubmissionReviewWindow(TenurixApiClient api, int propertyId)
        {
            InitializeComponent();
            _api = api;
            _propertyId = propertyId;
            Loaded += PropertySubmissionReviewWindow_Loaded;
        }

        // -------------------------------------------------------
        // LOAD
        // -------------------------------------------------------
        private async void PropertySubmissionReviewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _detail = await _api.GetPropertySubmissionDetailAsync(_propertyId);

                // --- Landlord section ---
                LandlordNameText.Text  = _detail.LandlordName  ?? "N/A";
                LandlordEmailText.Text = _detail.LandlordEmail ?? "—";
                LandlordPhoneText.Text = string.IsNullOrWhiteSpace(_detail.LandlordPhone)
                    ? "—" : _detail.LandlordPhone;

                if (!string.IsNullOrWhiteSpace(_detail.LandlordPhotoBase64))
                {
                    LandlordAvatar.Source = ImageFromBase64(_detail.LandlordPhotoBase64);
                    LandlordAvatarFallback.Visibility = Visibility.Collapsed;
                }
                else
                {
                    LandlordAvatar.Source = null;
                    LandlordAvatarFallback.Visibility = Visibility.Visible;
                }

                // --- Property overview ---
                PopulateOverview();

                // --- Property features ---
                PopulateFeatures();

                // --- Utilities ---
                PopulateUtilities();

                // --- Amenities ---
                PopulateAmenities();

                // --- Build Property Photo gallery ---
                var propUrls = _detail.AllPropertyPhotos ?? new List<string>();

                // Fallback: if JSON array is empty but legacy single URL exists, use it
                if (propUrls.Count == 0 && !string.IsNullOrWhiteSpace(_detail.PropertyImageUrl))
                    propUrls = new List<string> { _detail.PropertyImageUrl };

                _propPhotos = await BuildPhotoItems(propUrls);
                RenderPropGallery();

                // --- Build Owner ID gallery from LandlordDocuments (landlord-level) ---
                var idUrls = new List<string>();
                try
                {
                    if (_detail.LandlordUserId > 0)
                    {
                        var landlordDocs = await _api.GetLandlordDocumentsAsync(_detail.LandlordUserId, "ID_PROOF");
                        foreach (var doc in landlordDocs)
                        {
                            if (!string.IsNullOrWhiteSpace(doc.FileUrl))
                                idUrls.Add(doc.FileUrl);
                        }
                    }
                }
                catch { /* fallback below */ }

                // Fallback: try per-property ID photos if no landlord-level docs
                if (idUrls.Count == 0)
                {
                    var perPropIds = _detail.AllOwnerIdPhotos ?? new List<string>();
                    if (perPropIds.Count == 0 && !string.IsNullOrWhiteSpace(_detail.LandlordDocumentUrl))
                        perPropIds = new List<string> { _detail.LandlordDocumentUrl };
                    idUrls = perPropIds;
                }

                _idPhotos = await BuildPhotoItems(idUrls);
                RenderIdGallery();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load submission:\n" + ex.Message);
                Close();
            }
        }

        // -------------------------------------------------------
        // PROPERTY OVERVIEW
        // -------------------------------------------------------
        private void PopulateOverview()
        {
            if (_detail == null) return;

            // Type + SubType
            PropTypeText.Text = _detail.PropertyType ?? "—";
            PropSubTypeText.Text = !string.IsNullOrWhiteSpace(_detail.PropertySubType)
                ? $"({_detail.PropertySubType})" : "";

            // Address broken into lines
            PropAddressLine1Text.Text = _detail.Address ?? "—";

            // Try to show address line 2 if available (it's in the concatenated address,
            // but we also parse individual fields from the API)
            PropAddressLine2Text.Text = ""; // API currently returns concatenated address
            PropAddressLine2Text.Visibility = Visibility.Collapsed;

            PropCityProvinceText.Text = ""; // Already in concatenated address
            PropCityProvinceText.Visibility = Visibility.Collapsed;

            // Beds / Baths
            PropBedsText.Text = _detail.Bedrooms.ToString();
            PropBathsText.Text = _detail.Bathrooms.HasValue
                ? _detail.Bathrooms.Value.ToString("0.#") : "—";

            // Rent
            PropRentText.Text = $"{_detail.RentAmount:C}/mo";

            // Lease Term
            PropLeaseTermText.Text = !string.IsNullOrWhiteSpace(_detail.LeaseTerm)
                ? _detail.LeaseTerm : "—";

            // Available Date
            PropAvailDateText.Text = _detail.AvailableDate.HasValue
                ? _detail.AvailableDate.Value.ToString("MMM dd, yyyy") : "—";

            // Description
            PropDescText.Text = string.IsNullOrWhiteSpace(_detail.Description)
                ? "(no description)"
                : _detail.Description;
        }

        // -------------------------------------------------------
        // PROPERTY FEATURES (furnished, short term, year, etc.)
        // -------------------------------------------------------
        private void PopulateFeatures()
        {
            if (_detail == null) return;

            bool hasAny = _detail.IsFurnished.HasValue
                       || _detail.IsShortTerm.HasValue
                       || _detail.YearBuilt.HasValue
                       || _detail.NumberOfFloors.HasValue
                       || _detail.NumberOfUnits.HasValue
                       || _detail.ParkingSpots.HasValue
                       || !string.IsNullOrWhiteSpace(_detail.ParkingType);

            if (!hasAny)
            {
                FeaturesCard.Visibility = Visibility.Collapsed;
                return;
            }

            FeaturesCard.Visibility = Visibility.Visible;

            FurnishedText.Text = _detail.IsFurnished.HasValue
                ? (_detail.IsFurnished.Value ? "Yes" : "No") : "—";

            ShortTermText.Text = _detail.IsShortTerm.HasValue
                ? (_detail.IsShortTerm.Value ? "Yes" : "No") : "—";

            YearBuiltText.Text = _detail.YearBuilt.HasValue
                ? _detail.YearBuilt.Value.ToString() : "—";

            FloorsText.Text = _detail.NumberOfFloors.HasValue
                ? _detail.NumberOfFloors.Value.ToString() : "—";

            UnitsText.Text = _detail.NumberOfUnits.HasValue
                ? _detail.NumberOfUnits.Value.ToString() : "—";

            ParkingSpotsText.Text = _detail.ParkingSpots.HasValue
                ? _detail.ParkingSpots.Value.ToString() : "—";

            ParkingTypeText.Text = !string.IsNullOrWhiteSpace(_detail.ParkingType)
                ? _detail.ParkingType : "—";
        }

        // -------------------------------------------------------
        // UTILITIES
        // -------------------------------------------------------
        private void PopulateUtilities()
        {
            if (_detail == null) return;

            var items = ParseJsonArray(_detail.UtilitiesJson);
            if (items.Count == 0)
            {
                UtilitiesList.Visibility = Visibility.Collapsed;
                NoUtilitiesText.Visibility = Visibility.Visible;
                return;
            }

            UtilitiesList.Visibility = Visibility.Visible;
            NoUtilitiesText.Visibility = Visibility.Collapsed;
            UtilitiesList.ItemsSource = items;
        }

        // -------------------------------------------------------
        // AMENITIES
        // -------------------------------------------------------
        private void PopulateAmenities()
        {
            if (_detail == null) return;

            var items = ParseJsonArray(_detail.AmenitiesJson);
            if (items.Count == 0)
            {
                AmenitiesList.Visibility = Visibility.Collapsed;
                NoAmenitiesText.Visibility = Visibility.Visible;
                return;
            }

            AmenitiesList.Visibility = Visibility.Visible;
            NoAmenitiesText.Visibility = Visibility.Collapsed;
            AmenitiesList.ItemsSource = items;
        }

        // -------------------------------------------------------
        // Parse JSON array string -> List<string>
        // -------------------------------------------------------
        private static List<string> ParseJsonArray(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();
            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        // -------------------------------------------------------
        // Build PhotoItem list: download each URL into BitmapImage
        // -------------------------------------------------------
        private static async System.Threading.Tasks.Task<List<PhotoItem>> BuildPhotoItems(List<string> urls)
        {
            var items = new List<PhotoItem>();
            int index = 0;

            foreach (var url in urls)
            {
                if (string.IsNullOrWhiteSpace(url)) continue;

                BitmapImage? bmp = null;

                // Prefer HTTP download (gives reliable bytes regardless of WPF URI parsing)
                if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var bytes = await _http.GetByteArrayAsync(url);
                        bmp = BitmapFromBytes(bytes);
                    }
                    catch { /* fall through */ }
                }

                // Fallback: WPF native URI loading
                if (bmp == null)
                {
                    try
                    {
                        bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource   = new Uri(url, UriKind.Absolute);
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();
                    }
                    catch { bmp = null; }
                }

                if (bmp != null)
                {
                    items.Add(new PhotoItem
                    {
                        Index       = index,
                        Image       = bmp,
                        Url         = url,
                        BorderColor = Brushes.Transparent
                    });
                    index++;
                }
            }

            return items;
        }

        private static BitmapImage BitmapFromBytes(byte[] bytes)
        {
            var ms = new MemoryStream(bytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption  = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            ms.Dispose();
            return bmp;
        }

        // -------------------------------------------------------
        // PROPERTY GALLERY
        // -------------------------------------------------------
        private void RenderPropGallery()
        {
            if (_propPhotos.Count == 0)
            {
                NoPropertyPhotosText.Visibility = Visibility.Visible;
                PropMainBorder.Visibility       = Visibility.Collapsed;
                PropertyPhotosList.ItemsSource  = null;
                PropPhotoCountText.Text         = "";
                return;
            }

            NoPropertyPhotosText.Visibility = Visibility.Collapsed;
            PropMainBorder.Visibility       = Visibility.Visible;
            PropPhotoCountText.Text         = $"({_propPhotos.Count} photos)";

            // Highlight selected thumbnail
            foreach (var p in _propPhotos)
                p.BorderColor = p.Index == _propIdx ? HighlightBrush : Brushes.Transparent;

            PropertyPhotosList.ItemsSource = null;
            PropertyPhotosList.ItemsSource = _propPhotos;

            PropMainImage.Source = _propPhotos[_propIdx].Image;
            PropIndexText.Text   = $"{_propIdx + 1} / {_propPhotos.Count}";

            PropPrevBtn.IsEnabled = _propIdx > 0;
            PropNextBtn.IsEnabled = _propIdx < _propPhotos.Count - 1;
        }

        private void PropThumb_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int idx)
            {
                _propIdx = idx;
                RenderPropGallery();
            }
        }

        private void PropPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_propIdx > 0) { _propIdx--; RenderPropGallery(); }
        }

        private void PropNext_Click(object sender, RoutedEventArgs e)
        {
            if (_propIdx < _propPhotos.Count - 1) { _propIdx++; RenderPropGallery(); }
        }

        private void PropFullSize_Click(object sender, RoutedEventArgs e)
        {
            if (_propPhotos.Count == 0) return;
            OpenViewer("Property Photo", _propPhotos[_propIdx].Url);
        }

        // -------------------------------------------------------
        // OWNER ID GALLERY
        // -------------------------------------------------------
        private void RenderIdGallery()
        {
            if (_idPhotos.Count == 0)
            {
                NoIdPhotosText.Visibility      = Visibility.Visible;
                IdMainBorder.Visibility        = Visibility.Collapsed;
                OwnerIdPhotosList.ItemsSource  = null;
                IdPhotoCountText.Text          = "";
                return;
            }

            NoIdPhotosText.Visibility = Visibility.Collapsed;
            IdMainBorder.Visibility   = Visibility.Visible;
            IdPhotoCountText.Text     = $"({_idPhotos.Count} documents)";

            foreach (var p in _idPhotos)
                p.BorderColor = p.Index == _idIdx ? HighlightBrush : Brushes.Transparent;

            OwnerIdPhotosList.ItemsSource = null;
            OwnerIdPhotosList.ItemsSource = _idPhotos;

            IdMainImage.Source = _idPhotos[_idIdx].Image;
            IdIndexText.Text   = $"{_idIdx + 1} / {_idPhotos.Count}";

            IdPrevBtn.IsEnabled = _idIdx > 0;
            IdNextBtn.IsEnabled = _idIdx < _idPhotos.Count - 1;
        }

        private void IdThumb_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int idx)
            {
                _idIdx = idx;
                RenderIdGallery();
            }
        }

        private void IdPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_idIdx > 0) { _idIdx--; RenderIdGallery(); }
        }

        private void IdNext_Click(object sender, RoutedEventArgs e)
        {
            if (_idIdx < _idPhotos.Count - 1) { _idIdx++; RenderIdGallery(); }
        }

        private void IdFullSize_Click(object sender, RoutedEventArgs e)
        {
            if (_idPhotos.Count == 0) return;
            OpenViewer("Landlord Document", _idPhotos[_idIdx].Url);
        }

        // -------------------------------------------------------
        // Helpers
        // -------------------------------------------------------
        private void OpenViewer(string title, string url)
        {
            if (url.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                return;
            }

            var win = new ImageViewerWindow(title, url) { Owner = this };
            win.ShowDialog();
        }

        private static BitmapImage ImageFromBase64(string base64)
        {
            var bytes = Convert.FromBase64String(base64);
            return BitmapFromBytes(bytes);
        }

        // -------------------------------------------------------
        // Action buttons
        // -------------------------------------------------------
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private async void Reject_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ReasonBox.Text))
            {
                MessageBox.Show("Reject reason is required.");
                return;
            }

            var confirmResult = MessageBox.Show("Are you sure you want to reject this submission?", "Confirm Rejection", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmResult != MessageBoxResult.Yes) return;

            try
            {
                await _api.RejectPropertySubmissionAsync(_propertyId, ReasonBox.Text.Trim());
                MessageBox.Show("Rejected.");
                if (Application.Current.MainWindow is ShellWindow shell)
                    shell.ShowToast("Submission rejected successfully");
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Reject failed:\n" + ex.Message);
                if (Application.Current.MainWindow is ShellWindow shell2)
                    shell2.ShowToast("Failed to reject submission", true);
            }
        }

        private async void Hold_Click(object sender, RoutedEventArgs e)
        {
            var confirmResult = MessageBox.Show("Are you sure you want to put this submission on hold?", "Confirm Hold", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmResult != MessageBoxResult.Yes) return;

            try
            {
                await _api.HoldPropertySubmissionAsync(_propertyId, ReasonBox.Text?.Trim());
                MessageBox.Show("Property put on hold.");
                if (Application.Current.MainWindow is ShellWindow shell)
                    shell.ShowToast("Submission put on hold");
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hold failed:\n" + ex.Message);
                if (Application.Current.MainWindow is ShellWindow shell2)
                    shell2.ShowToast("Failed to put submission on hold", true);
            }
        }

        private async void Approve_Click(object sender, RoutedEventArgs e)
        {
            var confirmResult = MessageBox.Show("Are you sure you want to approve this submission?", "Confirm Approval", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmResult != MessageBoxResult.Yes) return;

            try
            {
                await _api.ApprovePropertySubmissionAsync(_propertyId);
                MessageBox.Show("Approved. It will be visible in Public Listings.");
                if (Application.Current.MainWindow is ShellWindow shell)
                    shell.ShowToast("Submission approved successfully");
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Approve failed:\n" + ex.Message);
                if (Application.Current.MainWindow is ShellWindow shell2)
                    shell2.ShowToast("Failed to approve submission", true);
            }
        }
    }
}
