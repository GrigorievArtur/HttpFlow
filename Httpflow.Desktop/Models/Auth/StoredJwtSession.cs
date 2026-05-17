using System;

namespace Httpflow.Desktop.Models.Auth;

public sealed class StoredJwtSession
{
    public required string AccessToken { get; init; }

    public required DateTimeOffset ExpiresAtUtc { get; init; }
}
