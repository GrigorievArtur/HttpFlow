using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using System;
using System.IO;
using System.Net.Http;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using DotNetEnv;
using Httpflow.Desktop.Dtos.Projects;
using Httpflow.Desktop.Features.Auth.Views;
using Httpflow.Desktop.Models.Nodes;
using Httpflow.Desktop.Services.Auth;
using Httpflow.Desktop.Services.Collaborators;
using Httpflow.Desktop.Services.Projects;
using Httpflow.Desktop.Services.Settings;
using Httpflow.Desktop.Shell.Views;

namespace Httpflow.Desktop;

public partial class App : Application
{
    private readonly JwtSessionService _jwtSessionService = new();
    private HttpClient? _httpClient;
    private AuthApiClient? _authApiClient;
    private ProjectsApiClient? _projectsApiClient;
    private CollaboratorsApiClient? _collaboratorsApiClient;
    private ProjectSessionService? _projectSession;
    private ProjectTestRunnerService? _projectTestRunner;
    private readonly AppSettingsService _settingsService = new();

    public JwtSessionService JwtSessionService => _jwtSessionService;

    public AuthApiClient AuthApiClient => _authApiClient ??= new AuthApiClient(HttpClient);

    public ProjectsApiClient ProjectsApiClient => _projectsApiClient ??= new ProjectsApiClient(HttpClient);

    public CollaboratorsApiClient CollaboratorsApiClient =>
        _collaboratorsApiClient ??= new CollaboratorsApiClient(HttpClient);

    public ProjectSessionService ProjectSessionService => _projectSession ??= new ProjectSessionService(this);

    public ProjectTestRunnerService ProjectTestRunner =>
        _projectTestRunner ??= new ProjectTestRunnerService(ProjectSessionService);

    public AppThemeMode CurrentThemeMode => RequestedThemeVariant == ThemeVariant.Dark
        ? AppThemeMode.Dark
        : AppThemeMode.Light;

    public ProjectDto? CurrentProject { get; set; }

    public CanvasNodeRecord? SelectedNode { get; set; }

    private HttpClient HttpClient => _httpClient ??= new HttpClient
    {
        BaseAddress = new Uri(Environment.GetEnvironmentVariable("API_HOST") ?? "http://localhost:5157/")
    };

    public override void Initialize()
    {
        var envFilePath = Path.Combine(AppContext.BaseDirectory, ".env");
        if (File.Exists(envFilePath))
        {
            Env.Load(envFilePath);
        }

        AvaloniaXamlLoader.Load(this);
        ApplySavedThemeMode();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var storedSession = _jwtSessionService.GetSessionAsync().GetAwaiter().GetResult();
            desktop.MainWindow = storedSession is not null && !_jwtSessionService.IsExpired(storedSession)
                ? new AppShellWindow()
                : new LoginWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void ShowLoginWindow(Avalonia.Controls.Window? currentWindow = null, string? errorMessage = null)
    {
        SwapMainWindow(new LoginWindow(errorMessage), currentWindow);
    }

    public void ShowRegisterWindow(Avalonia.Controls.Window? currentWindow = null, string? errorMessage = null)
    {
        SwapMainWindow(new RegisterWindow(errorMessage), currentWindow);
    }

    public void ShowMainWindow(Avalonia.Controls.Window? currentWindow = null)
    {
        SwapMainWindow(new AppShellWindow(), currentWindow);
    }

    public void SetThemeMode(AppThemeMode themeMode)
    {
        RequestedThemeVariant = themeMode == AppThemeMode.Dark
            ? ThemeVariant.Dark
            : ThemeVariant.Light;

        _settingsService.SaveThemeMode(themeMode);
    }

    private void SwapMainWindow(Avalonia.Controls.Window nextWindow, Avalonia.Controls.Window? currentWindow)
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        desktop.MainWindow = nextWindow;
        nextWindow.Show();
        currentWindow?.Close();
    }

    private void ApplySavedThemeMode()
    {
        var themeMode = _settingsService.LoadThemeMode();
        if (themeMode is not null)
        {
            SetThemeMode(themeMode.Value);
        }
    }
}
