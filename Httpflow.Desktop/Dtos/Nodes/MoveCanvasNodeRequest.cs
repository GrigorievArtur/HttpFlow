using System.ComponentModel.DataAnnotations;

namespace Httpflow.Desktop.Dtos.Nodes;

public record MoveCanvasNodeRequest(
    [param: Range(1, int.MaxValue)] int NodeId,
    int X,
    int Y);
