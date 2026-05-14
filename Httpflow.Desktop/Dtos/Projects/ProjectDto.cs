using System.ComponentModel.DataAnnotations;

namespace Httpflow.Desktop.Dtos.Projects;

public record ProjectDto(
    [param: Range(1, int.MaxValue)] int Id,
    [param: Range(1, int.MaxValue)] int OwnerUserId,
    [param: Required, StringLength(255)] string Name,
    [param: Required] string Value);
