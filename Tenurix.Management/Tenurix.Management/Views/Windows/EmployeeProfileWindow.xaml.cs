using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Tenurix.Management.Client.Api;
using Tenurix.Management.Models.Auth;
using Tenurix.Management.Client.Models; // EmployeeDto



namespace Tenurix.Management.Views.Windows;

public partial class EmployeeProfileWindow : Window
{
    // Simple row for display
    public sealed class BlockRow
    {
        public string Type { get; set; } = "";          // Shift / Break
        public string? BreakType { get; set; }
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public int? StartEventId { get; set; }
        public int? EndEventId { get; set; }

        public string StartLocal => DateTime.SpecifyKind(StartUtc, DateTimeKind.Utc).ToLocalTime().ToString("g");
        public string EndLocal => DateTime.SpecifyKind(EndUtc, DateTimeKind.Utc).ToLocalTime().ToString("g");

        public string DurationText
        {
            get
            {
                var mins = Math.Max(0, (int)(EndUtc - StartUtc).TotalMinutes);
                return $"{mins / 60:D2}:{mins % 60:D2}";
            }
        }
    }

    private readonly TenurixApiClient _api;
    private readonly LoginResponse _session;
    private readonly int _userId;
    private readonly EmployeeDto? _employee;

    // Bindable UI fields
    public string HeaderTitle => _employee != null ? _employee.FullName : $"Employee #{_userId}";
    public string HeaderSubtitle => _employee != null ? $"{_employee.Email} • {_employee.RoleName}" : "";

    public string SelectedDayTitle { get; private set; } = "Select a day";
    public string WorkedText { get; private set; } = "--";
    public string BreaksText { get; private set; } = "--";
    public string ShortBreakCountText { get; private set; } = "--";

    public Visibility CanEditAttendanceVisibility =>
        Has("ATTENDANCE_EDIT") ? Visibility.Visible : Visibility.Collapsed;

    public EmployeeProfileWindow(TenurixApiClient api, LoginResponse session, int userId, EmployeeDto? employee)
    {
        InitializeComponent();
        _api = api;
        _session = session;
        _userId = userId;
        _employee = employee;

        DataContext = this;
        Loaded += EmployeeProfileWindow_Loaded;
    }

    private bool Has(string permissionKey) =>
        _session.Permissions != null &&
        _session.Permissions.Any(p => string.Equals(p, permissionKey, StringComparison.OrdinalIgnoreCase));

    private async void EmployeeProfileWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Default select today
        Cal.SelectedDate = DateTime.Today;
        await LoadDay(DateTime.Today);
    }

    private async void Cal_SelectedDatesChanged(object? sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (Cal.SelectedDate is DateTime day)
            await LoadDay(day.Date);
    }

    private async Task LoadDay(DateTime dayLocalDate)
    {
        try
        {
            // Day window in UTC (use local day boundaries converted to UTC)
            var localStart = dayLocalDate.Date;
            var localEnd = localStart.AddDays(1);

            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(localStart);
            var toUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd);

            // Blocks
            var blocks = await _api.GetUserAttendanceBlocksAsync(_userId, fromUtc, toUtc);

            var rows = blocks.Select(b => new BlockRow
            {
                Type = b.Type,
                BreakType = b.BreakType,
                StartUtc = b.StartUtc,
                EndUtc = b.EndUtc,
                StartEventId = b.StartEventId,
                EndEventId = b.EndEventId
            }).ToList();

            BlocksGrid.ItemsSource = rows;

            // Summary (re-use your summary endpoint)
            var summary = await _api.GetUserAttendanceSummaryAsync(_userId, fromUtc, toUtc);
            // summary returns list by day; take first (the selected day)
            var s = summary.FirstOrDefault();

            SelectedDayTitle = dayLocalDate.ToString("D");

            if (s == null)
            {
                WorkedText = "00:00";
                BreaksText = "00:00";
                ShortBreakCountText = "0";
            }
            else
            {
                WorkedText = ToHHmm(s.MinutesWorked);
                BreaksText = ToHHmm(s.MinutesBreaks);
                ShortBreakCountText = s.ShortBreakCount.ToString();
            }

            // refresh bound props
            DataContext = null;
            DataContext = this;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to load attendance:\n" + ex.Message);
        }
    }

    private static string ToHHmm(int minutes)
    {
        minutes = Math.Max(0, minutes);
        return $"{minutes / 60:D2}:{minutes % 60:D2}";
    }

    private async void Void_Click(object sender, RoutedEventArgs e)
    {
        if (!Has("ATTENDANCE_EDIT"))
        {
            MessageBox.Show("You do not have permission to edit attendance.");
            return;
        }

        if (sender is not System.Windows.Controls.Button btn || btn.CommandParameter is not BlockRow row)
            return;

        // For safety: void the "end" event if present, otherwise start.
        var targetEventId = row.EndEventId ?? row.StartEventId;
        if (targetEventId is null)
        {
            MessageBox.Show("Cannot void: missing event id.");
            return;
        }

        var reason = Microsoft.VisualBasic.Interaction.InputBox(
            "Reason for voiding this record:", "Void Attendance", "Correction");

        if (string.IsNullOrWhiteSpace(reason))
            return;

        try
        {
            await _api.VoidAttendanceEventAsync(targetEventId.Value, reason);

            if (Cal.SelectedDate is DateTime day)
                await LoadDay(day.Date);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to void:\n" + ex.Message);
        }
    }

    private async void AdminPunch_Click(object sender, RoutedEventArgs e)
    {
        if (!Has("ATTENDANCE_EDIT"))
        {
            MessageBox.Show("You do not have permission to admin punch.");
            return;
        }

        // Minimal “admin punch” without extra windows:
        // You can upgrade this to a proper dialog later.
        var eventType = Microsoft.VisualBasic.Interaction.InputBox(
            "EventType (ShiftStart/ShiftEnd/BreakStart/BreakEnd):", "Admin Punch", "ShiftStart");

        if (string.IsNullOrWhiteSpace(eventType)) return;

        string? breakType = null;
        if (eventType.StartsWith("Break", StringComparison.OrdinalIgnoreCase))
        {
            breakType = Microsoft.VisualBasic.Interaction.InputBox(
                "BreakType (Lunch/ShortBreak):", "Admin Punch", "Lunch");
            if (string.IsNullOrWhiteSpace(breakType)) return;
        }

        try
        {
            await _api.AdminPunchAttendanceAsync(_userId, new TenurixApiClient.AttendancePunchRequest
            {
                EventType = eventType,
                BreakType = breakType,
                Source = "Admin",
                Note = $"Admin punch: {eventType}"
            });

            if (Cal.SelectedDate is DateTime day)
                await LoadDay(day.Date);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed admin punch:\n" + ex.Message);
        }
    }
}
