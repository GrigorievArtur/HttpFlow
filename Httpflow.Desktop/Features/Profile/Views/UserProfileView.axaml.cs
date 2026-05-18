using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Httpflow.Desktop.Models.Users;
using Httpflow.Desktop.Services.Settings;

namespace Httpflow.Desktop.Features.Profile.Views;

public partial class UserProfileView : UserControl
{
    public event EventHandler? LogoutRequested;
    private bool _isInitializingThemeMode;

    public UserProfileView() : this(null)
    {
    }

    public UserProfileView(UserProfile? user)
    {
        InitializeComponent();
        SetUser(user);
        SetThemeModeSelection();
    }

    private void SetUser(UserProfile? user)
    {
        if (user is null)
        {
            ProfileNameTextBlock.Text = "Unknown user";
            ProfileEmailTextBlock.Text = "Unavailable";
            ProfileUserIdTextBlock.Text = "Unavailable";
            return;
        }

        ProfileNameTextBlock.Text = $"{user.Firstname} {user.Lastname}";
        ProfileEmailTextBlock.Text = user.Email;
        ProfileUserIdTextBlock.Text = user.Id.ToString();
    }

    private void OnLogoutButtonClick(object? sender, RoutedEventArgs e)
    {
        LogoutRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SetThemeModeSelection()
    {
        if (Application.Current is not App app)
        {
            return;
        }

        _isInitializingThemeMode = true;
        ThemeModeComboBox.SelectedIndex = app.CurrentThemeMode == AppThemeMode.Dark ? 1 : 0;
        _isInitializingThemeMode = false;
    }

    private void OnThemeModeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializingThemeMode || Application.Current is not App app)
        {
            return;
        }

        app.SetThemeMode(ThemeModeComboBox.SelectedIndex == 1
            ? AppThemeMode.Dark
            : AppThemeMode.Light);
    }
}
