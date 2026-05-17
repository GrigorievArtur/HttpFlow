using System.ComponentModel.DataAnnotations;

namespace Httpflow.Desktop.Dtos.Nodes;

public record CreateCanvasNodeRequest(
    [param: Required, StringLength(64)] string NodeType,
    int X,
    int Y);
