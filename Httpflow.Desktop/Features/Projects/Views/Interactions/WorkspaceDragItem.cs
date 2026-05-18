namespace Httpflow.Desktop.Features.Projects.Views.Interactions;

public enum WorkspaceDragItemKind
{
    Test,
    Node
}

public readonly record struct WorkspaceDragItem(WorkspaceDragItemKind Kind, int TestId, int ItemId);
