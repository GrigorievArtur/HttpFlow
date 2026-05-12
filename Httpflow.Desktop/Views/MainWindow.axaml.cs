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
    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private App CurrentApp => (App)Application.Current!;

    private async void OnOpened(object? sender, EventArgs e)
    {
        await EnsureAuthenticatedAsync();
    }

    private async void LogoutButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await CurrentApp.JwtService.DeleteAsync();
        CurrentApp.ShowLoginWindow(this);
    }

    private void ProjectButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        MainContent.Content = new ProjectWorkspacePage();
    }

    private async Task EnsureAuthenticatedAsync()
    {
        try
        {
            var session = await CurrentApp.JwtService.GetSessionAsync();
            if (session is null)
            {
                CurrentApp.ShowLoginWindow(this, "Please log in to continue.");
                return;
            }

            if (CurrentApp.JwtService.IsExpired(session))
            {
                await CurrentApp.JwtService.DeleteAsync();
                CurrentApp.ShowLoginWindow(this, "Your session expired. Please log in again.");
                return;
            }

            var result = await CurrentApp.AuthApiClient.GetCurrentUserAsync(session.AccessToken);
            if (!result.IsSuccess || result.Data is null)
            {
                if (result.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    await CurrentApp.JwtService.DeleteAsync();
                    CurrentApp.ShowLoginWindow(this, "Your session expired. Please log in again.");
                    return;
                }

                CurrentUserTextBlock.Text = result.ErrorMessage ?? "Quick actions";
                return;
            }

            SetCurrentUser(result.Data);
        }
        catch (HttpRequestException)
        {
            CurrentUserTextBlock.Text = "Quick actions (API unavailable)";
        }
        catch (Exception)
        {
            CurrentUserTextBlock.Text = "Quick actions";
        }
    }

    private void SetCurrentUser(UserProfile user)
    {
        CurrentUserTextBlock.Text = $"Quick actions for {user.Firstname} {user.Lastname}";
    }
}
