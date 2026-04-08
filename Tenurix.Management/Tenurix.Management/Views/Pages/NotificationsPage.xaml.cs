using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Tenurix.Management.Client.Api;
using Tenurix.Management.Client.Models;

namespace Tenurix.Management.Views.Pages;

public partial class NotificationsPage : Page
{
    private readonly TenurixApiClient _api;
    private int _currentPage = 1;
    private const int PageSize = 20;
    private int _totalCount = 0;
    private readonly List<NotificationDto> _items = new();
    private bool _loadingMore = false;

    public NotificationsPage(TenurixApiClient api)
    {
        _api = api;
        InitializeComponent();
        Loaded += async (_, __) => await LoadAsync(reset: true);
    }

    private async System.Threading.Tasks.Task LoadAsync(bool reset)
    {
        if (reset)
        {
            _currentPage = 1;
            _items.Clear();
        }

        LoadingOverlay.Visibility = Visibility.Visible;
        EmptyState.Visibility = Visibility.Collapsed;
        try
        {
            var result = await _api.GetNotificationsAsync(_currentPage, PageSize);
            _totalCount = result.TotalCount;

            _items.AddRange(result.Items);
            NotifList.ItemsSource = null;
            NotifList.ItemsSource = _items;

            EmptyText.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyState.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            int unread = result.UnreadCount;
            UnreadLabel.Text = unread > 0
                ? $"{unread} unread notification{(unread == 1 ? "" : "s")}"
                : "All caught up!";

            LoadMoreBtn.Visibility = _items.Count < _totalCount ? Visibility.Visible : Visibility.Collapsed;
        }
        catch
        {
            MessageBox.Show("Failed to load notifications. Please try again.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private async void NotifRow_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not NotificationDto notif) return;
        if (notif.IsRead) return;

        try
        {
            await _api.MarkNotificationReadAsync(notif.NotificationId);
            notif.IsRead = true;

            // Refresh list so the unread dot updates
            NotifList.ItemsSource = null;
            NotifList.ItemsSource = _items;

            // Update unread label
            int unread = 0;
            foreach (var n in _items)
                if (!n.IsRead) unread++;

            UnreadLabel.Text = unread > 0
                ? $"{unread} unread notification{(unread == 1 ? "" : "s")}"
                : "All caught up!";
        }
        catch
        {
            // Non-critical — swallow silently
        }
    }

    private async void MarkAllRead_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _api.MarkAllNotificationsReadAsync();
            await LoadAsync(reset: true);
        }
        catch
        {
            MessageBox.Show("Failed to mark all as read. Please try again.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadAsync(reset: true);
    }

    private async void LoadMore_Click(object sender, RoutedEventArgs e)
    {
        if (_loadingMore) return;
        _loadingMore = true;
        try
        {
            _currentPage++;
            await LoadAsync(reset: false);
        }
        finally
        {
            _loadingMore = false;
        }
    }
}
