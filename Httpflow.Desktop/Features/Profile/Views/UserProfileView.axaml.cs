using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Httpflow.Desktop.Models.Users;

namespace Httpflow.Desktop.Features.Profile.Views;

public partial class UserProfileView : UserControl
{
    public event EventHandler? LogoutRequested;

    public UserProfileView() : this(null)
    {
    }

    public UserProfileView(UserProfile? user)
    {
        InitializeComponent();
        SetUser(user);
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
}
