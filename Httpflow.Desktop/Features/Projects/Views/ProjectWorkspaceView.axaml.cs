using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Httpflow.Desktop.Features.Projects.ViewModels;
using Httpflow.Desktop.Features.Projects.Views.Interactions;
using Httpflow.Desktop.Services.Projects;

namespace Httpflow.Desktop.Features.Projects.Views;

public partial class ProjectWorkspaceView : UserControl
{
    private readonly ProjectSessionService _projectSessionService;
    private readonly ProjectWorkspaceViewModel _viewModel;
    private readonly WorkspaceDragState _dragState = new();

    public ProjectWorkspaceView()
    {
        InitializeComponent();
        _projectSessionService = ((App)Application.Current!).ProjectSessionService;
        _viewModel = new ProjectWorkspaceViewModel(_projectSessionService);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        DataContext = _viewModel;
        Loaded += OnLoaded;

        ApplySidebarState();
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (((App)Application.Current!).CurrentProject is not { } currentProject)
        {
            return;
        }

        await _projectSessionService.LoadProjectById(currentProject.Id);
        _viewModel.ProjectTitle = string.IsNullOrWhiteSpace(currentProject.Name)
            ? $"Project #{currentProject.Id}"
            : currentProject.Name;
        _viewModel.LoadSession(_projectSessionService.CurrentProject);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProjectWorkspaceViewModel.IsSidebarOpen))
        {
            ApplySidebarState();
        }
    }

    private void ApplySidebarState()
    {
        WorkspaceLayout.ColumnDefinitions[2].Width = _viewModel.SidebarWidth;
    }

    private void OnTestGripPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (TryGetTest(sender, out var test))
        {
            _dragState.Begin(new WorkspaceDragItem(WorkspaceDragItemKind.Test, test.Id, test.Id));
            _viewModel.SelectTest(test.Id);
            Focus();
        }

        if (sender is IInputElement inputElement)
        {
            e.Pointer.Capture(inputElement);
        }

        e.Handled = true;
    }

    private void OnTestGripPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is IInputElement inputElement)
        {
            e.Pointer.Capture(null);
        }

        if (!_dragState.WasReordered && TryGetTest(sender, out var test))
        {
            _viewModel.SelectTest(test.Id);
            Focus();
        }

        _dragState.End();
        e.Handled = true;
    }

    private void OnTestColumnPointerEnter(object? sender, PointerEventArgs e)
    {
        if (_dragState.ActiveItem is not { Kind: WorkspaceDragItemKind.Test } dragItem ||
            !TryGetTest(sender, out var targetTest))
        {
            return;
        }

        if (dragItem.ItemId == targetTest.Id)
        {
            return;
        }

        _viewModel.MoveTest(dragItem.ItemId, targetTest.Id);
        _dragState.Begin(dragItem);
        _dragState.MarkReordered();
        e.Handled = true;
    }

    private void OnNodeGripPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (TryGetNode(sender, out var node))
        {
            _dragState.Begin(new WorkspaceDragItem(WorkspaceDragItemKind.Node, node.TestId, node.Id));
            _viewModel.SelectNode(node.TestId, node.Id);
            Focus();
        }

        if (sender is IInputElement inputElement)
        {
            e.Pointer.Capture(inputElement);
        }

        e.Handled = true;
    }

    private void OnNodeGripPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is IInputElement inputElement)
        {
            e.Pointer.Capture(null);
        }

        if (!_dragState.WasReordered && TryGetNode(sender, out var node))
        {
            _viewModel.SelectNode(node.TestId, node.Id);
            Focus();
        }

        _dragState.End();
        e.Handled = true;
    }

    private void OnNodeCardPointerEnter(object? sender, PointerEventArgs e)
    {
        if (_dragState.ActiveItem is not { Kind: WorkspaceDragItemKind.Node } dragItem ||
            !TryGetNode(sender, out var targetNode))
        {
            return;
        }

        if (dragItem.TestId != targetNode.TestId || dragItem.ItemId == targetNode.Id)
        {
            return;
        }

        _viewModel.MoveNode(dragItem.TestId, dragItem.ItemId, targetNode.Id);
        _dragState.Begin(dragItem);
        _dragState.MarkReordered();
        e.Handled = true;
    }

    private void OnWorkspaceKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete && e.Key != Key.Back)
        {
            return;
        }

        _viewModel.DeleteSelectedTest();
        e.Handled = true;
    }

    private static bool TryGetTest(object? sender, out WorkspaceTestColumnViewModel test)
    {
        if (sender is Control control && control.Tag is WorkspaceTestColumnViewModel viewModel)
        {
            test = viewModel;
            return true;
        }

        test = null!;
        return false;
    }

    private static bool TryGetNode(object? sender, out WorkspaceNodeCardViewModel node)
    {
        if (sender is Control control && control.Tag is WorkspaceNodeCardViewModel viewModel)
        {
            node = viewModel;
            return true;
        }

        node = null!;
        return false;
    }
}
