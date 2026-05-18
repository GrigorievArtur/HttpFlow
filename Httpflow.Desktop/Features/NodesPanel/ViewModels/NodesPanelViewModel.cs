using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Httpflow.Desktop.Features.Projects.ViewModels;
using Httpflow.Desktop.Models.Nodes;
using Httpflow.Desktop.Services.Projects;

namespace Httpflow.Desktop.Features.NodesPanel.ViewModels;

public partial class NodesPanelViewModel : ObservableObject
{
    private readonly RequestNodePanelViewModel _requestNodePanel;
    private readonly ExpectedNodePanelViewModel _expectedNodePanel;
    private readonly TestDetailsPanelViewModel _testDetailsPanel;
    private readonly EmptyNodesPanelViewModel _emptyPanel = new();

    public NodesPanelViewModel(ProjectSessionService projectSessionService)
    {
        _requestNodePanel = new RequestNodePanelViewModel(projectSessionService);
        _expectedNodePanel = new ExpectedNodePanelViewModel(projectSessionService);
        _testDetailsPanel = new TestDetailsPanelViewModel(projectSessionService);

        _requestNodePanel.NodeUpdated += (testId, nodeId) => NodeUpdated?.Invoke(testId, nodeId);
        _requestNodePanel.NodeDeleted += (testId, nodeId) => NodeDeleted?.Invoke(testId, nodeId);
        _expectedNodePanel.NodeUpdated += (testId, nodeId) => NodeUpdated?.Invoke(testId, nodeId);
        _expectedNodePanel.NodeDeleted += (testId, nodeId) => NodeDeleted?.Invoke(testId, nodeId);
        _testDetailsPanel.TestUpdated += testId => TestUpdated?.Invoke(testId);
        _testDetailsPanel.TestDeleted += testId => TestDeleted?.Invoke(testId);
        _testDetailsPanel.TestImported += testId => TestImported?.Invoke(testId);

        ActivePanel = _emptyPanel;
    }

    public event Action<int, int>? NodeUpdated;

    public event Action<int, int>? NodeDeleted;

    public event Action<int>? TestUpdated;

    public event Action<int>? TestDeleted;

    public event Action<int>? TestImported;

    [ObservableProperty]
    private object activePanel;

    public void SetSelectedNode(WorkspaceNodeCardViewModel? node)
    {
        if (node is null)
        {
            ActivePanel = _emptyPanel;
            return;
        }

        if (node.NodeType == NodeTypeNames.Expected)
        {
            _expectedNodePanel.SetSelectedNode(node);
            ActivePanel = _expectedNodePanel;
            return;
        }

        _requestNodePanel.SetSelectedNode(node);
        ActivePanel = _requestNodePanel;
    }

    public void SetSelectedTest(WorkspaceTestColumnViewModel? test)
    {
        if (test is null)
        {
            ActivePanel = _emptyPanel;
            return;
        }

        _testDetailsPanel.SetSelectedTest(test);
        ActivePanel = _testDetailsPanel;
    }
}
