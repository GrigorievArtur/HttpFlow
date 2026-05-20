using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Httpflow.Desktop.Dtos.Collaborators;
using Httpflow.Desktop.Services.Collaborators;
using Httpflow.Desktop.ViewModels;

namespace Httpflow.Desktop.Features.Collaborators.ViewModels;

public enum CollaboratorSortMode
{
    DateJoined,
    Online,
    Role
}

public partial class CollaboratorDashboardViewModel : ViewModelBase
{
    private const string AdminRole = "Admin";
    private const string MemberRole = "Member";
    private const string VisitorRole = "Visitor";

    private readonly App _app;

    public CollaboratorDashboardViewModel(App app)
    {
        _app = app;
    }

    public List<ProjectCollaboratorDto> Collaborators { get; } = [];

    public IReadOnlyCollection<ProjectCollaboratorDto> VisibleCollaborators =>
        ApplySort(FilteredCollaborators).ToList();

    public string SortButtonText => CollaboratorSortMode switch
    {
        CollaboratorSortMode.DateJoined => "Date joined",
        CollaboratorSortMode.Online => "Online",
        CollaboratorSortMode.Role => "Role",
        _ => "Date joined"
    };

    public bool CanEditSelectedRole =>
        IsManagementEnabled && SelectedCollaborator is { IsOwner: false };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleCollaborators))]
    private string searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleCollaborators))]
    [NotifyPropertyChangedFor(nameof(SortButtonText))]
    private CollaboratorSortMode collaboratorSortMode;

    [ObservableProperty]
    private string projectText = "Select a project";

    [ObservableProperty]
    private string inviteEmail = string.Empty;

    [ObservableProperty]
    private string inviteRole = MemberRole;

    [ObservableProperty]
    private string statusText = "Select a project to manage collaborators.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditSelectedRole))]
    private bool isManagementEnabled;

    [ObservableProperty]
    private bool isUpdatingRoleSelection;

    [ObservableProperty]
    private int? currentUserId;

    [ObservableProperty]
    private string? currentUserRole;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditSelectedRole))]
    private ProjectCollaboratorDto? selectedCollaborator;

    [ObservableProperty]
    private string selectedCollaboratorName = "No collaborator selected";

    [ObservableProperty]
    private string selectedCollaboratorEmail = "-";

    [ObservableProperty]
    private string accessText = "-";

    [ObservableProperty]
    private string selectedCollaboratorStatus = "-";

    [ObservableProperty]
    private string selectedCollaboratorJoinedText = "-";

    [ObservableProperty]
    private string selectedCollaboratorOnlineText = "-";

    [ObservableProperty]
    private string selectedRole = MemberRole;

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(VisibleCollaborators));

        if (SelectedCollaborator is null
            || !VisibleCollaborators.Any(collaborator => collaborator.UserId == SelectedCollaborator.UserId))
        {
            SelectCollaborator(VisibleCollaborators.FirstOrDefault());
        }
    }

    partial void OnSelectedCollaboratorChanged(ProjectCollaboratorDto? value)
    {
        IsUpdatingRoleSelection = true;

        if (value is null)
        {
            SelectedCollaboratorName = "No collaborator selected";
            SelectedCollaboratorEmail = "-";
            AccessText = "-";
            SelectedCollaboratorStatus = "-";
            SelectedCollaboratorJoinedText = "-";
            SelectedCollaboratorOnlineText = "-";
            SelectedRole = MemberRole;
        }
        else
        {
            SelectedCollaboratorName = GetFullName(value);
            SelectedCollaboratorEmail = value.Email;
            AccessText = value.IsOwner ? "Owner" : value.Role;
            SelectedCollaboratorStatus = value.Status;
            SelectedCollaboratorJoinedText = value.IsOwner
                ? "Owner"
                : value.JoinedAt is null
                    ? "Pending"
                    : value.JoinedAt.Value.ToLocalTime().ToString("g");
            SelectedCollaboratorOnlineText = value.IsOnline ? "Online" : "Offline";
            SelectedRole = value.Role;
        }

        IsUpdatingRoleSelection = false;
    }

    partial void OnSelectedRoleChanged(string value)
    {
        if (IsUpdatingRoleSelection
            || SelectedCollaborator is null
            || SelectedCollaborator.IsOwner
            || value == SelectedCollaborator.Role)
        {
            return;
        }

        _ = UpdateCollaboratorRoleAsync(SelectedCollaborator, value);
    }

    public async Task LoadCollaboratorsAsync(int? selectedUserId = null)
    {
        Collaborators.Clear();
        SelectedCollaborator = null;
        IsManagementEnabled = false;

        if (_app.CurrentProject is not { } project)
        {
            ProjectText = "Select a project";
            StatusText = "Select a project before opening the dashboard.";
            NotifyCollaboratorsChanged();
            return;
        }

        ProjectText = string.IsNullOrWhiteSpace(project.Name)
            ? $"Project #{project.Id}"
            : $"Project: {project.Name}";
        StatusText = "Loading collaborators...";

        try
        {
            var token = await _app.JwtSessionService.GetTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                StatusText = "Please log in again to load collaborators.";
                NotifyCollaboratorsChanged();
                return;
            }

            var currentUserResult = await _app.AuthApiClient.GetCurrentUserAsync(token);
            if (!currentUserResult.IsSuccess || currentUserResult.Data is null)
            {
                StatusText = currentUserResult.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        "Your session expired. Please log in again.",
                    _ => currentUserResult.ErrorMessage ?? "Unable to load current user."
                };
                NotifyCollaboratorsChanged();
                return;
            }

            CurrentUserId = currentUserResult.Data.Id;

            var collaboratorsResult = await _app.CollaboratorsApiClient.GetCollaboratorsAsync(token, project.Id);
            if (!collaboratorsResult.IsSuccess)
            {
                StatusText = collaboratorsResult.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        collaboratorsResult.ErrorMessage ?? "You do not have access to this project.",
                    _ => collaboratorsResult.ErrorMessage ?? "Unable to load collaborators."
                };
                NotifyCollaboratorsChanged();
                return;
            }

            Collaborators.AddRange(collaboratorsResult.Data ?? []);
            CurrentUserRole = Collaborators
                .FirstOrDefault(collaborator => collaborator.UserId == CurrentUserId)
                ?.Role;

            NotifyCollaboratorsChanged();

            SelectCollaborator(
                VisibleCollaborators.FirstOrDefault(collaborator => collaborator.UserId == selectedUserId)
                ?? VisibleCollaborators.FirstOrDefault(collaborator => collaborator.UserId == CurrentUserId)
                ?? VisibleCollaborators.FirstOrDefault());

            StatusText = CurrentUserRole == AdminRole
                ? "Ready."
                : "Only project admins can invite collaborators or change roles.";
            IsManagementEnabled = CurrentUserRole == AdminRole;
        }
        catch (HttpRequestException)
        {
            StatusText = "Could not reach the backend.";
            NotifyCollaboratorsChanged();
        }
        catch (Exception)
        {
            StatusText = "Something went wrong while loading collaborators.";
            NotifyCollaboratorsChanged();
        }
    }

    [RelayCommand]
    private void CycleSortMode()
    {
        CollaboratorSortMode = CollaboratorSortMode switch
        {
            CollaboratorSortMode.DateJoined => CollaboratorSortMode.Online,
            CollaboratorSortMode.Online => CollaboratorSortMode.Role,
            _ => CollaboratorSortMode.DateJoined
        };
    }

    [RelayCommand]
    private void SelectCollaborator(ProjectCollaboratorDto? collaborator)
    {
        SelectedCollaborator = collaborator;
        NotifyCollaboratorsChanged();
    }

    [RelayCommand]
    private async Task InviteCollaboratorAsync()
    {
        if (_app.CurrentProject is not { } project)
        {
            StatusText = "Select a project before inviting collaborators.";
            return;
        }

        var email = InviteEmail.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            StatusText = "Email is required.";
            return;
        }

        IsManagementEnabled = false;
        StatusText = "Inviting collaborator...";

        try
        {
            var token = await _app.JwtSessionService.GetTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                StatusText = "Please log in again to invite collaborators.";
                return;
            }

            var result = await _app.CollaboratorsApiClient.AddCollaboratorAsync(
                token,
                project.Id,
                new AddProjectCollaboratorDto(email, InviteRole));

            if (!result.IsSuccess)
            {
                StatusText = result.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        result.ErrorMessage ?? "Only project admins can invite collaborators.",
                    _ => result.ErrorMessage ?? "Unable to invite collaborator."
                };
                return;
            }

            InviteEmail = string.Empty;
            await LoadCollaboratorsAsync(result.Data?.UserId);
            StatusText = "Invite sent. They can accept or decline it from Profile.";
        }
        catch (HttpRequestException)
        {
            StatusText = "Could not reach the backend.";
        }
        catch (Exception)
        {
            StatusText = "Something went wrong while inviting the collaborator.";
        }
        finally
        {
            IsManagementEnabled = CurrentUserRole == AdminRole;
        }
    }

    public async Task UpdateCollaboratorRoleAsync(ProjectCollaboratorDto collaborator, string role)
    {
        if (_app.CurrentProject is not { } project)
        {
            return;
        }

        IsManagementEnabled = false;
        StatusText = "Updating role...";

        try
        {
            var token = await _app.JwtSessionService.GetTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                StatusText = "Please log in again to update roles.";
                return;
            }

            var result = await _app.CollaboratorsApiClient.UpdateCollaboratorRoleAsync(
                token,
                project.Id,
                collaborator.UserId,
                new UpdateProjectCollaboratorRoleDto(role));

            if (!result.IsSuccess)
            {
                StatusText = result.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        result.ErrorMessage ?? "Only project admins can change roles.",
                    _ => result.ErrorMessage ?? "Unable to update collaborator role."
                };
                SelectCollaborator(SelectedCollaborator);
                return;
            }

            await LoadCollaboratorsAsync(collaborator.UserId);
        }
        catch (HttpRequestException)
        {
            StatusText = "Could not reach the backend.";
        }
        catch (Exception)
        {
            StatusText = "Something went wrong while updating the collaborator role.";
        }
        finally
        {
            IsManagementEnabled = CurrentUserRole == AdminRole;
        }
    }

    public void NotifyCollaboratorsChanged()
    {
        OnPropertyChanged(nameof(VisibleCollaborators));
        OnPropertyChanged(nameof(CanEditSelectedRole));
    }

    public static string GetFullName(ProjectCollaboratorDto collaborator) =>
        $"{collaborator.Firstname} {collaborator.Lastname}";

    private IReadOnlyCollection<ProjectCollaboratorDto> FilteredCollaborators =>
        string.IsNullOrWhiteSpace(SearchText)
            ? Collaborators
            : Collaborators
                .Where(collaborator => GetFullName(collaborator).Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                    || collaborator.Email.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                    || collaborator.Role.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                    || collaborator.Status.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();

    private IEnumerable<ProjectCollaboratorDto> ApplySort(IEnumerable<ProjectCollaboratorDto> collaborators)
    {
        return CollaboratorSortMode switch
        {
            CollaboratorSortMode.Online => collaborators
                .OrderByDescending(collaborator => collaborator.IsOnline)
                .ThenBy(collaborator => collaborator.Email),
            CollaboratorSortMode.Role => collaborators
                .OrderBy(collaborator => GetRoleRank(collaborator.Role))
                .ThenBy(collaborator => collaborator.Email),
            _ => collaborators
                .OrderByDescending(collaborator => collaborator.IsOwner)
                .ThenByDescending(collaborator => collaborator.JoinedAt ?? collaborator.InvitedAt)
                .ThenBy(collaborator => collaborator.Email)
        };
    }

    private static int GetRoleRank(string role)
    {
        return role switch
        {
            AdminRole => 0,
            MemberRole => 1,
            VisitorRole => 2,
            _ => 3
        };
    }
}
