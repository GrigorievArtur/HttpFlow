using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Httpflow.Desktop.Dtos.Projects;
using Httpflow.Desktop.Services.Projects;
using Httpflow.Desktop.ViewModels;

namespace Httpflow.Desktop.Features.Projects.ViewModels;

public partial class ProjectListViewModel : ViewModelBase
{
    private const string DefaultProjectValue = "{}";
    private const int PageSize = 5;

    private readonly App _app;

    public ProjectListViewModel(App app, string quickActionsText)
    {
        _app = app;
        QuickActionsText = quickActionsText;
    }

    public event EventHandler? WorkspaceRequested;

    public List<ProjectDto> CurrentPageProjects { get; } = [];

    public IReadOnlyList<ProjectDto> VisibleProjects =>
        string.IsNullOrWhiteSpace(SearchText)
            ? CurrentPageProjects
            : CurrentPageProjects
                .Where(project => project.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();

    public string CurrentPageDisplay => $"Page {CurrentPage}";

    public bool CanGoPrevious => CurrentPage > 1;

    public bool CanGoNext => HasNextPage;

    public bool HasCreateProjectMessage => !string.IsNullOrWhiteSpace(CreateProjectMessage);

    public bool IsCreateProjectButtonsEnabled => !IsCreateProjectBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleProjects))]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string quickActionsText = "Quick actions";

    [ObservableProperty]
    private string projectsStatusText = "Loading projects...";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPageDisplay), nameof(CanGoPrevious))]
    private int currentPage = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    private bool hasNextPage;

    [ObservableProperty]
    private bool isCreateProjectDialogOpen;

    [ObservableProperty]
    private string createProjectName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCreateProjectMessage))]
    private string createProjectMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCreateProjectButtonsEnabled))]
    private bool isCreateProjectBusy;

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(VisibleProjects));
    }

    public async Task LoadProjectListAsync()
    {
        ProjectsStatusText = "Loading projects...";

        try
        {
            var token = await _app.JwtSessionService.GetTokenAsync();

            if (string.IsNullOrWhiteSpace(token))
            {
                ProjectsStatusText = "Please log in again to load projects.";
                CurrentPageProjects.Clear();
                NotifyProjectsChanged();
                return;
            }

            var result = await _app.ProjectsApiClient.GetProjectsAsync(token, CurrentPage, PageSize);

            if (!result.IsSuccess)
            {
                ProjectsStatusText = result.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        "Your session expired. Please log in again.",
                    _ => result.ErrorMessage ?? "Unable to load projects."
                };
                CurrentPageProjects.Clear();
                NotifyProjectsChanged();
                return;
            }

            CurrentPageProjects.Clear();
            CurrentPageProjects.AddRange(result.Data ?? []);
            HasNextPage = CurrentPageProjects.Count == PageSize;
            UpdateProjectsStatusText();
            NotifyProjectsChanged();
        }
        catch (HttpRequestException)
        {
            ProjectsStatusText = "Could not reach the backend.";
            CurrentPageProjects.Clear();
            NotifyProjectsChanged();
        }
        catch
        {
            ProjectsStatusText = "Something went wrong while loading projects.";
            CurrentPageProjects.Clear();
            NotifyProjectsChanged();
        }
    }

    [RelayCommand]
    private void OpenCreateProjectDialog()
    {
        CreateProjectMessage = string.Empty;
        CreateProjectName = string.Empty;
        IsCreateProjectDialogOpen = true;
    }

    [RelayCommand]
    private void CancelCreateProjectDialog()
    {
        IsCreateProjectDialogOpen = false;
    }

    [RelayCommand]
    private async Task ConfirmCreateProjectAsync()
    {
        var projectName = CreateProjectName.Trim();

        if (string.IsNullOrWhiteSpace(projectName))
        {
            CreateProjectMessage = "Project name is required.";
            return;
        }

        IsCreateProjectBusy = true;
        CreateProjectMessage = "Creating project...";

        try
        {
            var token = await _app.JwtSessionService.GetTokenAsync();

            if (string.IsNullOrWhiteSpace(token))
            {
                CreateProjectMessage = "Please log in again to create a project.";
                return;
            }

            var result = await _app.ProjectsApiClient.CreateProjectAsync(
                token,
                new CreateProjectDto(projectName, DefaultProjectValue));

            if (!result.IsSuccess)
            {
                CreateProjectMessage = result.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        "Your session expired. Please log in again.",
                    HttpStatusCode.Conflict =>
                        result.ErrorMessage ?? "A project with this name already exists.",
                    _ => result.ErrorMessage ?? "Unable to create the project."
                };
                return;
            }

            IsCreateProjectDialogOpen = false;
            CurrentPage = 1;
            await LoadProjectListAsync();
        }
        catch (HttpRequestException)
        {
            CreateProjectMessage = "Could not reach the backend.";
        }
        catch
        {
            CreateProjectMessage = "Something went wrong while creating the project.";
        }
        finally
        {
            IsCreateProjectBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private async Task PreviousPageAsync()
    {
        if (CurrentPage <= 1)
        {
            return;
        }

        CurrentPage--;
        await LoadProjectListAsync();
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task NextPageAsync()
    {
        if (!HasNextPage)
        {
            return;
        }

        CurrentPage++;
        await LoadProjectListAsync();
    }

    [RelayCommand]
    private void SelectProject(ProjectDto project)
    {
        _app.CurrentProject = project;
        WorkspaceRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task DeleteProjectAsync(ProjectDto project)
    {
        ProjectsStatusText = "Deleting project...";

        try
        {
            var result = await _app.ProjectSessionService.DeleteProjectById(project.Id);
            if (!result.IsSuccess)
            {
                ProjectsStatusText = result.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        result.ErrorMessage ?? "You do not have permission to delete this project.",
                    _ => result.ErrorMessage ?? "Unable to delete the project."
                };
                return;
            }

            await LoadProjectListAsync();
        }
        catch (HttpRequestException)
        {
            ProjectsStatusText = "Could not reach the backend.";
        }
        catch
        {
            ProjectsStatusText = "Something went wrong while deleting the project.";
        }
    }

    public void UpdateProjectsStatusText()
    {
        ProjectsStatusText = CurrentPageProjects.Count == 0
            ? "No projects yet."
            : $"Projects ({VisibleProjects.Count})";

        if (VisibleProjects.Count == 0 && CurrentPageProjects.Count > 0)
        {
            ProjectsStatusText = "No matching projects.";
        }
    }

    public void NotifyProjectsChanged()
    {
        OnPropertyChanged(nameof(VisibleProjects));
        OnPropertyChanged(nameof(CurrentPageDisplay));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }
}
