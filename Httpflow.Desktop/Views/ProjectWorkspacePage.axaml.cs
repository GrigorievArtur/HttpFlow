using Avalonia.Controls;

namespace Httpflow.Desktop.Views;

public partial class ProjectWorkspacePage : UserControl
{
    private bool _isSidebarOpen = true;

    public ProjectWorkspacePage()
    {
        InitializeComponent();
    }

    private void SidebarToggleButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _isSidebarOpen = !_isSidebarOpen;
        SidebarPanel.IsVisible = _isSidebarOpen;
        WorkspaceLayout.ColumnDefinitions[2].Width = new GridLength(_isSidebarOpen ? 392 : 0);
        SidebarToggleButton.Content = _isSidebarOpen ? ">>" : "<<";
    }
}
