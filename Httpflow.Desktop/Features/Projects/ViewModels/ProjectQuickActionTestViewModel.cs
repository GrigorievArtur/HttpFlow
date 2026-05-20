using System;
using Httpflow.Desktop.Models.Projects;

namespace Httpflow.Desktop.Features.Projects.ViewModels;

public sealed class ProjectQuickActionTestViewModel
{
    public ProjectQuickActionTestViewModel(ProjectTestState test)
    {
        Id = test.Id;
        Name = string.IsNullOrWhiteSpace(test.Name) ? $"Test {test.Id}" : test.Name;
        Order = test.Order <= 0 ? 1 : test.Order;
        Status = string.IsNullOrWhiteSpace(test.Status) ? "Not started" : test.Status;
        NodeCount = test.Nodes.Count;
    }

    public int Id { get; }

    public string Name { get; }

    public int Order { get; }

    public string Status { get; }

    public int NodeCount { get; }

    public string NodeCountText => NodeCount == 1 ? "1 node" : $"{NodeCount} nodes";

    public bool IsPassed => IsStatus("Passed");

    public bool IsFailed => IsStatus("Failed");

    public bool IsRunning => IsStatus("Running");

    public bool IsWaiting => IsStatus("Waiting");

    public bool IsNeutral => !IsPassed && !IsFailed && !IsRunning && !IsWaiting;

    private bool IsStatus(string status)
    {
        return string.Equals(Status, status, StringComparison.OrdinalIgnoreCase);
    }
}
