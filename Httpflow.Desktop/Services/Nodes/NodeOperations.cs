using System;
using Httpflow.Desktop.Dtos.Operations;

namespace Httpflow.Desktop.Services;

public class NodeOperations
{
    public void CreateNode(NewNodeOperation operation)
    {
        Console.WriteLine($"Creating node at X={operation.X}, Y={operation.Y}, Type = {operation.NodeType}");
    }
}
