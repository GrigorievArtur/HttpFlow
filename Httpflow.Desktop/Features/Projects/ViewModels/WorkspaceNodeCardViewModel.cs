using System.Collections.Generic;
using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Httpflow.Desktop.Models.Nodes;

namespace Httpflow.Desktop.Features.Projects.ViewModels;

public sealed partial class WorkspaceNodeCardViewModel : ObservableObject
{
    public WorkspaceNodeCardViewModel(int testId, CanvasNodeRecord node, bool showConnector)
    {
        TestId = testId;
        Node = node;
        ShowConnector = showConnector;
    }

    public int TestId { get; }

    public CanvasNodeRecord Node { get; }

    public int Id => Node.Id;

    public string Name => Node.Name;

    public string NodeType => Node.NodeType;

    public string RequestMethod => GetValue("Method", "GET");

    public string Host
    {
        get
        {
            var url = GetValue("Url", string.Empty);
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
            {
                return uri.Host;
            }

            return string.IsNullOrWhiteSpace(url) ? "No host" : url;
        }
    }

    public IReadOnlyList<NodeValueRecord> Values => Node.Values;

    public bool ShowConnector { get; }

    [ObservableProperty]
    private bool isSelected;

    private string GetValue(string label, string fallback)
    {
        return Values.FirstOrDefault(value => string.Equals(value.Label, label, StringComparison.OrdinalIgnoreCase))?.Value
               ?? fallback;
    }
}
