using System;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Httpflow.Desktop.Dtos.Nodes;
using Httpflow.Desktop.Enums.Nodes;
using Httpflow.Desktop.Models.Nodes;
using Httpflow.Desktop.Services.Nodes;
using Httpflow.Desktop.Services.Projects;
using Httpflow.Desktop.ViewModels;

namespace Httpflow.Desktop.Features.Projects.ViewModels;

public partial class ProjectWorkspaceViewModel : ViewModelBase
{
    private readonly NodeRecordFactory _nodeRecordFactory = new();
    private readonly ProjectSessionService _projectSessionService;
    private Point _mouseProjectionPosition;

    public event Action<CanvasNodeRecord>? NodeCreated;

    public GridLength SidebarWidth => new(IsSidebarOpen ? 392 : 0);

    public string SidebarToggleText => IsSidebarOpen ? ">>" : "<<";

    public ProjectWorkspaceViewModel(ProjectSessionService projectSessionService)
    {
        _projectSessionService = projectSessionService;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SidebarWidth), nameof(SidebarToggleText))]
    private bool isSidebarOpen = true;

    [ObservableProperty]
    private string projectTitle = "Project";

    [ObservableProperty]
    private string zoomDisplay = "100%";

    public void SetMouseProjectionPosition(Point canvasPoint)
    {
        _mouseProjectionPosition = canvasPoint;
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarOpen = !IsSidebarOpen;
    }

    [RelayCommand]
    private void AddStartNode()
    {
        _nodeRecordFactory.SyncNextNodeId(_projectSessionService.CurrentProject?.Nodes ?? []);

        var nodeRecord = _nodeRecordFactory.CreateNodeRecord(new CreateCanvasNodeRequest(
            NodeType.Start.ToString(),
            (int)Math.Round(_mouseProjectionPosition.X),
            (int)Math.Round(_mouseProjectionPosition.Y)));

        _projectSessionService.UpsertNode(nodeRecord);
        NodeCreated?.Invoke(nodeRecord);
    }
}
