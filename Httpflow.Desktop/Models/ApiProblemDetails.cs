using System.Collections.Generic;

namespace Httpflow.Desktop.Models;

public sealed class ApiProblemDetails
{
    public string? Title { get; init; }

    public string? Detail { get; init; }

    public Dictionary<string, string[]>? Errors { get; init; }
}
