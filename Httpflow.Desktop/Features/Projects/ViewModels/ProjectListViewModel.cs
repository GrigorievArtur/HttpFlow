using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Httpflow.Desktop.Dtos.Projects;
using Httpflow.Desktop.Models.Projects;
using Httpflow.Desktop.Services.Projects;
using Httpflow.Desktop.ViewModels;

namespace Httpflow.Desktop.Features.Projects.ViewModels;

public partial class ProjectListViewModel : ViewModelBase, IDisposable
{
    private const string DefaultProjectValue = "{}";
    private const int PageSize = 10;

    private readonly App _app;

    public ProjectListViewModel(App app, string quickActionsText)
    {
        _app = app;
        QuickActionsText = quickActionsText;
        _app.ProjectTestRunner.ProgressChanged += OnRunProgressChanged;
    }

    public event EventHandler? WorkspaceRequested;

    public List<ProjectDto> CurrentPageProjects { get; } = [];

    public ObservableCollection<ProjectQuickActionTestViewModel> QuickActionTests { get; } = [];

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

    public bool CanRunSelectedProject => IsQuickActionsLoaded && !IsRunInProgress;

    public bool HasQuickActionTests => QuickActionTests.Count > 0;

    public bool IsRunProgressError => IsRunProgressVisible && !IsRunProgressHealthy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleProjects))]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string quickActionsText = "Quick actions";

    [ObservableProperty]
    private int? selectedProjectId;

    [ObservableProperty]
    private string selectedProjectName = "No project selected";

    [ObservableProperty]
    private string quickActionsStatusText = "Select a project to load its quick actions.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRunSelectedProject))]
    [NotifyCanExecuteChangedFor(nameof(RunAllTestsCommand))]
    private bool isQuickActionsLoaded;

    [ObservableProperty]
    private bool isQuickActionsBusy;

    [ObservableProperty]
    private string quickActionTestsStatusText = "Project tests will appear here.";

    [ObservableProperty]
    private string projectsStatusText = "Loading projects...";

    [ObservableProperty]
    private double runProgressMaximum = 1;

    [ObservableProperty]
    private double runProgressValue;

    [ObservableProperty]
    private string runProgressText = "No run yet";

    [ObservableProperty]
    private bool isRunProgressVisible;

    [ObservableProperty]
    private bool isRunProgressHealthy = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRunSelectedProject))]
    [NotifyCanExecuteChangedFor(nameof(RunAllTestsCommand))]
    private bool isRunInProgress;

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

            var result = await _app.ProjectsApiClient.GetProjectsAsync(token, CurrentPage);

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
    private async Task LoadQuickActionsAsync(ProjectDto? project)
    {
        if (project is null)
        {
            return;
        }

        var isNewSelection = SelectedProjectId != project.Id;
        SelectedProjectId = project.Id;
        SelectedProjectName = GetProjectDisplayName(project);
        QuickActionsStatusText = "Loading project quick actions...";
        QuickActionTestsStatusText = "Loading project tests...";
        IsQuickActionsBusy = true;
        IsQuickActionsLoaded = false;

        if (isNewSelection)
        {
            ResetRunProgress();
        }

        try
        {
            _app.CurrentProject = project;
            var session = await _app.ProjectSessionService.LoadProjectById(project.Id);

            if (session is null)
            {
                QuickActionsStatusText = "Unable to load this project.";
                ClearQuickActionTests();
                return;
            }

            SelectedProjectName = string.IsNullOrWhiteSpace(session.Name)
                ? GetProjectDisplayName(project)
                : session.Name;
            QuickActionsStatusText = "Ready";
            IsQuickActionsLoaded = true;
            RefreshQuickActionTests(session);
        }
        catch (HttpRequestException)
        {
            QuickActionsStatusText = "Could not reach the backend.";
            ClearQuickActionTests();
        }
        catch
        {
            QuickActionsStatusText = "Something went wrong while loading the project.";
            ClearQuickActionTests();
        }
        finally
        {
            IsQuickActionsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunSelectedProject))]
    private async Task RunAllTestsAsync()
    {
        if (SelectedProjectId is not { } projectId)
        {
            return;
        }

        IsRunInProgress = true;

        try
        {
            if (_app.ProjectSessionService.CurrentProject?.ProjectId != projectId)
            {
                await _app.ProjectSessionService.LoadProjectById(projectId);
            }

            await _app.ProjectTestRunner.RunCurrentProjectAsync();
            RefreshQuickActionTests(_app.ProjectSessionService.CurrentProject);
        }
        catch (HttpRequestException)
        {
            ApplyRunFailure("Could not reach the backend.");
        }
        catch
        {
            ApplyRunFailure("Something went wrong while running tests.");
        }
        finally
        {
            IsRunInProgress = false;
        }
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

            if (SelectedProjectId == project.Id)
            {
                ClearSelectedQuickActions();
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

    public void Dispose()
    {
        _app.ProjectTestRunner.ProgressChanged -= OnRunProgressChanged;
    }

    partial void OnIsRunProgressHealthyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsRunProgressError));
    }

    partial void OnIsRunProgressVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsRunProgressError));
    }

    private static string GetProjectDisplayName(ProjectDto project)
    {
        return string.IsNullOrWhiteSpace(project.Name) ? $"Project #{project.Id}" : project.Name;
    }

    private void RefreshQuickActionTests(ProjectSessionState? session)
    {
        QuickActionTests.Clear();

        if (session is null)
        {
            QuickActionTestsStatusText = "No project tests loaded.";
            OnPropertyChanged(nameof(HasQuickActionTests));
            return;
        }

        foreach (var test in session.Tests
                     .OrderBy(test => Math.Max(1, test.Order))
                     .ThenBy(test => test.Id))
        {
            QuickActionTests.Add(new ProjectQuickActionTestViewModel(test));
        }

        QuickActionTestsStatusText = QuickActionTests.Count == 0
            ? "No tests in this project."
            : $"Project tests ({QuickActionTests.Count})";
        OnPropertyChanged(nameof(HasQuickActionTests));
    }

    private void ClearQuickActionTests()
    {
        QuickActionTests.Clear();
        QuickActionTestsStatusText = "No project tests loaded.";
        OnPropertyChanged(nameof(HasQuickActionTests));
    }

    private void ResetRunProgress()
    {
        RunProgressMaximum = 1;
        RunProgressValue = 0;
        RunProgressText = "No run yet";
        IsRunProgressVisible = false;
        IsRunProgressHealthy = true;
        IsRunInProgress = false;
    }

    private void ClearSelectedQuickActions()
    {
        SelectedProjectId = null;
        SelectedProjectName = "No project selected";
        QuickActionsStatusText = "Select a project to load its quick actions.";
        IsQuickActionsLoaded = false;
        IsQuickActionsBusy = false;
        ClearQuickActionTests();
        QuickActionTestsStatusText = "Project tests will appear here.";
        ResetRunProgress();
    }

    private void OnRunProgressChanged(ProjectRunProgress progress)
    {
        if (SelectedProjectId is null ||
            _app.ProjectSessionService.CurrentProject?.ProjectId != SelectedProjectId.Value)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            RunProgressMaximum = Math.Max(1, progress.TotalNodes);
            RunProgressValue = progress.CompletedNodes;
            RunProgressText = progress.TotalNodes == 0
                ? progress.Message
                : $"{progress.CompletedNodes}/{progress.TotalNodes} nodes";
            IsRunProgressVisible = progress.IsRunning || progress.TotalNodes > 0;
            IsRunProgressHealthy = !progress.HasError;
            IsRunInProgress = progress.IsRunning;
            RefreshQuickActionTests(_app.ProjectSessionService.CurrentProject);
        });
    }

    private void ApplyRunFailure(string message)
    {
        RunProgressMaximum = 1;
        RunProgressValue = 1;
        RunProgressText = message;
        IsRunProgressVisible = true;
        IsRunProgressHealthy = false;
        QuickActionsStatusText = message;
        RefreshQuickActionTests(_app.ProjectSessionService.CurrentProject);
    }
}
