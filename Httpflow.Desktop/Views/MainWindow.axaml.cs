using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Httpflow.Desktop.Models;

namespace Httpflow.Desktop.Views;

public partial class MainWindow : Window
{
    private string _projectsQuickActionsText = "Quick actions";

    public MainWindow()
    {
        InitializeComponent();
        AppNavbar.ProjectsRequested += AppNavbar_OnProjectsRequested;
        AppNavbar.DashboardRequested += AppNavbar_OnDashboardRequested;
        AppNavbar.WorkspaceRequested += AppNavbar_OnWorkspaceRequested;
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

    private async void AppNavbar_OnWorkspaceRequested(object? sender, EventArgs e)
    {
        await EnsureAuthenticatedAsync();
    }
    
    private async void ProjectsPage_OnLogoutRequested(object? sender, EventArgs e)
    {
        await CurrentApp.JwtService.DeleteAsync();
        CurrentApp.ShowLoginWindow(this);
    }

    private void ProjectsPage_OnWorkspaceRequested(object? sender, EventArgs e)
    {
        ShowWorkspacePage();
    }

    private void AppNavbar_OnProjectsRequested(object? sender, EventArgs e)
    {
        ShowProjectsPage();
    }

    private void AppNavbar_OnDashboardRequested(object? sender, EventArgs e)
    {
        ShowDashboardPage();
    }

    private void ShowProjectsPage()
    {
        var projectsPage = new ProjectsPage();
        projectsPage.SetQuickActionsText(_projectsQuickActionsText);
        projectsPage.WorkspaceRequested += ProjectsPage_OnWorkspaceRequested;
        projectsPage.LogoutRequested += ProjectsPage_OnLogoutRequested;
        MainContent.Content = projectsPage;
    }

    private void ShowDashboardPage()
    {
        MainContent.Content = new DashboardPage();
    }

    private void ShowWorkspacePage()
    {
        MainContent.Content = new ProjectWorkspacePage();
    }

    private async Task<bool> EnsureAuthenticatedAsync()
    {
        try
        {
            var session = await CurrentApp.JwtService.GetSessionAsync();
            if (session is null)
            {
                CurrentApp.ShowLoginWindow(this, "Please log in to continue.");
                return false;
            }

            if (CurrentApp.JwtService.IsExpired(session))
            {
                await CurrentApp.JwtService.DeleteAsync();
                CurrentApp.ShowLoginWindow(this, "Your session expired. Please log in again.");
                return false;
            }

            var result = await CurrentApp.AuthApiClient.GetCurrentUserAsync(session.AccessToken);
            if (!result.IsSuccess || result.Data is null)
            {
                if (result.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    await CurrentApp.JwtService.DeleteAsync();
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
        _projectsQuickActionsText = $"Quick actions for {user.Firstname} {user.Lastname}";
    }
}
