using System;
using Avalonia;

namespace Httpflow.Desktop.Features.Projects.Views.Interactions;

public sealed class WorkspaceDragState
{
    public WorkspaceDragItem? ActiveItem { get; private set; }

    public bool WasReordered { get; private set; }

    public bool IsDragReady { get; private set; }

    private Point StartPoint { get; set; }

    public void Begin(WorkspaceDragItem item, Point startPoint)
    {
        ActiveItem = item;
        WasReordered = false;
        IsDragReady = false;
        StartPoint = startPoint;
    }

    public bool TryActivate(Point currentPoint, WorkspaceDragAxis axis)
    {
        if (IsDragReady)
        {
            return true;
        }

        var horizontalDelta = Math.Abs(currentPoint.X - StartPoint.X);
        var verticalDelta = Math.Abs(currentPoint.Y - StartPoint.Y);
        const double activationThreshold = 8;

        IsDragReady = axis == WorkspaceDragAxis.Horizontal
            ? horizontalDelta >= activationThreshold && horizontalDelta >= verticalDelta
            : verticalDelta >= activationThreshold && verticalDelta >= horizontalDelta;

        return IsDragReady;
    }

    public void MarkReordered()
    {
        WasReordered = true;
    }

    public void End()
    {
        ActiveItem = null;
        WasReordered = false;
        IsDragReady = false;
    }
}
