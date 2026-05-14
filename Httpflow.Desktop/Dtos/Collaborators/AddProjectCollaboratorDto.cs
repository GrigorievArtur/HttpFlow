using System.ComponentModel.DataAnnotations;

namespace Httpflow.Desktop.Dtos.Collaborators;

public record AddProjectCollaboratorDto(
    [param: Required, EmailAddress, StringLength(320)] string Email,
    [param: Required, StringLength(32)] string Role);
