using System;
using Avalonia;
using CommunityToolkit.Mvvm.Input;
using Httpflow.Desktop.Dtos.Operations;
using Httpflow.Desktop.Enums.Nodes;
using Httpflow.Desktop.Services;

namespace Httpflow.Desktop.ViewModels;

public class ProjectWorkspaceViewModel : ViewModelBase
{
    private readonly NodeOperations _nodeOperations = new();
    private Point _mouseProjectionPosition;

    public IRelayCommand AddStartNodeCommand { get; }

    public ProjectWorkspaceViewModel()
    {
        AddStartNodeCommand = new RelayCommand(AddStartNode);
    }

    public void SetMouseProjectionPosition(Point canvasPoint)
    {
        _mouseProjectionPosition = canvasPoint;
    }

    private void AddStartNode()
    {
        _nodeOperations.CreateNode(new NewNodeOperation(
            NodeType.Start.ToString(),
            (int)Math.Round(_mouseProjectionPosition.X),
            (int)Math.Round(_mouseProjectionPosition.Y)));
    }
}
