using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Httpflow.Desktop.Dtos.Collaborators;
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
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await LoadInvitesAsync();
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

    private async Task LoadInvitesAsync()
    {
        if (Application.Current is not App app)
        {
            return;
        }

        InvitesStatusTextBlock.Text = "Loading invites...";
        InvitesListPanel.Children.Clear();

        try
        {
            var token = await app.JwtSessionService.GetTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                InvitesStatusTextBlock.Text = "Please log in again to load invites.";
                return;
            }

            var result = await app.CollaboratorsApiClient.GetMyInvitesAsync(token);
            if (!result.IsSuccess)
            {
                InvitesStatusTextBlock.Text = result.ErrorMessage ?? "Unable to load invites.";
                return;
            }

            RenderInvites(result.Data ?? []);
        }
        catch (HttpRequestException)
        {
            InvitesStatusTextBlock.Text = "Could not reach the backend.";
        }
        catch
        {
            InvitesStatusTextBlock.Text = "Something went wrong while loading invites.";
        }
    }

    private void RenderInvites(IReadOnlyCollection<ProjectInviteDto> invites)
    {
        InvitesListPanel.Children.Clear();
        InvitesStatusTextBlock.Text = invites.Count == 0
            ? "No pending invites."
            : $"Pending invites ({invites.Count})";

        foreach (var invite in invites)
        {
            InvitesListPanel.Children.Add(CreateInviteRow(invite));
        }
    }

    private Control CreateInviteRow(ProjectInviteDto invite)
    {
        var acceptButton = new Button
        {
            Content = "Accept",
            CommandParameter = invite,
            MinWidth = 84
        };
        acceptButton.Click += OnAcceptInviteClick;

        var declineButton = new Button
        {
            Content = "Decline",
            CommandParameter = invite,
            MinWidth = 84
        };
        declineButton.Click += OnDeclineInviteClick;

        return new Border
        {
            Padding = new Thickness(12),
            BorderBrush = new SolidColorBrush(Color.Parse("#D9DEE8")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                ColumnSpacing = 12,
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = invite.ProjectName,
                                FontWeight = FontWeight.SemiBold
                            },
                            new TextBlock
                            {
                                Text = $"{invite.OwnerEmail} - {invite.Role}",
                                Opacity = 0.72
                            }
                        }
                    },
                    new StackPanel
                    {
                        [Grid.ColumnProperty] = 1,
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            acceptButton,
                            declineButton
                        }
                    }
                }
            }
        };
    }

    private async void OnAcceptInviteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: ProjectInviteDto invite })
        {
            await RespondToInviteAsync(invite.ProjectId, accept: true);
        }
    }

    private async void OnDeclineInviteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: ProjectInviteDto invite })
        {
            await RespondToInviteAsync(invite.ProjectId, accept: false);
        }
    }

    private async Task RespondToInviteAsync(int projectId, bool accept)
    {
        if (Application.Current is not App app)
        {
            return;
        }

        InvitesStatusTextBlock.Text = accept ? "Accepting invite..." : "Declining invite...";

        try
        {
            var token = await app.JwtSessionService.GetTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                InvitesStatusTextBlock.Text = "Please log in again to update invites.";
                return;
            }

            var errorMessage = accept
                ? (await app.CollaboratorsApiClient.AcceptInviteAsync(token, projectId)).ErrorMessage
                : (await app.CollaboratorsApiClient.DeclineInviteAsync(token, projectId)).ErrorMessage;

            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                InvitesStatusTextBlock.Text = errorMessage;
                return;
            }

            await LoadInvitesAsync();
        }
        catch (HttpRequestException)
        {
            InvitesStatusTextBlock.Text = "Could not reach the backend.";
        }
        catch
        {
            InvitesStatusTextBlock.Text = "Something went wrong while updating invite.";
        }
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
