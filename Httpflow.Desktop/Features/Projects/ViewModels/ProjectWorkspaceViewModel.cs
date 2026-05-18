using Avalonia;
using Avalonia.Controls;
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
    private readonly ProjectSessionService _projectSessionService;

    public ProjectWorkspaceViewModel(ProjectSessionService projectSessionService)
    {
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
        var node = _projectSessionService.AddNode(testId);
        LoadSession(_projectSessionService.CurrentProject);
        SelectNode(testId, node.Id);
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

        if (!_projectSessionService.DeleteNode(node.TestId, node.Id))
        {
            return;
        }

        LoadSession(_projectSessionService.CurrentProject);
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

        OnPropertyChanged(nameof(SelectedTestId));
        OnPropertyChanged(nameof(SelectedNode));
    }

    public void DeleteSelectedTest()
    {
        if (SelectedNode is not null)
        {
            DeleteNode(SelectedNode);
            return;
        }

        if (SelectedTestId is not { } testId)
        {
            return;
        }

        if (!_projectSessionService.DeleteTest(testId))
        {
            return;
        }

        LoadSession(_projectSessionService.CurrentProject);

        if (Tests.Count > 0)
        {
            SelectTest(Tests[0].Id);
        }
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
}
