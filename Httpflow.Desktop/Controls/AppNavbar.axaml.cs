using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Httpflow.Desktop.Controls;

public partial class AppNavbar : UserControl
{
    public event EventHandler? ProjectsRequested;
    public event EventHandler? WorkspaceRequested;
    public event EventHandler? DashboardRequested;

    public AppNavbar()
    {
        InitializeComponent();
    }

    private void ProjectsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ProjectsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void WorkspaceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        WorkspaceRequested?.Invoke(this, EventArgs.Empty);
    }

    private void DashboardButton_OnClick(object? sender, RoutedEventArgs e)
    {
        DashboardRequested?.Invoke(this, EventArgs.Empty);
    }
}
