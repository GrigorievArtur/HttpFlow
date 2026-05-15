using System.ComponentModel.DataAnnotations;

namespace Httpflow.Desktop.Dtos.Operations;

public record NewNodeOperation(
    [param: Required, StringLength(64)] string NodeType,
    int X,
    int Y);
