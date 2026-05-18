using Avalonia;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Httpflow.Desktop.Models.Nodes;
using Httpflow.Desktop.Models.Projects;
using Httpflow.Desktop.Services.Projects;
using Httpflow.Desktop.ViewModels;

namespace Httpflow.Desktop.Features.Projects.ViewModels;

public partial class ProjectWorkspaceViewModel : ViewModelBase
{
    private readonly App _app;
    private readonly ProjectSessionService _projectSessionService;

    public ProjectWorkspaceViewModel(App app, ProjectSessionService projectSessionService)
    {
        _app = app;
        _projectSessionService = projectSessionService;
    }

    public GridLength SidebarWidth => new(IsSidebarOpen ? 392 : 0);

    public string SidebarToggleText => IsSidebarOpen ? ">>" : "<<";

    public ObservableCollection<WorkspaceTestColumnViewModel> Tests { get; } = [];

    public int? SelectedTestId => Tests.FirstOrDefault(test => test.IsSelected)?.Id;

    public WorkspaceNodeCardViewModel? SelectedNode =>
        Tests.SelectMany(test => test.Nodes).FirstOrDefault(node => node.IsSelected);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SidebarWidth), nameof(SidebarToggleText))]
    private bool isSidebarOpen = true;

    [ObservableProperty]
    private string projectTitle = "Project";

    [ObservableProperty]
    private string zoomDisplay = "100%";

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarOpen = !IsSidebarOpen;
    }

    [RelayCommand]
    private void AddTest()
    {
        var test = _projectSessionService.AddTest();
        LoadSession(_projectSessionService.CurrentProject);
        SelectTest(test.Id);
    }

    [RelayCommand]
    private void AddNode(int testId)
    {
        AddNodeToTest(testId);
    }

    [RelayCommand]
    private void DeleteTest(int testId)
    {
        if (!_projectSessionService.DeleteTest(testId))
        {
            return;
        }

        LoadSession(_projectSessionService.CurrentProject);
    }

    [RelayCommand]
    private void DeleteNode(WorkspaceNodeCardViewModel? node)
    {
        if (node is null)
        {
            return;
        }

        var nextSelection = GetNodeSelectionBefore(node.TestId, node.Id);
        if (!_projectSessionService.DeleteNode(node.TestId, node.Id))
        {
            return;
        }

        LoadSession(_projectSessionService.CurrentProject);
        if (nextSelection is { } nextNode)
        {
            SelectNode(nextNode.TestId, nextNode.NodeId);
            return;
        }

        SelectTest(node.TestId);
    }

    public void SelectTest(int testId)
    {
        foreach (var test in Tests)
        {
            test.IsSelected = test.Id == testId;
            foreach (var node in test.Nodes)
            {
                node.IsSelected = false;
            }
        }

        _app.SelectedNode = null;
        OnPropertyChanged(nameof(SelectedTestId));
        OnPropertyChanged(nameof(SelectedNode));
    }

    public void SelectNode(int testId, int nodeId)
    {
        foreach (var test in Tests)
        {
            test.IsSelected = test.Id == testId;
            foreach (var node in test.Nodes)
            {
                node.IsSelected = test.Id == testId && node.Id == nodeId;
            }
        }

        _app.SelectedNode = SelectedNode?.Node;
        OnPropertyChanged(nameof(SelectedTestId));
        OnPropertyChanged(nameof(SelectedNode));
    }

    public void DeleteSelectedNode()
    {
        if (SelectedNode is not null)
        {
            DeleteNode(SelectedNode);
        }
    }

    public WorkspaceNodeCardViewModel? AddNodeToActiveSelection()
    {
        var testId = SelectedNode?.TestId ?? SelectedTestId;
        return testId is null ? null : AddNodeToTest(testId.Value);
    }

    public WorkspaceNodeCardViewModel? AddNodeToTest(int testId)
    {
        var node = _projectSessionService.AddNode(testId);
        LoadSession(_projectSessionService.CurrentProject);
        SelectNode(testId, node.Id);
        return SelectedNode;
    }

    public void SelectAdjacentNode(int direction)
    {
        var selectedNode = SelectedNode;
        if (selectedNode is null)
        {
            if (SelectedTestId is { } selectedTestId)
            {
                var selectedTest = Tests.FirstOrDefault(test => test.Id == selectedTestId);
                var firstNode = direction >= 0 ? selectedTest?.Nodes.FirstOrDefault() : selectedTest?.Nodes.LastOrDefault();
                if (firstNode is not null)
                {
                    SelectNode(firstNode.TestId, firstNode.Id);
                }
            }

            return;
        }

        var test = Tests.FirstOrDefault(item => item.Id == selectedNode.TestId);
        if (test is null)
        {
            return;
        }

        var index = test.Nodes.ToList().FindIndex(node => node.Id == selectedNode.Id);
        if (index < 0)
        {
            return;
        }

        var nextIndex = index + Math.Sign(direction);
        if (nextIndex < 0 || nextIndex >= test.Nodes.Count)
        {
            return;
        }

        var nextNode = test.Nodes[nextIndex];
        SelectNode(nextNode.TestId, nextNode.Id);
    }

    public void MoveTest(int sourceTestId, int targetTestId)
    {
        if (sourceTestId == targetTestId)
        {
            return;
        }

        if (!_projectSessionService.MoveTest(sourceTestId, targetTestId))
        {
            return;
        }

        LoadSession(_projectSessionService.CurrentProject);
        SelectTest(sourceTestId);
    }

    public void MoveNode(int testId, int sourceNodeId, int targetNodeId)
    {
        if (sourceNodeId == targetNodeId)
        {
            return;
        }

        if (!_projectSessionService.MoveNode(testId, sourceNodeId, targetNodeId))
        {
            return;
        }

        LoadSession(_projectSessionService.CurrentProject);
        SelectNode(testId, sourceNodeId);
    }

    public void LoadSession(ProjectSessionState? session)
    {
        var selectedId = SelectedTestId;
        (int TestId, int NodeId)? selectedNode = SelectedNode is null
            ? null
            : (SelectedNode.TestId, SelectedNode.Id);
        Tests.Clear();

        if (session is null)
        {
            return;
        }

        foreach (var test in session.Tests)
        {
            Tests.Add(new WorkspaceTestColumnViewModel(
                test.Id,
                string.IsNullOrWhiteSpace(test.Name) ? $"Test {test.Id}" : test.Name,
                BuildNodeViewModels(test.Id, test.Nodes)));
        }

        if (selectedNode is not null &&
            Tests.SelectMany(test => test.Nodes).Any(node => node.TestId == selectedNode.Value.TestId && node.Id == selectedNode.Value.NodeId))
        {
            SelectNode(selectedNode.Value.TestId, selectedNode.Value.NodeId);
        }
        else if (selectedId is not null && Tests.Any(test => test.Id == selectedId))
        {
            SelectTest(selectedId.Value);
        }
        else if (Tests.Count > 0)
        {
            SelectTest(Tests[0].Id);
        }
        else
        {
            _app.SelectedNode = null;
        }
    }

    private static IEnumerable<WorkspaceNodeCardViewModel> BuildNodeViewModels(int testId, IEnumerable<CanvasNodeRecord> nodes)
    {
        var orderedNodes = nodes.ToList();
        for (var index = 0; index < orderedNodes.Count; index++)
        {
            yield return new WorkspaceNodeCardViewModel(
                testId,
                orderedNodes[index],
                showConnector: index < orderedNodes.Count - 1);
        }
    }

    private (int TestId, int NodeId)? GetNodeSelectionBefore(int testId, int nodeId)
    {
        var test = Tests.FirstOrDefault(item => item.Id == testId);
        if (test is null)
        {
            return null;
        }

        var index = test.Nodes.ToList().FindIndex(node => node.Id == nodeId);
        if (index > 0)
        {
            return (testId, test.Nodes[index - 1].Id);
        }

        if (index == 0 && test.Nodes.Count > 1)
        {
            return (testId, test.Nodes[1].Id);
        }

        return null;
    }
}
