namespace Httpflow.Desktop.Features.Projects.Views.Interactions;

public sealed class WorkspaceDragState
{
    public WorkspaceDragItem? ActiveItem { get; private set; }

    public bool WasReordered { get; private set; }

    public void Begin(WorkspaceDragItem item)
    {
        ActiveItem = item;
        WasReordered = false;
    }

    public void MarkReordered()
    {
        WasReordered = true;
    }

    public void End()
    {
        ActiveItem = null;
        WasReordered = false;
    }
}
