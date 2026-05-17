using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Httpflow.Desktop.Features.Collaborators.Views;
using Httpflow.Desktop.Features.Profile.Views;
using Httpflow.Desktop.Features.Projects.Views;
using Httpflow.Desktop.Models.Users;
using Httpflow.Desktop.Shell.Controls;

namespace Httpflow.Desktop.Shell.Views;

public partial class AppShellWindow : Window
{
    private string _projectsQuickActionsText = "Quick actions";
    private UserProfile? _currentUser;

    public AppShellWindow()
    {
        InitializeComponent();
        NavigationBar.ProfileRequested += OnProfileRequested;
        NavigationBar.ProjectsRequested += OnProjectsRequested;
        NavigationBar.DashboardRequested += OnDashboardRequested;
        NavigationBar.WorkspaceRequested += OnWorkspaceRequested;
        Opened += OnOpened;
    }

    private App CurrentApp => (App)Application.Current!;

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (await EnsureAuthenticatedAsync())
        {
            ShowProjectsPage();
        }
    }

    private async void OnWorkspaceRequested(object? sender, EventArgs e)
    {
        await EnsureAuthenticatedAsync();
    }

    private async void OnProfileLogoutRequested(object? sender, EventArgs e)
    {
        await CurrentApp.JwtSessionService.DeleteAsync();
        CurrentApp.ShowLoginWindow(this);
    }

    private void OnProjectsWorkspaceRequested(object? sender, EventArgs e)
    {
        ShowWorkspacePage();
    }

    private void OnProjectsRequested(object? sender, EventArgs e)
    {
        ShowProjectsPage();
    }

    private void OnProfileRequested(object? sender, EventArgs e)
    {
        ShowProfilePage();
    }

    private void OnDashboardRequested(object? sender, EventArgs e)
    {
        ShowDashboardPage();
    }

    private void ShowProjectsPage()
    {
        var projectListView = new ProjectListView(_projectsQuickActionsText);
        projectListView.WorkspaceRequested += OnProjectsWorkspaceRequested;
        MainContent.Content = projectListView;
    }

    private void ShowProfilePage()
    {
        var userProfileView = new UserProfileView(_currentUser);
        userProfileView.LogoutRequested += OnProfileLogoutRequested;
        MainContent.Content = userProfileView;
    }

    private void ShowDashboardPage()
    {
        MainContent.Content = new CollaboratorDashboardView();
    }

    private void ShowWorkspacePage()
    {
        MainContent.Content = new ProjectWorkspaceView();
    }

    private async Task<bool> EnsureAuthenticatedAsync()
    {
        try
        {
            var session = await CurrentApp.JwtSessionService.GetSessionAsync();
            if (session is null)
            {
                CurrentApp.ShowLoginWindow(this, "Please log in to continue.");
                return false;
            }

            if (CurrentApp.JwtSessionService.IsExpired(session))
            {
                await CurrentApp.JwtSessionService.DeleteAsync();
                CurrentApp.ShowLoginWindow(this, "Your session expired. Please log in again.");
                return false;
            }

            var result = await CurrentApp.AuthApiClient.GetCurrentUserAsync(session.AccessToken);
            if (!result.IsSuccess || result.Data is null)
            {
                if (result.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    await CurrentApp.JwtSessionService.DeleteAsync();
                    CurrentApp.ShowLoginWindow(this, "Your session expired. Please log in again.");
                    return false;
                }

                _projectsQuickActionsText = result.ErrorMessage ?? "Quick actions";
                return true;
            }

            SetCurrentUser(result.Data);
            return true;
        }
        catch (HttpRequestException)
        {
            _projectsQuickActionsText = "Quick actions (API unavailable)";
            return true;
        }
        catch (Exception)
        {
            _projectsQuickActionsText = "Quick actions";
            return true;
        }
    }

    private void SetCurrentUser(UserProfile user)
    {
        _currentUser = user;
        _projectsQuickActionsText = $"Quick actions for {user.Firstname} {user.Lastname}";
    }
}
