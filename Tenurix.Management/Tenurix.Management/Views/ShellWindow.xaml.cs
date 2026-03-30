using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Tenurix.Management.Client.Api;
using Tenurix.Management.Models.Auth;
using Tenurix.Management.Views.Pages;
using System.IO;
using System.Windows.Media.Imaging;

namespace Tenurix.Management.Views
{
    public partial class ShellWindow : Window
    {
        private readonly TenurixApiClient _api;
        private readonly LoginResponse _session;
        private DispatcherTimer? _bellTimer;

        public ShellWindow(TenurixApiClient api, LoginResponse session)
        {
            InitializeComponent();

            _api = api;
            _session = session;

            // Header text
            UserText.Text = $"Welcome, {_session.FullName}";
            RoleText.Text = $"Role: {_session.RoleName}";

            // Initials badge (top-right)
            UserInitialsText.Text = GetInitials(_session.FullName);

            ApplyPermissions();

            Loaded += ShellWindow_Loaded;

            // Default landing page
            NavDashboard.IsChecked = true;
            Navigate(new DashboardPage(_api));
        }

        private async void ShellWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var me = await _api.GetMyProfileAsync();

                if (!string.IsNullOrWhiteSpace(me.PhotoBase64))
                {
                    HeaderAvatar.Source = Base64ToImage(me.PhotoBase64);
                    HeaderAvatarFallback.Visibility = Visibility.Collapsed;
                }
                else
                {
                    HeaderAvatar.Source = null;
                    HeaderAvatarFallback.Visibility = Visibility.Visible;
                }

                // If name changed server-side, keep initials consistent
                if (!string.IsNullOrWhiteSpace(me.FullName))
                {
                    _session.FullName = me.FullName;
                    UserText.Text = $"Welcome, {_session.FullName}";
                    UserInitialsText.Text = GetInitials(_session.FullName);
                }
            }
            catch
            {
                HeaderAvatar.Source = null;
                HeaderAvatarFallback.Visibility = Visibility.Visible;
            }

            // Initial bell count + start polling every 60 seconds
            await RefreshBellAsync();

            _bellTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            _bellTimer.Tick += async (_, __) => await RefreshBellAsync();
            _bellTimer.Start();
        }

        private async System.Threading.Tasks.Task RefreshBellAsync()
        {
            try
            {
                int count = await _api.GetNotificationUnreadCountAsync();
                UpdateBellBadge(count);
            }
            catch
            {
                // Non-critical — swallow silently
            }
        }

        private void UpdateBellBadge(int count)
        {
            if (count <= 0)
            {
                BellBadge.Visibility = Visibility.Collapsed;
            }
            else
            {
                BellBadgeText.Text = count > 99 ? "99+" : count.ToString();
                BellBadge.Visibility = Visibility.Visible;
            }
        }

        private static string GetInitials(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return "U";

            var parts = fullName
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Take(2)
                .Select(p => char.ToUpperInvariant(p[0]))
                .ToArray();

            return parts.Length == 0 ? "U" : new string(parts);
        }

        private static BitmapImage Base64ToImage(string base64)
        {
            byte[] bytes = Convert.FromBase64String(base64);

            using var ms = new MemoryStream(bytes);
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.StreamSource = ms;
            img.EndInit();
            img.Freeze();
            return img;
        }

        private void Navigate(object page)
        {
            MainFrame.Navigate(page);
        }

        private bool Has(string permissionKey)
        {
            return _session.Permissions != null &&
                   _session.Permissions.Any(p =>
                       string.Equals(p, permissionKey, StringComparison.OrdinalIgnoreCase));
        }

        private void ApplyPermissions()
        {
            NavDashboard.IsEnabled = true;

            NavProps.IsEnabled = Has("REVIEW_PROPERTY") || Has("APPROVE_PROPERTY");
            NavApps.IsEnabled = Has("REVIEW_LEASE_APP") || Has("APPROVE_LEASE_APP");
            NavIssues.IsEnabled = Has("MANAGE_ISSUES");
            NavLandlords.IsEnabled = Has("VIEW_LANDLORD_PORTFOLIO");
            NavListings.IsEnabled = Has("VIEW_LISTINGS") || Has("MANAGE_LISTINGS") || true; // keep visible; adjust key if you have one

            // Employees tab: only visible for users with MANAGE_USERS (Manager role)
            // TeamLead and Staff won't see this tab at all
            if (Has("MANAGE_USERS"))
            {
                NavEmployees.Visibility = Visibility.Visible;
                NavEmployees.IsEnabled = true;
            }
            else
            {
                NavEmployees.Visibility = Visibility.Collapsed;
            }
        }

        private void Dashboard_Click(object sender, RoutedEventArgs e)
        {
            Navigate(new DashboardPage(_api));
        }

        private void Props_Click(object sender, RoutedEventArgs e)
        {
            Navigate(new PropertySubmissionsPage(_api, _session));
        }

        private void Apps_Click(object sender, RoutedEventArgs e)
        {
            Navigate(new LeaseApplicationsPage(_api));
        }

        private void Issues_Click(object sender, RoutedEventArgs e)
        {
            Navigate(new IssuesPage(_api));
        }

        private void Landlords_Click(object sender, RoutedEventArgs e)
        {
            Navigate(new LandlordsPage(_api));
        }

        private void Employees_Click(object sender, RoutedEventArgs e)
        {
            Navigate(new EmployeesPage(_api, _session));
        }

        private void Listings_Click(object sender, RoutedEventArgs e)
        {
            Navigate(new ListingsPage(_api));
        }

        private void Notifications_Click(object sender, RoutedEventArgs e)
        {
            Navigate(new NotificationsPage(_api));
            // Reset bell badge after user navigates to notifications
            UpdateBellBadge(0);
        }

        private void Bell_Click(object sender, RoutedEventArgs e)
        {
            NavNotifications.IsChecked = true;
            Navigate(new NotificationsPage(_api));
            UpdateBellBadge(0);
        }

        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            var win = new Tenurix.Management.Views.Windows.ChangePasswordWindow(_api)
            {
                Owner = this
            };
            win.ShowDialog();
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            _bellTimer?.Stop();
            var login = new LoginWindow();
            login.Show();
            Close();
        }

        private async void MenuProfile_Click(object sender, RoutedEventArgs e)
        {
            var win = new Tenurix.Management.Views.Windows.MyProfileWindow(_api)
            {
                Owner = this
            };

            var updated = win.ShowDialog();

            if (updated == true)
            {
                var me = await _api.GetMyProfileAsync();
                _session.FullName = me.FullName;

                UserText.Text = $"Welcome, {_session.FullName}";
                UserInitialsText.Text = GetInitials(_session.FullName);
            }
        }

        private void MenuLogout_Click(object sender, RoutedEventArgs e)
        {
            _bellTimer?.Stop();
            var login = new LoginWindow();
            login.Show();
            Close();
        }
    }
}
