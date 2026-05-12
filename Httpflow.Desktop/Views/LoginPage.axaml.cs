using System;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Httpflow.Desktop.Models;

namespace Httpflow.Desktop.Views;

public partial class LoginPage : Window
{
    public LoginPage()
        : this(null)
    {
    }

    public LoginPage(string? initialErrorMessage = null)
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(initialErrorMessage))
        {
            ShowError(initialErrorMessage);
        }
    }

    private App CurrentApp => (App)Application.Current!;

    private async void LoginButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await SubmitLoginAsync();
    }

    private void GoToRegisterButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CurrentApp.ShowRegisterWindow(this);
    }

    private async Task SubmitLoginAsync()
    {
        SetBusyState(true);
        HideError();

        try
        {
            var email = EmailTextBox.Text?.Trim() ?? string.Empty;
            var password = PasswordTextBox.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ShowError("Email and password are required.");
                return;
            }

            var result = await CurrentApp.AuthApiClient.LoginAsync(email, password);
            if (!result.IsSuccess || result.Data is null)
            {
                ShowError(result.ErrorMessage ?? "Unable to log in.");
                return;
            }

            await SaveSessionAndOpenMainAsync(result.Data);
        }
        catch (HttpRequestException)
        {
            ShowError("Could not reach the backend. Make sure the API is running on the configured host.");
        }
        catch (Exception)
        {
            ShowError("Something went wrong while logging in.");
        }
        finally
        {
            SetBusyState(false);
        }
    }

    private async Task SaveSessionAndOpenMainAsync(AuthResponse response)
    {
        await CurrentApp.JwtService.SaveAsync(response.AccessToken, response.ExpiresAtUtc);
        CurrentApp.ShowMainWindow(this);
    }

    private void SetBusyState(bool isBusy)
    {
        LoginButton.IsEnabled = !isBusy;
        GoToRegisterButton.IsEnabled = !isBusy;
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.IsVisible = true;
    }

    private void HideError()
    {
        ErrorTextBlock.IsVisible = false;
        ErrorTextBlock.Text = string.Empty;
    }
}
