using System.ComponentModel.DataAnnotations;

namespace Httpflow.Desktop.Dtos.Collaborators;

public record UpdateProjectCollaboratorRoleDto(
    [param: Required, StringLength(32)] string Role);
