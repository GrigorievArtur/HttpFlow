using System.Collections.Generic;
using Httpflow.Desktop.Models.Nodes;

namespace Httpflow.Desktop.Models.Projects;

public sealed class ProjectTestState
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = "Not started";

    public List<CanvasNodeRecord> Nodes { get; set; } = [];
}
