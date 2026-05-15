using System.ComponentModel.DataAnnotations;

namespace Httpflow.Desktop.Dtos.Operations;

public record ValueChangeOperation(
    [param: Range(1, int.MaxValue)] int NodeId,
    [param: Range(1, int.MaxValue)] int ValueId,
    [param: Required] string Value);
