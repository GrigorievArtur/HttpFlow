using System;

using Httpflow.Desktop.Models.Users;

namespace Httpflow.Desktop.Models.Auth;

public sealed class AuthResponse
{
    public required string AccessToken { get; init; }

    public required DateTimeOffset ExpiresAtUtc { get; init; }

    public required UserProfile User { get; init; }
}
