using System;

namespace Httpflow.Desktop.Models;

public sealed class StoredSession
{
    public required string AccessToken { get; init; }

    public required DateTimeOffset ExpiresAtUtc { get; init; }
}
