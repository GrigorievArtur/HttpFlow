using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Httpflow.Desktop.Dtos.Projects;

namespace Httpflow.Desktop.Views;

public partial class ProjectsPage : UserControl
{
    private const string DefaultProjectValue = "{}";

    public event EventHandler? WorkspaceRequested;
    public event EventHandler? LogoutRequested;

    public ProjectsPage()
    {
        InitializeComponent();
        Loaded += ProjectsPage_OnLoaded;
    }

    private App CurrentApp => (App)Application.Current!;

    private async void ProjectsPage_OnLoaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= ProjectsPage_OnLoaded;
        await LoadProjectList();
    }

    public async Task LoadProjectList()
    {
        ProjectsStatusTextBlock.Text = "Loading projects...";
        ProjectsListPanel.Children.Clear();

        try
        {
            var token = await CurrentApp.JwtService.GetTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                ProjectsStatusTextBlock.Text = "Please log in again to load projects.";
                return;
            }

            var result = await CurrentApp.ProjectsApiClient.GetProjectsAsync(token);
            if (!result.IsSuccess)
            {
                ProjectsStatusTextBlock.Text = result.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        "Your session expired. Please log in again.",
                    _ => result.ErrorMessage ?? "Unable to load projects."
                };
                return;
            }

            var projects = result.Data ?? [];
            if (projects.Count == 0)
            {
                ProjectsStatusTextBlock.Text = "No projects yet.";
                return;
            }

            ProjectsStatusTextBlock.Text = $"Projects ({projects.Count})";
            foreach (var project in projects)
            {
                ProjectsListPanel.Children.Add(CreateProjectButton(project));
            }
        }
        catch (HttpRequestException)
        {
            ProjectsStatusTextBlock.Text = "Could not reach the backend.";
        }
        catch (Exception)
        {
            ProjectsStatusTextBlock.Text = "Something went wrong while loading projects.";
        }
    }

    public void SetQuickActionsText(string text)
    {
        CurrentUserTextBlock.Text = text;
    }

    private void NewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        CreateProjectMessageTextBlock.IsVisible = false;
        CreateProjectMessageTextBlock.Text = string.Empty;
        ProjectNameTextBox.Text = string.Empty;
        SetCreateProjectDialogOpen(true);
    }

    private void CancelCreateProjectButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SetCreateProjectDialogOpen(false);
    }

    private async void ConfirmCreateProjectButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await CreateProjectAsync();
    }

    private void WorkspaceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        WorkspaceRequested?.Invoke(this, EventArgs.Empty);
    }

    private void LogoutButton_OnClick(object? sender, RoutedEventArgs e)
    {
        LogoutRequested?.Invoke(this, EventArgs.Empty);
    }

    private Button CreateProjectButton(ProjectDto project)
    {
        var button = new Button
        {
            Content = project.Name,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
        };

        button.Click += WorkspaceButton_OnClick;
        return button;
    }

    protected override async void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!CreateProjectOverlay.IsVisible)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            SetCreateProjectDialogOpen(false);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            await CreateProjectAsync();
            e.Handled = true;
        }
    }

    private async Task CreateProjectAsync()
    {
        var projectName = ProjectNameTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(projectName))
        {
            ShowCreateProjectMessage("Project name is required.");
            ProjectNameTextBox.Focus();
            return;
        }

        SetCreateProjectButtonsEnabled(false);
        ShowCreateProjectMessage("Creating project...");

        try
        {
            var token = await CurrentApp.JwtService.GetTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                ShowCreateProjectMessage("Please log in again to create a project.");
                return;
            }

            var result = await CurrentApp.ProjectsApiClient.CreateProjectAsync(
                token,
                new CreateProjectDto(projectName, DefaultProjectValue));

            if (!result.IsSuccess)
            {
                var message = result.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        "Your session expired. Please log in again.",
                    HttpStatusCode.Conflict =>
                        result.ErrorMessage ?? "A project with this name already exists.",
                    _ => result.ErrorMessage ?? "Unable to create the project."
                };

                ShowCreateProjectMessage(message);
                return;
            }

            SetCreateProjectDialogOpen(false);
            await LoadProjectList();
        }
        catch (HttpRequestException)
        {
            ShowCreateProjectMessage("Could not reach the backend.");
        }
        catch (Exception)
        {
            ShowCreateProjectMessage("Something went wrong while creating the project.");
        }
        finally
        {
            SetCreateProjectButtonsEnabled(true);
        }
    }

    private void SetCreateProjectDialogOpen(bool isOpen)
    {
        CreateProjectOverlay.IsVisible = isOpen;

        if (isOpen)
        {
            ProjectNameTextBox.Focus();
        }
    }

    private void SetCreateProjectButtonsEnabled(bool isEnabled)
    {
        CancelCreateProjectButton.IsEnabled = isEnabled;
        ConfirmCreateProjectButton.IsEnabled = isEnabled;
    }

    private void ShowCreateProjectMessage(string message)
    {
        CreateProjectMessageTextBlock.Text = message;
        CreateProjectMessageTextBlock.IsVisible = !string.IsNullOrWhiteSpace(message);
    }
}
