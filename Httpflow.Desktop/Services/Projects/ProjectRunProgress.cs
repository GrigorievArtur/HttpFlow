namespace Httpflow.Desktop.Services.Projects;

public sealed record ProjectRunProgress(
    int CompletedNodes,
    int TotalNodes,
    bool HasError,
    bool IsRunning,
    string Message);
