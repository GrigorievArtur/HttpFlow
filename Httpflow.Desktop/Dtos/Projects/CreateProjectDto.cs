using System.ComponentModel.DataAnnotations;

namespace Httpflow.Desktop.Dtos.Projects;

public record CreateProjectDto(
    [param: Required, StringLength(255)] string Name,
    [param: Required] string Value);
