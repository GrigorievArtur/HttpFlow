using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Httpflow.Desktop.Models.Nodes;

namespace Httpflow.Desktop.Features.Projects.ViewModels;

public sealed partial class WorkspaceNodeCardViewModel : ObservableObject
{
    public WorkspaceNodeCardViewModel(int testId, CanvasNodeRecord node, bool showConnector)
    {
        TestId = testId;
        Node = node;
        ShowConnector = showConnector;
    }

    public int TestId { get; }

    public CanvasNodeRecord Node { get; }

    public int Id => Node.Id;

    public string Name => Node.Name;

    public string NodeType => Node.NodeType;

    public IReadOnlyList<NodeValueRecord> Values => Node.Values;

    public bool ShowConnector { get; }

    [ObservableProperty]
    private bool isSelected;
}
