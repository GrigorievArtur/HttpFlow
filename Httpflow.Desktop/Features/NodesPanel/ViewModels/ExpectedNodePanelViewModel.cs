using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Httpflow.Desktop.Features.Projects.ViewModels;
using Httpflow.Desktop.Models.Nodes;
using Httpflow.Desktop.Services.Projects;

namespace Httpflow.Desktop.Features.NodesPanel.ViewModels;

public partial class ExpectedNodePanelViewModel : ObservableObject
{
    private readonly ProjectSessionService _projectSessionService;
    private int? _selectedTestId;
    private int? _selectedNodeId;
    private bool _isLoadingNode;

    public ExpectedNodePanelViewModel(ProjectSessionService projectSessionService)
    {
        _projectSessionService = projectSessionService;
    }

    public event Action<int, int>? NodeUpdated;

    public event Action<int, int>? NodeDeleted;

    [ObservableProperty]
    private string nodeName = string.Empty;

    [ObservableProperty]
    private string expectedCode = "200";

    [ObservableProperty]
    private string throwbackError = string.Empty;

    [ObservableProperty]
    private bool continueTest = true;

    [ObservableProperty]
    private string actualCode = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string error = string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(Error);

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
            NodeName = record.Name;
            ExpectedCode = GetValue(record, "ExpectedCode", "200");
            ThrowbackError = GetValue(record, "ThrowbackError", string.Empty);
            ContinueTest = bool.TryParse(GetValue(record, "ContinueTest", bool.TrueString), out var shouldContinue)
                ? shouldContinue
                : true;
            ActualCode = GetValue(record, "ActualCode", string.Empty);
            Error = GetValue(record, "Error", string.Empty);
        }
        finally
        {
            _isLoadingNode = false;
        }
    }

    partial void OnNodeNameChanged(string value)
    {
        UpdateSelectedNodeName(value);
    }

    partial void OnExpectedCodeChanged(string value)
    {
        UpdateSelectedNodeValue("ExpectedCode", value);
    }

    partial void OnThrowbackErrorChanged(string value)
    {
        UpdateSelectedNodeValue("ThrowbackError", value);
    }

    partial void OnContinueTestChanged(bool value)
    {
        UpdateSelectedNodeValue("ContinueTest", value.ToString());
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
