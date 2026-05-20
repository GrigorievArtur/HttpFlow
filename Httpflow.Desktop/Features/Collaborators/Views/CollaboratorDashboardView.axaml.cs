using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Httpflow.Desktop.Features.Collaborators.ViewModels;
using Httpflow.Desktop.Dtos.Collaborators;

namespace Httpflow.Desktop.Features.Collaborators.Views;

public partial class CollaboratorDashboardView : UserControl
{
    private readonly CollaboratorDashboardViewModel _viewModel;

    public CollaboratorDashboardView()
    {
        InitializeComponent();
        _viewModel = new CollaboratorDashboardViewModel((App)Application.Current!);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        SyncInviteRole();
        await _viewModel.LoadCollaboratorsAsync();
        RenderCollaborators();
        RefreshRoleSelection();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CollaboratorDashboardViewModel.SearchText)
            or nameof(CollaboratorDashboardViewModel.SelectedCollaborator)
            or nameof(CollaboratorDashboardViewModel.IsManagementEnabled)
            or nameof(CollaboratorDashboardViewModel.CollaboratorSortMode)
            or nameof(CollaboratorDashboardViewModel.StatusText))
        {
            RenderCollaborators();
            RefreshRoleSelection();
        }
    }

    private void OnRoleComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_viewModel.IsUpdatingRoleSelection
            || RoleComboBox.SelectedItem is not ComboBoxItem selectedRole)
        {
            return;
        }

        _viewModel.SelectedRole = selectedRole.Content?.ToString() ?? _viewModel.SelectedRole;
    }

    private void RenderCollaborators()
    {
        CollaboratorsListPanel.Children.Clear();

        foreach (var collaborator in _viewModel.VisibleCollaborators)
        {
            CollaboratorsListPanel.Children.Add(CreateCollaboratorRow(collaborator));
        }
    }

    private Border CreateCollaboratorRow(ProjectCollaboratorDto collaborator)
    {
        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#D9DEE8")),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 14),
            Background = _viewModel.SelectedCollaborator?.UserId == collaborator.UserId
                ? new SolidColorBrush(Color.Parse("#EEF3FF"))
                : Brushes.Transparent
        };

        var button = new Button
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Command = _viewModel.SelectCollaboratorCommand,
            CommandParameter = collaborator
        };

        var rowGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 10
        };

        var status = collaborator.IsOwner
            ? "Owner"
            : collaborator.Status;

        rowGrid.Children.Add(new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = $"{collaborator.Email} - {collaborator.Role}",
                    FontSize = 15,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text = CollaboratorDashboardViewModel.GetFullName(collaborator),
                    Opacity = 0.72
                }
            }
        });

        var statusLabel = new TextBlock
        {
            Text = status,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumn(statusLabel, 1);
        rowGrid.Children.Add(statusLabel);

        button.Content = rowGrid;
        border.Child = button;
        return border;
    }

    private void RefreshRoleSelection()
    {
        _viewModel.IsUpdatingRoleSelection = true;
        RoleComboBox.SelectedIndex = _viewModel.SelectedRole switch
        {
            "Admin" => 0,
            "Member" => 1,
            "Visitor" => 2,
            _ => 1
        };
        _viewModel.IsUpdatingRoleSelection = false;
    }

    private void SyncInviteRole()
    {
        if (InviteRoleComboBox.SelectedItem is ComboBoxItem item)
        {
            _viewModel.InviteRole = item.Content?.ToString() ?? _viewModel.InviteRole;
        }

        InviteRoleComboBox.SelectionChanged += (_, _) =>
        {
            if (InviteRoleComboBox.SelectedItem is ComboBoxItem selected)
            {
                _viewModel.InviteRole = selected.Content?.ToString() ?? _viewModel.InviteRole;
            }
        };
    }
}
