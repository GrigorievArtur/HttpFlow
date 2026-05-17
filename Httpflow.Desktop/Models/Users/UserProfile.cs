namespace Httpflow.Desktop.Models.Users;

public sealed class UserProfile
{
    public required int Id { get; init; }

    public required string Firstname { get; init; }

    public required string Lastname { get; init; }

    public required string Email { get; init; }
}
