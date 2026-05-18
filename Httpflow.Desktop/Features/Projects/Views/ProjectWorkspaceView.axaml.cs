using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
        var app = (App)Application.Current!;
        _projectSessionService = app.ProjectSessionService;
        _viewModel = new ProjectWorkspaceViewModel(app, _projectSessionService);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        DataContext = _viewModel;
        Loaded += OnLoaded;

        ApplySidebarState();
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        await ReloadProjectAsync();
    }

    public async Task ReloadProjectAsync()
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

    public void ReloadFromSession()
    {
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

        e.Handled = true;
    }

    private void OnTestColumnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsFromButton(e.Source))
        {
            return;
        }

        if (TryGetTest(sender, out var test))
        {
            _dragState.Begin(new WorkspaceDragItem(WorkspaceDragItemKind.Test, test.Id, test.Id));
            _viewModel.SelectTest(test.Id);
            Focus();
            e.Handled = true;
        }
    }

    private void OnTestColumnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragState.ActiveItem is { Kind: WorkspaceDragItemKind.Test })
        {
            _dragState.End();
            e.Handled = true;
        }
    }

    private void OnTestGripPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
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

    private void OnWorkspacePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragState.ActiveItem is not { } dragItem)
        {
            return;
        }

        var pointerPosition = e.GetPosition(this);
        if (dragItem.Kind == WorkspaceDragItemKind.Test)
        {
            MoveTestUnderPointer(dragItem, pointerPosition);
            e.Handled = true;
            return;
        }

        MoveNodeUnderPointer(dragItem, pointerPosition);
        e.Handled = true;
    }

    private void OnWorkspacePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragState.End();
    }

    private void OnNodeGripPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (TryGetNode(sender, out var node))
        {
            _dragState.Begin(new WorkspaceDragItem(WorkspaceDragItemKind.Node, node.TestId, node.Id));
            _viewModel.SelectNode(node.TestId, node.Id);
            Focus();
        }

        e.Handled = true;
    }

    private void OnNodeCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsFromButton(e.Source))
        {
            return;
        }

        if (TryGetNode(sender, out var node))
        {
            _viewModel.SelectNode(node.TestId, node.Id);
            Focus();
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _dragState.Begin(new WorkspaceDragItem(WorkspaceDragItemKind.Node, node.TestId, node.Id));
                e.Handled = true;
            }
        }
    }

    private void OnNodeCardPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragState.ActiveItem is { Kind: WorkspaceDragItemKind.Node })
        {
            _dragState.End();
            e.Handled = true;
        }
    }

    private void OnNodeGripPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
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

    private async void OnWorkspaceKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Key.C:
                    if (_viewModel.SelectedNode is { } nodeToCopy)
                    {
                        _viewModel.CopyNode(nodeToCopy);
                        e.Handled = true;
                    }
                    return;

                case Key.X:
                    if (_viewModel.SelectedNode is { } nodeToCut)
                    {
                        _viewModel.CutNode(nodeToCut);
                        ScrollSelectedNodeIntoView();
                        e.Handled = true;
                    }
                    return;

                case Key.V:
                    if (_viewModel.PasteNodeAfter() is not null)
                    {
                        ScrollSelectedNodeIntoView();
                        e.Handled = true;
                    }
                    return;

                case Key.D:
                    if (_viewModel.SelectedNode is { } nodeToDuplicate &&
                        _viewModel.DuplicateNode(nodeToDuplicate) is not null)
                    {
                        ScrollSelectedNodeIntoView();
                        e.Handled = true;
                    }
                    return;
            }
        }

        switch (e.Key)
        {
            case Key.Space:
                if (await PickNodeTypeAsync() is { } activeNodeType &&
                    _viewModel.AddNodeToActiveSelection(activeNodeType) is not null)
                {
                    ScrollSelectedNodeIntoView();
                    e.Handled = true;
                }
                return;

            case Key.Up:
                _viewModel.SelectAdjacentNode(-1);
                ScrollSelectedNodeIntoView();
                e.Handled = true;
                return;

            case Key.Down:
                _viewModel.SelectAdjacentNode(1);
                ScrollSelectedNodeIntoView();
                e.Handled = true;
                return;

            case Key.Delete:
            case Key.Back:
                _viewModel.DeleteSelectedNode();
                ScrollSelectedNodeIntoView();
                e.Handled = true;
                return;
        }
    }

    private async void OnAddNodeButtonClick(object? sender, RoutedEventArgs e)
    {
        if (TryGetTest(sender, out var test) &&
            await PickNodeTypeAsync() is { } nodeType &&
            _viewModel.AddNodeToTest(test.Id, nodeType) is not null)
        {
            Focus();
            ScrollSelectedNodeIntoView();
        }

        e.Handled = true;
    }

    private void OnCopyNodeMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (TryGetNode(sender, out var node))
        {
            _viewModel.CopyNode(node);
            Focus();
        }
    }

    private void OnCutNodeMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (TryGetNode(sender, out var node))
        {
            _viewModel.CutNode(node);
            Focus();
            ScrollSelectedNodeIntoView();
        }
    }

    private void OnPasteNodeMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (TryGetNode(sender, out var node) && _viewModel.PasteNodeAfter(node) is not null)
        {
            Focus();
            ScrollSelectedNodeIntoView();
        }
    }

    private void OnDuplicateNodeMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (TryGetNode(sender, out var node) && _viewModel.DuplicateNode(node) is not null)
        {
            Focus();
            ScrollSelectedNodeIntoView();
        }
    }

    private async void OnInsertNodeBeforeMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (TryGetNode(sender, out var node) &&
            await PickNodeTypeAsync() is { } nodeType &&
            _viewModel.InsertNodeBefore(node, nodeType) is not null)
        {
            Focus();
            ScrollSelectedNodeIntoView();
        }
    }

    private async void OnInsertNodeAfterMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (TryGetNode(sender, out var node) &&
            await PickNodeTypeAsync() is { } nodeType &&
            _viewModel.InsertNodeAfter(node, nodeType) is not null)
        {
            Focus();
            ScrollSelectedNodeIntoView();
        }
    }

    private async Task<string?> PickNodeTypeAsync()
    {
        var picker = new NodePickerWindow();
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return null;
        }

        var accepted = await picker.ShowDialog<bool?>(owner);

        return accepted == true ? picker.SelectedNodeType : null;
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

        if (sender is MenuItem menuItem)
        {
            var contextMenu = menuItem
                .GetLogicalAncestors()
                .OfType<ContextMenu>()
                .FirstOrDefault();

            if (contextMenu?.PlacementTarget is Control { Tag: WorkspaceNodeCardViewModel placementNode })
            {
                node = placementNode;
                return true;
            }
        }

        node = null!;
        return false;
    }

    private static bool IsFromButton(object? source)
    {
        if (source is not Visual visual)
        {
            return false;
        }

        return visual.GetSelfAndVisualAncestors().OfType<Button>().Any();
    }

    private void MoveTestUnderPointer(WorkspaceDragItem dragItem, Point pointerPosition)
    {
        var targetTest = FindTaggedControlAt<WorkspaceTestColumnViewModel>(pointerPosition)?.Tag as WorkspaceTestColumnViewModel;
        if (targetTest is null || targetTest.Id == dragItem.ItemId)
        {
            return;
        }

        _viewModel.MoveTest(dragItem.ItemId, targetTest.Id);
        _dragState.Begin(dragItem);
        _dragState.MarkReordered();
    }

    private void MoveNodeUnderPointer(WorkspaceDragItem dragItem, Point pointerPosition)
    {
        var targetNode = FindTaggedControlAt<WorkspaceNodeCardViewModel>(pointerPosition)?.Tag as WorkspaceNodeCardViewModel;
        if (targetNode is null || targetNode.TestId != dragItem.TestId || targetNode.Id == dragItem.ItemId)
        {
            return;
        }

        _viewModel.MoveNode(dragItem.TestId, dragItem.ItemId, targetNode.Id);
        _dragState.Begin(dragItem);
        _dragState.MarkReordered();
        ScrollSelectedNodeIntoView();
    }

    private Control? FindTaggedControlAt<TTag>(Point rootPoint)
    {
        return this.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control.Tag is TTag && ContainsRootPoint(control, rootPoint))
            .OrderBy(control => control.Bounds.Width * control.Bounds.Height)
            .FirstOrDefault();
    }

    private bool ContainsRootPoint(Control control, Point rootPoint)
    {
        var origin = control.TranslatePoint(new Point(0, 0), this);
        if (origin is null)
        {
            return false;
        }

        return new Rect(origin.Value, control.Bounds.Size).Contains(rootPoint);
    }

    private void ScrollSelectedNodeIntoView()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var selectedNode = _viewModel.SelectedNode;
            if (selectedNode is null)
            {
                return;
            }

            var selectedNodeControl = this.GetVisualDescendants()
                .OfType<Control>()
                .FirstOrDefault(control => ReferenceEquals(control.Tag, selectedNode));
            selectedNodeControl?.BringIntoView();
        }, DispatcherPriority.Loaded);
    }
}
