using System.ComponentModel.DataAnnotations;

namespace Httpflow.Desktop.Dtos.Operations;

public record MoveOperation(
    [param: Range(1, int.MaxValue)] int NodeId,
    int X,
    int Y);
