using Microsoft.Win32;
using System;
using System.IO;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using Tenurix.Management.Client.Api;
using Tenurix.Management.Client.Models;

namespace Tenurix.Management.Views.Windows
{
    public partial class MyProfileWindow : Window
    {
        private readonly TenurixApiClient _api;
        private string? _selectedPhotoPath;

        public MyProfileWindow(TenurixApiClient api)
        {
            InitializeComponent();
            _api = api;

            Loaded += MyProfileWindow_Loaded;
        }

        private async void MyProfileWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var me = await _api.GetMyProfileAsync();

                FullNameBox.Text = me.FullName ?? "";
                PhoneBox.Text = me.Phone ?? "";
                JobTitleBox.Text = me.JobTitle ?? "";
                DepartmentBox.Text = me.Department ?? "";
                EmailText.Text = me.Email ?? "";

                // Load existing photo (base64)
                if (!string.IsNullOrWhiteSpace(me.PhotoBase64))
                {
                    SetImageFromBase64(me.PhotoBase64);
                }
                else
                {
                    PhotoImage.Source = null;
                    NoPhotoText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load profile:\n" + ex.Message);
                DialogResult = false;
                Close();
            }
        }

        private void ChoosePhoto_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
            };

            if (dlg.ShowDialog() == true)
            {
                _selectedPhotoPath = dlg.FileName;

                // Preview local image
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(_selectedPhotoPath);
                bmp.EndInit();

                PhotoImage.Source = bmp;
                NoPhotoText.Visibility = Visibility.Collapsed;
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FullNameBox.Text))
            {
                MessageBox.Show("Full Name is required.");
                return;
            }

            try
            {
                await _api.UpdateMyProfileAsync(
                    FullNameBox.Text.Trim(),
                    string.IsNullOrWhiteSpace(PhoneBox.Text) ? null : PhoneBox.Text.Trim(),
                    string.IsNullOrWhiteSpace(JobTitleBox.Text) ? null : JobTitleBox.Text.Trim(),
                    string.IsNullOrWhiteSpace(DepartmentBox.Text) ? null : DepartmentBox.Text.Trim()
                );

                if (!string.IsNullOrWhiteSpace(_selectedPhotoPath))
                {
                    await _api.UploadMyPhotoAsync(_selectedPhotoPath);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save profile:\n" + ex.Message);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SetImageFromBase64(string base64)
        {
            try
            {
                var bytes = Convert.FromBase64String(base64);
                using var ms = new MemoryStream(bytes);

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();

                PhotoImage.Source = bmp;
                NoPhotoText.Visibility = Visibility.Collapsed;
            }
            catch
            {
                PhotoImage.Source = null;
                NoPhotoText.Visibility = Visibility.Visible;
            }
        }
    }
}
