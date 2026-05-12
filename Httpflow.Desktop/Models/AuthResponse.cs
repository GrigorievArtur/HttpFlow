using System;

namespace Httpflow.Desktop.Models;

public sealed class AuthResponse
{
    public required string AccessToken { get; init; }

    public required DateTimeOffset ExpiresAtUtc { get; init; }

    public required UserProfile User { get; init; }
}
