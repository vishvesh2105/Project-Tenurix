using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Tenurix.Management.Views.Windows
{
    public partial class ImageViewerWindow : Window
    {
        private readonly string _url;
        private double _zoom = 1.0;

        public ImageViewerWindow(string title, string url)
        {
            InitializeComponent();
            TitleText.Text = title;
            _url = url;

            Loaded += async (_, __) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                StatusText.Text = "Loading...";
                MainImage.Visibility = Visibility.Collapsed;

                using var http = new HttpClient();
                var bytes = await http.GetByteArrayAsync(_url);

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = new MemoryStream(bytes);
                bmp.EndInit();
                bmp.Freeze();

                MainImage.Source = bmp;
                MainImage.Visibility = Visibility.Visible;
                StatusText.Text = "";

                // Wait one UI render cycle so ScrollViewer gets real ViewportWidth/Height
                await Dispatcher.InvokeAsync(() => { },
                    System.Windows.Threading.DispatcherPriority.Render);

                // Now viewport is valid, Fit() calculates correct scale
                UpdateLayout();
                Fit();
            }

            catch (Exception ex)
            {
                StatusText.Text = "Failed to load image.";
                MessageBox.Show("Failed to load image:\n" + ex.Message);
            }
        }

        private void Fit()
        {
            if (MainImage.Source is not BitmapSource bmp) return;

            // ScrollViewer visible viewport
            var vw = Scroll.ViewportWidth;
            var vh = Scroll.ViewportHeight;

            // If not measured yet, try again after layout
            if (vw <= 0 || vh <= 0) return;

            // Image pixel size
            var iw = bmp.PixelWidth;
            var ih = bmp.PixelHeight;

            if (iw <= 0 || ih <= 0) return;

            // Fit scale
            var sx = vw / iw;
            var sy = vh / ih;
            _zoom = Math.Min(sx, sy);

            // Cap zoom (optional)
            _zoom = Math.Min(2.0, Math.Max(0.05, _zoom));

            Scale.ScaleX = _zoom;
            Scale.ScaleY = _zoom;

            Scroll.ScrollToHorizontalOffset(0);
            Scroll.ScrollToVerticalOffset(0);
        }

        private void Fit_Click(object sender, RoutedEventArgs e) => Fit();

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            _zoom = Math.Min(6.0, _zoom + 0.25);
            Scale.ScaleX = _zoom;
            Scale.ScaleY = _zoom;
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _zoom = Math.Max(0.25, _zoom - 0.25);
            Scale.ScaleX = _zoom;
            Scale.ScaleY = _zoom;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}