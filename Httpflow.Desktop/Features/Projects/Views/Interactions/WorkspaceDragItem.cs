namespace Httpflow.Desktop.Features.Projects.Views.Interactions;

public enum WorkspaceDragItemKind
{
    Test,
    Node
}

public enum WorkspaceDragAxis
{
    Horizontal,
    Vertical
}

public readonly record struct WorkspaceDragItem(WorkspaceDragItemKind Kind, int TestId, int ItemId);
