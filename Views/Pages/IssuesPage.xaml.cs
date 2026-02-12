using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Tenurix.Management.Views.Pages;

public partial class IssuesPage : Page
{
    private sealed class IssueRow
    {
        public int IssueId { get; set; }
        public string PropertyAddress { get; set; } = "";
        public string ReportedBy { get; set; } = "";
        public string Priority { get; set; } = "";
        public string Status { get; set; } = "";
    }

    private List<IssueRow> _rows = new();

    public IssuesPage()
    {
        InitializeComponent();
        LoadSample();
    }

    private void LoadSample()
    {
        _rows = new List<IssueRow>
        {
            new() { IssueId=9001, PropertyAddress="123 King St W", ReportedBy="tenant1@tenurix.com", Priority="High", Status="Open" },
            new() { IssueId=9002, PropertyAddress="45 Weber St", ReportedBy="tenant2@tenurix.com", Priority="Medium", Status="In Progress" },
        };

        Grid.ItemsSource = _rows;
    }

    private IssueRow? Selected() => Grid.SelectedItem as IssueRow;

    private void MarkInProgress_Click(object sender, RoutedEventArgs e)
    {
        var row = Selected();
        if (row == null)
        {
            MessageBox.Show("Please select an issue first.");
            return;
        }

        // Later: API call to update issue status in SQL
        row.Status = "In Progress";
        Refresh();
    }

    private void MarkResolved_Click(object sender, RoutedEventArgs e)
    {
        var row = Selected();
        if (row == null)
        {
            MessageBox.Show("Please select an issue first.");
            return;
        }

        // Later: API call to update issue status in SQL
        row.Status = "Resolved";
        Refresh();
    }

    private void Refresh()
    {
        Grid.ItemsSource = null;
        Grid.ItemsSource = _rows.ToList();
    }
}
