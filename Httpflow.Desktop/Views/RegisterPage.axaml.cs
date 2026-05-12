using System;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Httpflow.Desktop.Models;

namespace Httpflow.Desktop.Views;

public partial class RegisterPage : Window
{
    public RegisterPage()
        : this(null)
    {
    }

    public RegisterPage(string? initialErrorMessage = null)
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(initialErrorMessage))
        {
            ShowError(initialErrorMessage);
        }
    }

    private App CurrentApp => (App)Application.Current!;

    private async void RegisterButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await SubmitRegisterAsync();
    }

    private void GoToLoginButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CurrentApp.ShowLoginWindow(this);
    }

    private async Task SubmitRegisterAsync()
    {
        SetBusyState(true);
        HideError();

        try
        {
            var firstName = FirstNameTextBox.Text?.Trim() ?? string.Empty;
            var lastName = LastNameTextBox.Text?.Trim() ?? string.Empty;
            var email = EmailTextBox.Text?.Trim() ?? string.Empty;
            var password = PasswordTextBox.Text ?? string.Empty;
            var confirmPassword = ConfirmPasswordTextBox.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(firstName) ||
                string.IsNullOrWhiteSpace(lastName) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                ShowError("All fields are required.");
                return;
            }

            if (password != confirmPassword)
            {
                ShowError("Passwords do not match.");
                return;
            }

            var result = await CurrentApp.AuthApiClient.RegisterAsync(firstName, lastName, email, password);
            if (!result.IsSuccess || result.Data is null)
            {
                ShowError(result.ErrorMessage ?? "Unable to register.");
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
            ShowError("Something went wrong while creating the account.");
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
        RegisterButton.IsEnabled = !isBusy;
        GoToLoginButton.IsEnabled = !isBusy;
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
