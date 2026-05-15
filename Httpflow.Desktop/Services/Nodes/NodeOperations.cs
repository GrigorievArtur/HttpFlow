using System.Collections.Generic;
using System.Linq;
using Httpflow.Desktop.Dtos.Operations;
using Httpflow.Desktop.Models.Nodes;

namespace Httpflow.Desktop.Services;

public class NodeOperations
{
    private int _nextNodeId = 1;

    public void SyncNextNodeId(IEnumerable<CanvasNodeRecord> nodes)
    {
        _nextNodeId = nodes.Any() ? nodes.Max(node => node.Id) + 1 : 1;
    }

    public CanvasNodeRecord CreateNodeRecord(NewNodeOperation operation)
    {
        return new CanvasNodeRecord(
            _nextNodeId++,
            $"{operation.NodeType} Node",
            operation.NodeType,
            operation.X,
            operation.Y,
            CreateDefaultValues(operation.NodeType));
    }

    private static IReadOnlyList<NodeValueRecord> CreateDefaultValues(string nodeType)
    {
        return nodeType switch
        {
            "Start" =>
            [
                new NodeValueRecord("Status", "Ready"),
                new NodeValueRecord("Order", "1")
            ],
            _ =>
            [
                new NodeValueRecord("Status", "Draft")
            ]
        };
    }
}
