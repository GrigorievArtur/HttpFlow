using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Httpflow.Desktop.Dtos.Collaborators;

namespace Httpflow.Desktop.Views;

public partial class DashboardPage : UserControl
{
    private const string AdminRole = "Admin";
    private const string MemberRole = "Member";
    private const string VisitorRole = "Visitor";

    private readonly List<ProjectCollaboratorDto> _collaborators = [];
    private ProjectCollaboratorDto? _selectedCollaborator;
    private int? _currentUserId;
    private string? _currentUserRole;
    private bool _isUpdatingRoleSelection;

    public DashboardPage()
    {
        InitializeComponent();
        Loaded += DashboardPage_OnLoaded;
    }

    private App CurrentApp => (App)Application.Current!;

    private async void DashboardPage_OnLoaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= DashboardPage_OnLoaded;
        await LoadCollaboratorsAsync();
    }

    private void SearchTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        var filtered = GetVisibleCollaborators();
        RenderCollaborators(filtered);

        if (_selectedCollaborator is null
            || !filtered.Any(collaborator => collaborator.UserId == _selectedCollaborator.UserId))
        {
            SelectCollaborator(filtered.FirstOrDefault());
        }
    }

    private async void InviteButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await InviteCollaboratorAsync();
    }

    private async void RoleComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingRoleSelection
            || _selectedCollaborator is null
            || _selectedCollaborator.IsOwner
            || RoleComboBox.SelectedItem is not ComboBoxItem selectedRole)
        {
            return;
        }

        var role = selectedRole.Content?.ToString() ?? _selectedCollaborator.Role;
        if (role == _selectedCollaborator.Role)
        {
            return;
        }

        await UpdateCollaboratorRoleAsync(_selectedCollaborator, role);
    }

    private async Task LoadCollaboratorsAsync(int? selectedUserId = null)
    {
        CollaboratorsListPanel.Children.Clear();
        _collaborators.Clear();
        _selectedCollaborator = null;
        RefreshSelectedCollaboratorData();
        SetManagementControlsEnabled(false);

        if (CurrentApp.SelectedProjectId is not { } projectId)
        {
            ProjectTextBlock.Text = "Select a project";
            StatusTextBlock.Text = "Select a project before opening the dashboard.";
            return;
        }

        ProjectTextBlock.Text = CurrentApp.SelectedProjectName is { Length: > 0 } projectName
            ? $"Project: {projectName}"
            : $"Project #{projectId}";
        StatusTextBlock.Text = "Loading collaborators...";

        try
        {
            var token = await CurrentApp.JwtService.GetTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                StatusTextBlock.Text = "Please log in again to load collaborators.";
                return;
            }

            var currentUserResult = await CurrentApp.AuthApiClient.GetCurrentUserAsync(token);
            if (!currentUserResult.IsSuccess || currentUserResult.Data is null)
            {
                StatusTextBlock.Text = currentUserResult.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        "Your session expired. Please log in again.",
                    _ => currentUserResult.ErrorMessage ?? "Unable to load current user."
                };
                return;
            }

            _currentUserId = currentUserResult.Data.Id;

            var collaboratorsResult = await CurrentApp.CollaboratorsApiClient.GetCollaboratorsAsync(token, projectId);
            if (!collaboratorsResult.IsSuccess)
            {
                StatusTextBlock.Text = collaboratorsResult.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        collaboratorsResult.ErrorMessage ?? "You do not have access to this project.",
                    _ => collaboratorsResult.ErrorMessage ?? "Unable to load collaborators."
                };
                return;
            }

            _collaborators.AddRange(collaboratorsResult.Data ?? []);
            _currentUserRole = _collaborators
                .FirstOrDefault(collaborator => collaborator.UserId == _currentUserId)
                ?.Role;

            var visibleCollaborators = GetVisibleCollaborators();
            RenderCollaborators(visibleCollaborators);
            SelectCollaborator(
                visibleCollaborators.FirstOrDefault(collaborator => collaborator.UserId == selectedUserId)
                ?? visibleCollaborators.FirstOrDefault(collaborator => collaborator.UserId == _currentUserId)
                ?? visibleCollaborators.FirstOrDefault());

            StatusTextBlock.Text = _currentUserRole == AdminRole
                ? "Ready."
                : "Only project admins can invite collaborators or change roles.";
            SetManagementControlsEnabled(_currentUserRole == AdminRole);
        }
        catch (HttpRequestException)
        {
            StatusTextBlock.Text = "Could not reach the backend.";
        }
        catch (Exception)
        {
            StatusTextBlock.Text = "Something went wrong while loading collaborators.";
        }
    }

    private async Task InviteCollaboratorAsync()
    {
        if (CurrentApp.SelectedProjectId is not { } projectId)
        {
            StatusTextBlock.Text = "Select a project before inviting collaborators.";
            return;
        }

        var email = InviteEmailTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email))
        {
            StatusTextBlock.Text = "Email is required.";
            InviteEmailTextBox.Focus();
            return;
        }

        var role = GetSelectedRole(InviteRoleComboBox);
        SetManagementControlsEnabled(false);
        StatusTextBlock.Text = "Inviting collaborator...";

        try
        {
            var token = await CurrentApp.JwtService.GetTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                StatusTextBlock.Text = "Please log in again to invite collaborators.";
                return;
            }

            var result = await CurrentApp.CollaboratorsApiClient.AddCollaboratorAsync(
                token,
                projectId,
                new AddProjectCollaboratorDto(email, role));

            if (!result.IsSuccess)
            {
                StatusTextBlock.Text = result.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        result.ErrorMessage ?? "Only project admins can invite collaborators.",
                    _ => result.ErrorMessage ?? "Unable to invite collaborator."
                };
                return;
            }

            InviteEmailTextBox.Text = string.Empty;
            await LoadCollaboratorsAsync(result.Data?.UserId);
        }
        catch (HttpRequestException)
        {
            StatusTextBlock.Text = "Could not reach the backend.";
        }
        catch (Exception)
        {
            StatusTextBlock.Text = "Something went wrong while inviting the collaborator.";
        }
        finally
        {
            SetManagementControlsEnabled(_currentUserRole == AdminRole);
        }
    }

    private async Task UpdateCollaboratorRoleAsync(ProjectCollaboratorDto collaborator, string role)
    {
        if (CurrentApp.SelectedProjectId is not { } projectId)
        {
            return;
        }

        SetManagementControlsEnabled(false);
        StatusTextBlock.Text = "Updating role...";

        try
        {
            var token = await CurrentApp.JwtService.GetTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                StatusTextBlock.Text = "Please log in again to update roles.";
                return;
            }

            var result = await CurrentApp.CollaboratorsApiClient.UpdateCollaboratorRoleAsync(
                token,
                projectId,
                collaborator.UserId,
                new UpdateProjectCollaboratorRoleDto(role));

            if (!result.IsSuccess)
            {
                StatusTextBlock.Text = result.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        result.ErrorMessage ?? "Only project admins can change roles.",
                    _ => result.ErrorMessage ?? "Unable to update collaborator role."
                };
                RefreshSelectedCollaboratorData();
                return;
            }

            await LoadCollaboratorsAsync(collaborator.UserId);
        }
        catch (HttpRequestException)
        {
            StatusTextBlock.Text = "Could not reach the backend.";
        }
        catch (Exception)
        {
            StatusTextBlock.Text = "Something went wrong while updating the collaborator role.";
        }
        finally
        {
            SetManagementControlsEnabled(_currentUserRole == AdminRole);
        }
    }

    private void RenderCollaborators(IReadOnlyCollection<ProjectCollaboratorDto> collaborators)
    {
        CollaboratorsListPanel.Children.Clear();

        foreach (var collaborator in collaborators)
        {
            var row = CreateCollaboratorRow(collaborator);
            CollaboratorsListPanel.Children.Add(row);
        }
    }

    private IReadOnlyCollection<ProjectCollaboratorDto> GetVisibleCollaborators()
    {
        var query = SearchTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            return _collaborators;
        }

        return _collaborators
            .Where(collaborator => GetFullName(collaborator).Contains(query, StringComparison.OrdinalIgnoreCase)
                || collaborator.Email.Contains(query, StringComparison.OrdinalIgnoreCase)
                || collaborator.Role.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private Border CreateCollaboratorRow(ProjectCollaboratorDto collaborator)
    {
        var border = new Border
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, 16),
            Background = _selectedCollaborator?.UserId == collaborator.UserId
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
        button.Click += (_, _) => SelectCollaborator(collaborator);

        var rowGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };

        rowGrid.Children.Add(new TextBlock
        {
            Text = collaborator.IsOwner
                ? $"{GetFullName(collaborator)} - {collaborator.Role} - Owner"
                : $"{GetFullName(collaborator)} - {collaborator.Role}",
            FontSize = 16,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        });

        var emailLabel = new TextBlock
        {
            Text = collaborator.Email,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        Grid.SetColumn(emailLabel, 1);
        rowGrid.Children.Add(emailLabel);

        button.Content = rowGrid;
        border.Child = button;
        return border;
    }

    private void SelectCollaborator(ProjectCollaboratorDto? collaborator)
    {
        _selectedCollaborator = collaborator;
        RefreshSelectedCollaboratorData();
        RenderCollaborators(GetVisibleCollaborators());
    }

    private void RefreshSelectedCollaboratorData()
    {
        _isUpdatingRoleSelection = true;

        if (_selectedCollaborator is null)
        {
            SelectedNameTextBlock.Text = "Name: No collaborator selected";
            SelectedEmailTextBlock.Text = "-";
            AccessTextBlock.Text = "-";
            RoleComboBox.SelectedIndex = -1;
            RoleComboBox.IsEnabled = false;
            _isUpdatingRoleSelection = false;
            return;
        }

        SelectedNameTextBlock.Text = $"Name: {GetFullName(_selectedCollaborator)}";
        SelectedEmailTextBlock.Text = _selectedCollaborator.Email;
        AccessTextBlock.Text = _selectedCollaborator.IsOwner ? "Owner" : "Collaborator";
        RoleComboBox.SelectedIndex = _selectedCollaborator.Role switch
        {
            AdminRole => 0,
            MemberRole => 1,
            VisitorRole => 2,
            _ => -1
        };
        RoleComboBox.IsEnabled = _currentUserRole == AdminRole && !_selectedCollaborator.IsOwner;
        _isUpdatingRoleSelection = false;
    }

    private void SetManagementControlsEnabled(bool isEnabled)
    {
        InviteEmailTextBox.IsEnabled = isEnabled;
        InviteRoleComboBox.IsEnabled = isEnabled;
        InviteButton.IsEnabled = isEnabled;
        RoleComboBox.IsEnabled = isEnabled && _selectedCollaborator is not null && !_selectedCollaborator.IsOwner;
    }

    private static string GetSelectedRole(ComboBox comboBox)
    {
        return comboBox.SelectedItem is ComboBoxItem item && item.Content is not null
            ? item.Content.ToString() ?? MemberRole
            : MemberRole;
    }

    private static string GetFullName(ProjectCollaboratorDto collaborator)
    {
        return $"{collaborator.Firstname} {collaborator.Lastname}";
    }
}
