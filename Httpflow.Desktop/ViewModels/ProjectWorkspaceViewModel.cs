using System;
using Avalonia;
using CommunityToolkit.Mvvm.Input;
using Httpflow.Desktop.Dtos.Operations;
using Httpflow.Desktop.Enums.Nodes;
using Httpflow.Desktop.Models.Nodes;
using Httpflow.Desktop.Services;
using Httpflow.Desktop.Services.Projects;

namespace Httpflow.Desktop.ViewModels;

public class ProjectWorkspaceViewModel : ViewModelBase
{
    private readonly NodeOperations _nodeOperations = new();
    private readonly ProjectSession _projectSession;
    private Point _mouseProjectionPosition;

    public IRelayCommand AddStartNodeCommand { get; }
    public event Action<CanvasNodeRecord>? NodeCreated;

    public ProjectWorkspaceViewModel(ProjectSession projectSession)
    {
        _projectSession = projectSession;
        AddStartNodeCommand = new RelayCommand(AddStartNode);
    }

    public void SetMouseProjectionPosition(Point canvasPoint)
    {
        _mouseProjectionPosition = canvasPoint;
    }

    private void AddStartNode()
    {
        _nodeOperations.SyncNextNodeId(_projectSession.CurrentProject?.Nodes ?? []);

        var nodeRecord = _nodeOperations.CreateNodeRecord(new NewNodeOperation(
            NodeType.Start.ToString(),
            (int)Math.Round(_mouseProjectionPosition.X),
            (int)Math.Round(_mouseProjectionPosition.Y)));

        _projectSession.UpsertNode(nodeRecord);
        NodeCreated?.Invoke(nodeRecord);
    }
}
