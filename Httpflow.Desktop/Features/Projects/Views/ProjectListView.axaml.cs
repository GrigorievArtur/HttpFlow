using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Httpflow.Desktop.Dtos.Projects;
using Httpflow.Desktop.Features.Projects.ViewModels;

namespace Httpflow.Desktop.Features.Projects.Views;

public partial class ProjectListView : UserControl
{
    private readonly ProjectListViewModel _viewModel;

    public event EventHandler? WorkspaceRequested;

    public ProjectListView() : this("Quick actions")
    {
    }

    public ProjectListView(string quickActionsText)
    {
        InitializeComponent();
        _viewModel = new ProjectListViewModel((App)Application.Current!, quickActionsText);
        _viewModel.WorkspaceRequested += (_, _) => WorkspaceRequested?.Invoke(this, EventArgs.Empty);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.LoadProjectListAsync();
        RenderProjectList();
        FocusCreateProjectNameIfNeeded();
    }

    protected override async void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!_viewModel.IsCreateProjectDialogOpen)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            _viewModel.CancelCreateProjectDialogCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            await ExecuteAsync(_viewModel.ConfirmCreateProjectCommand);
            e.Handled = true;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProjectListViewModel.SearchText)
            or nameof(ProjectListViewModel.CurrentPage)
            or nameof(ProjectListViewModel.HasNextPage)
            or nameof(ProjectListViewModel.ProjectsStatusText)
            or nameof(ProjectListViewModel.IsCreateProjectDialogOpen))
        {
            if (e.PropertyName == nameof(ProjectListViewModel.SearchText))
            {
                _viewModel.UpdateProjectsStatusText();
            }

            RenderProjectList();
            FocusCreateProjectNameIfNeeded();
        }
    }

    private void RenderProjectList()
    {
        ProjectsListPanel.Children.Clear();

        foreach (var project in _viewModel.VisibleProjects)
        {
            ProjectsListPanel.Children.Add(CreateProjectRow(project));
        }
    }

    private Control CreateProjectRow(ProjectDto project)
    {
        var openButton = new Button
        {
            Content = project.Name,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Command = _viewModel.SelectProjectCommand,
            CommandParameter = project
        };

        var deleteButton = new Button
        {
            Content = "Delete",
            Foreground = Brushes.Firebrick,
            HorizontalAlignment = HorizontalAlignment.Right,
            Command = _viewModel.DeleteProjectCommand,
            CommandParameter = project
        };

        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
            Children =
            {
                openButton,
                new Border
                {
                    Child = deleteButton,
                    [Grid.ColumnProperty] = 1
                }
            }
        };
    }

    private void FocusCreateProjectNameIfNeeded()
    {
        if (_viewModel.IsCreateProjectDialogOpen)
        {
            ProjectNameTextBox.Focus();
        }
    }

    private static async Task ExecuteAsync(object command)
    {
        if (command is IAsyncRelayCommand asyncCommand)
        {
            await asyncCommand.ExecuteAsync(null);
        }
    }
}
