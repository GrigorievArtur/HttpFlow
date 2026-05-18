using System.Collections.Generic;

namespace Httpflow.Desktop.Models.Nodes;

public static class NodeCatalog
{
    public static IReadOnlyList<NodeDefinition> AvailableNodes { get; } =
    [
        new NodeDefinition(NodeTypeNames.Request, NodeTypeNames.Request, "Send an HTTP request and store the response."),
        new NodeDefinition(NodeTypeNames.Expected, NodeTypeNames.Expected, "Check the last response status code.")
    ];
}
