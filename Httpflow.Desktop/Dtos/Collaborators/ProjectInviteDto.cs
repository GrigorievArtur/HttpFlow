using System;
using System.ComponentModel.DataAnnotations;

namespace Httpflow.Desktop.Dtos.Collaborators;

public record ProjectInviteDto(
    [param: Range(1, int.MaxValue)] int ProjectId,
    [param: Required, StringLength(255)] string ProjectName,
    [param: Required, StringLength(32)] string Role,
    DateTime InvitedAt,
    [param: Required, StringLength(255)] string OwnerFirstname,
    [param: Required, StringLength(255)] string OwnerLastname,
    [param: Required, EmailAddress, StringLength(320)] string OwnerEmail);
