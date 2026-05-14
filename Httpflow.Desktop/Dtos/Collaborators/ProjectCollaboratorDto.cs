using System.ComponentModel.DataAnnotations;

namespace Httpflow.Desktop.Dtos.Collaborators;

public record ProjectCollaboratorDto(
    [param: Range(1, int.MaxValue)] int UserId,
    [param: Required, StringLength(255)] string Firstname,
    [param: Required, StringLength(255)] string Lastname,
    [param: Required, EmailAddress, StringLength(320)] string Email,
    [param: Required, StringLength(32)] string Role,
    bool IsOwner);
