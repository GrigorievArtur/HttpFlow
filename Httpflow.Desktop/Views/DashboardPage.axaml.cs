using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Httpflow.Desktop.Views;

public partial class DashboardPage : UserControl
{
    private readonly List<Teammate> _teammates =
    [
        new("Alex Johnson", "Admin", true, "2024-01-14", "2026-05-05 09:42"),
        new("Priya Patel", "Member", false, "2024-03-10", "2026-04-28 16:05"),
        new("Jordan Kim", "Member", true, "2024-06-22", "2026-05-12 08:17")
    ];

    private Teammate? _selectedTeammate;

    public DashboardPage()
    {
        InitializeComponent();

        RenderTeammates(_teammates);
        SelectTeammate(_teammates[0]);
    }

    private void SearchTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        var query = SearchTextBox.Text?.Trim() ?? string.Empty;
        var filtered = _teammates
            .Where(teammate => teammate.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || teammate.Role.Contains(query, StringComparison.OrdinalIgnoreCase)
                || teammate.StatusLabel.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        RenderTeammates(filtered);

        if (_selectedTeammate is null || !filtered.Contains(_selectedTeammate))
        {
            SelectTeammate(filtered.FirstOrDefault());
        }
    }

    private void RoleComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_selectedTeammate is null || RoleComboBox.SelectedItem is not ComboBoxItem selectedRole)
        {
            return;
        }

        _selectedTeammate.Role = selectedRole.Content?.ToString() ?? _selectedTeammate.Role;
        RefreshSelectedUserData();
        RenderTeammates(GetVisibleTeammates());
    }

    private void RenderTeammates(IReadOnlyCollection<Teammate> teammates)
    {
        TeammatesListPanel.Children.Clear();

        foreach (var teammate in teammates)
        {
            var row = CreateTeammateRow(teammate);
            TeammatesListPanel.Children.Add(row);
        }
    }

    private IReadOnlyCollection<Teammate> GetVisibleTeammates()
    {
        var query = SearchTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            return _teammates;
        }

        return _teammates
            .Where(teammate => teammate.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || teammate.Role.Contains(query, StringComparison.OrdinalIgnoreCase)
                || teammate.StatusLabel.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private Border CreateTeammateRow(Teammate teammate)
    {
        var border = new Border
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, 16),
            Background = _selectedTeammate == teammate
                ? new SolidColorBrush(Color.Parse("#EEF3FF"))
                : Brushes.Transparent
        };

        var button = new Button
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
        };
        button.Click += (_, _) => SelectTeammate(teammate);

        var rowGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };

        rowGrid.Children.Add(new TextBlock
        {
            Text = $"{teammate.Name} - {teammate.Role} - {teammate.StatusLabel}",
            FontSize = 16,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        });

        var statusDot = new Border
        {
            Width = 14,
            Height = 14,
            CornerRadius = new CornerRadius(7),
            Background = teammate.IsOnline
                ? new SolidColorBrush(Color.Parse("#33A852"))
                : new SolidColorBrush(Color.Parse("#8F95A3")),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        Grid.SetColumn(statusDot, 1);
        rowGrid.Children.Add(statusDot);

        button.Content = rowGrid;
        border.Child = button;
        return border;
    }

    private void SelectTeammate(Teammate? teammate)
    {
        _selectedTeammate = teammate;
        RefreshSelectedUserData();
        RenderTeammates(GetVisibleTeammates());
    }

    private void RefreshSelectedUserData()
    {
        if (_selectedTeammate is null)
        {
            SelectedNameTextBlock.Text = "Name: No teammate selected";
            InvitedTextBlock.Text = "-";
            LastLoginTextBlock.Text = "-";
            StatusTextBlock.Text = "Status: -";
            RoleComboBox.SelectedIndex = -1;
            return;
        }

        SelectedNameTextBlock.Text = $"Name: {_selectedTeammate.Name}";
        InvitedTextBlock.Text = _selectedTeammate.InvitedOn;
        LastLoginTextBlock.Text = _selectedTeammate.LastLogin;
        StatusTextBlock.Text = $"Status: {_selectedTeammate.StatusLabel}";
        RoleComboBox.SelectedIndex = _selectedTeammate.Role switch
        {
            "Admin" => 0,
            "Member" => 1,
            "Viewer" => 2,
            _ => -1
        };
    }

    private sealed class Teammate(
        string name,
        string role,
        bool isOnline,
        string invitedOn,
        string lastLogin)
    {
        public string Name { get; } = name;
        public bool IsOnline { get; } = isOnline;
        public string InvitedOn { get; } = invitedOn;
        public string LastLogin { get; } = lastLogin;
        public string Role { get; set; } = role;
        public string StatusLabel => IsOnline ? "Online" : "Offline";
    }
}
