using System.Collections.Generic;

namespace Httpflow.Desktop.Models.Nodes;

public sealed record CanvasNodeRecord(
    int Id,
    string Name,
    string NodeType,
    int X,
    int Y,
    IReadOnlyList<NodeValueRecord> Values);
