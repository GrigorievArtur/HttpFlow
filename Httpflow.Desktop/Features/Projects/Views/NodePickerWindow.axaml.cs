using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Httpflow.Desktop.Models.Nodes;

namespace Httpflow.Desktop.Features.Projects.Views;

public sealed record NodePickerItem(string Name, string NodeType, string Description);

public partial class NodePickerWindow : Window
{
    private readonly ObservableCollection<NodePickerItem> _results = [];

    public NodePickerWindow()
    {
        InitializeComponent();
        ResultsList.ItemsSource = _results;
        RefreshResults(string.Empty);
        Opened += (_, _) => SearchBox.Focus();
    }

    public string? SelectedNodeType { get; private set; }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        RefreshResults(SearchBox.Text ?? string.Empty);
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        SelectNode(_results.FirstOrDefault());
        e.Handled = true;
    }

    private void OnNodeButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: NodePickerItem item })
        {
            SelectNode(item);
        }
    }

    private void SelectNode(NodePickerItem? item)
    {
        if (item is null)
        {
            return;
        }

        SelectedNodeType = item.NodeType;
        Close(true);
    }

    private void RefreshResults(string query)
    {
        var ranked = NodeCatalog.AvailableNodes
            .Select(node => new
            {
                Node = node,
                Score = GetFuzzyScore(node.Name, query)
            })
            .Where(item => item.Score >= 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Node.Name)
            .Select(item => new NodePickerItem(item.Node.Name, item.Node.NodeType, item.Node.Description))
            .ToList();

        _results.Clear();
        foreach (var item in ranked)
        {
            _results.Add(item);
        }
    }

    private static int GetFuzzyScore(string value, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return 1;
        }

        var score = 0;
        var queryIndex = 0;
        for (var valueIndex = 0; valueIndex < value.Length && queryIndex < query.Length; valueIndex++)
        {
            if (char.ToUpperInvariant(value[valueIndex]) != char.ToUpperInvariant(query[queryIndex]))
            {
                continue;
            }

            score += valueIndex == queryIndex ? 3 : 1;
            queryIndex++;
        }

        return queryIndex == query.Length ? score : -1;
    }
}
