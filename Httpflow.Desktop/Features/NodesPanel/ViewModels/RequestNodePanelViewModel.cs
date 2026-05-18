using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Httpflow.Desktop.Features.Projects.ViewModels;
using Httpflow.Desktop.Models.Nodes;
using Httpflow.Desktop.Services.Projects;

namespace Httpflow.Desktop.Features.NodesPanel.ViewModels;

public partial class RequestNodePanelViewModel : ObservableObject
{
    private readonly ProjectSessionService _projectSessionService;
    private int? _selectedTestId;
    private int? _selectedNodeId;
    private bool _isLoadingNode;

    public RequestNodePanelViewModel(ProjectSessionService projectSessionService)
    {
        _projectSessionService = projectSessionService;
    }

    public event Action<int, int>? NodeUpdated;

    public event Action<int, int>? NodeDeleted;

    public IReadOnlyList<string> Methods { get; } = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    [ObservableProperty]
    private string requestName = string.Empty;

    [ObservableProperty]
    private string method = "GET";

    [ObservableProperty]
    private string url = string.Empty;

    [ObservableProperty]
    private string body = string.Empty;

    [ObservableProperty]
    private string response = string.Empty;

    [RelayCommand]
    private void DeleteSelectedNode()
    {
        if (_selectedTestId is not { } testId || _selectedNodeId is not { } nodeId)
        {
            return;
        }

        if (_projectSessionService.DeleteNode(testId, nodeId))
        {
            NodeDeleted?.Invoke(testId, nodeId);
        }
    }

    public void SetSelectedNode(WorkspaceNodeCardViewModel node)
    {
        _selectedTestId = node.TestId;
        _selectedNodeId = node.Id;

        _isLoadingNode = true;
        try
        {
            var record = node.Node;
            RequestName = record.Name;
            Method = GetValue(record, "Method", "GET");
            Url = GetValue(record, "Url", string.Empty);
            Body = GetValue(record, "Body", string.Empty);
            Response = GetValue(record, "Response", string.Empty);
        }
        finally
        {
            _isLoadingNode = false;
        }
    }

    partial void OnRequestNameChanged(string value)
    {
        UpdateSelectedNodeName(value);
    }

    partial void OnMethodChanged(string value)
    {
        UpdateSelectedNodeValue("Method", value);
    }

    partial void OnUrlChanged(string value)
    {
        UpdateSelectedNodeValue("Url", value);
    }

    partial void OnBodyChanged(string value)
    {
        UpdateSelectedNodeValue("Body", value);
    }

    partial void OnResponseChanged(string value)
    {
        UpdateSelectedNodeValue("Response", value);
    }

    private void UpdateSelectedNodeName(string value)
    {
        if (_isLoadingNode || _selectedTestId is not { } testId || _selectedNodeId is not { } nodeId)
        {
            return;
        }

        if (_projectSessionService.UpdateNodeName(testId, nodeId, value))
        {
            NodeUpdated?.Invoke(testId, nodeId);
        }
    }

    private void UpdateSelectedNodeValue(string label, string value)
    {
        if (_isLoadingNode || _selectedTestId is not { } testId || _selectedNodeId is not { } nodeId)
        {
            return;
        }

        if (_projectSessionService.UpdateNodeValue(testId, nodeId, label, value))
        {
            NodeUpdated?.Invoke(testId, nodeId);
        }
    }

    private static string GetValue(CanvasNodeRecord record, string label, string fallback)
    {
        foreach (var value in record.Values)
        {
            if (string.Equals(value.Label, label, StringComparison.OrdinalIgnoreCase))
            {
                return value.Value;
            }
        }

        return fallback;
    }
}
