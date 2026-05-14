using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using System;
using System.IO;
using System.Net.Http;
using Avalonia.Markup.Xaml;
using DotNetEnv;
using Httpflow.Desktop.Services;
using Httpflow.Desktop.Views;

namespace Httpflow.Desktop;

public partial class App : Application
{
    private readonly JwtService _jwtService = new();
    private HttpClient? _httpClient;
    private AuthApiClient? _authApiClient;
    private ProjectsApiClient? _projectsApiClient;
    private CollaboratorsApiClient? _collaboratorsApiClient;

    public JwtService JwtService => _jwtService;

    public AuthApiClient AuthApiClient => _authApiClient ??= new AuthApiClient(HttpClient);

    public ProjectsApiClient ProjectsApiClient => _projectsApiClient ??= new ProjectsApiClient(HttpClient);

    public CollaboratorsApiClient CollaboratorsApiClient =>
        _collaboratorsApiClient ??= new CollaboratorsApiClient(HttpClient);

    public int? SelectedProjectId { get; set; }

    public string? SelectedProjectName { get; set; }

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
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var storedSession = _jwtService.GetSessionAsync().GetAwaiter().GetResult();
            desktop.MainWindow = storedSession is not null && !_jwtService.IsExpired(storedSession)
                ? new MainWindow()
                : new LoginPage();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void ShowLoginWindow(Avalonia.Controls.Window? currentWindow = null, string? errorMessage = null)
    {
        SwapMainWindow(new LoginPage(errorMessage), currentWindow);
    }

    public void ShowRegisterWindow(Avalonia.Controls.Window? currentWindow = null, string? errorMessage = null)
    {
        SwapMainWindow(new RegisterPage(errorMessage), currentWindow);
    }

    public void ShowMainWindow(Avalonia.Controls.Window? currentWindow = null)
    {
        SwapMainWindow(new MainWindow(), currentWindow);
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
}
