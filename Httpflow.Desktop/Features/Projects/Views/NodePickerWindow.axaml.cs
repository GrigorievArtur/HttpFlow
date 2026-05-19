using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Httpflow.Desktop.Features.Projects.Services;
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
        var ranked = NodePickerSearch.Filter(NodeCatalog.AvailableNodes, query)
            .Select(node => new NodePickerItem(node.Name, node.NodeType, node.Description))
            .ToList();

        _results.Clear();
        foreach (var item in ranked)
        {
            _results.Add(item);
        }
    }
}
