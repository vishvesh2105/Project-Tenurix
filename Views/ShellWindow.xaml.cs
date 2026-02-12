using System;
using System.Linq;
using System.Windows;
using Tenurix.Management.Client.Api;
using Tenurix.Management.Models.Auth;
using Tenurix.Management.Views.Pages;

namespace Tenurix.Management.Views
{
    public partial class ShellWindow : Window
    {
        private readonly TenurixApiClient _api;
        private readonly LoginResponse _session;

        public ShellWindow(TenurixApiClient api, LoginResponse session)
        {
            InitializeComponent();

            _api = api;
            _session = session;

            // Header
            UserText.Text = $"Welcome, {_session.FullName}";
            RoleText.Text = $"Role: {_session.RoleName}";

            ApplyPermissions();

            Navigate("Dashboard", new DashboardPage(_api));
        }

        private async void MyProfile_Click(object sender, RoutedEventArgs e)
        {
            var win = new Tenurix.Management.Views.Windows.MyProfileWindow(_api)
            {
                Owner = this
            };

            var updatedName = win.ShowDialog();

            // After close, refresh header from server if user saved
            if (updatedName == true)
            {
                var me = await _api.GetMyProfileAsync();
                _session.FullName = me.FullName;

                UserText.Text = $"Welcome, {_session.FullName}";
            }
        }


        private void Navigate(string title, object page)
        {
            PageTitleText.Text = title;
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
            DashboardBtn.IsEnabled = true;

            PropsBtn.IsEnabled = Has("REVIEW_PROPERTY") || Has("APPROVE_PROPERTY");
            AppsBtn.IsEnabled = Has("REVIEW_LEASE_APP") || Has("APPROVE_LEASE_APP");
            IssuesBtn.IsEnabled = Has("MANAGE_ISSUES");
            LandlordsBtn.IsEnabled = Has("VIEW_LANDLORD_PORTFOLIO");
            EmployeesBtn.IsEnabled = Has("MANAGE_USERS");
        }

        private void Dashboard_Click(object sender, RoutedEventArgs e) =>
            Navigate("Dashboard", new DashboardPage(_api));

        private void Props_Click(object sender, RoutedEventArgs e) =>
            Navigate("Property Submissions", new PropertySubmissionsPage(_api));

        private void Apps_Click(object sender, RoutedEventArgs e) =>
            Navigate("Lease Applications", new LeaseApplicationsPage(_api));

        private void Issues_Click(object sender, RoutedEventArgs e) =>
            Navigate("Issues", new IssuesPage()); // if your IssuesPage needs session later

        private void Landlords_Click(object sender, RoutedEventArgs e) =>
            Navigate("Landlord Portfolio", new LandlordsPage(_api));

        private void Employees_Click(object sender, RoutedEventArgs e) =>
            Navigate("Employees", new EmployeesPage(_api, _session)); 

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var login = new LoginWindow();
            login.Show();
            Close();
        }



        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            var win = new Tenurix.Management.Views.Windows.ChangePasswordWindow(_api)
            {
                Owner = this
            };
            win.ShowDialog();
        }
    }
}
