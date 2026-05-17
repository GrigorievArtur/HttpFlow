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
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, 16),
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
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };

        rowGrid.Children.Add(new TextBlock
        {
            Text = collaborator.IsOwner
                ? $"{CollaboratorDashboardViewModel.GetFullName(collaborator)} - {collaborator.Role} - Owner"
                : $"{CollaboratorDashboardViewModel.GetFullName(collaborator)} - {collaborator.Role}",
            FontSize = 16,
            VerticalAlignment = VerticalAlignment.Center
        });

        var emailLabel = new TextBlock
        {
            Text = collaborator.Email,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(emailLabel, 1);
        rowGrid.Children.Add(emailLabel);

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
