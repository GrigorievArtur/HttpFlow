using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Httpflow.Desktop.Shell.Controls;

public partial class AppNavigationBar : UserControl
{
    public event EventHandler? ProfileRequested;
    public event EventHandler? ProjectsRequested;
    public event EventHandler? WorkspaceRequested;
    public event EventHandler? DashboardRequested;

    public AppNavigationBar()
    {
        InitializeComponent();
    }

    private void OnProfileButtonClick(object? sender, RoutedEventArgs e)
    {
        ProfileRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnProjectsButtonClick(object? sender, RoutedEventArgs e)
    {
        ProjectsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnWorkspaceButtonClick(object? sender, RoutedEventArgs e)
    {
        WorkspaceRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnDashboardButtonClick(object? sender, RoutedEventArgs e)
    {
        DashboardRequested?.Invoke(this, EventArgs.Empty);
    }
}
