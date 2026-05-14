using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Httpflow.Desktop.Dtos.Projects;

namespace Httpflow.Desktop.Views;

public partial class ProjectsPage : UserControl
{
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
}
