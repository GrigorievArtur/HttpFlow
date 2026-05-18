using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Httpflow.Desktop.Features.Projects.ViewModels;

public sealed partial class WorkspaceTestColumnViewModel : ObservableObject
{
    public WorkspaceTestColumnViewModel(
        int id,
        string name,
        int order,
        string status,
        IEnumerable<WorkspaceNodeCardViewModel> nodes)
    {
        Id = id;
        Name = name;
        Order = order;
        Status = status;
        Nodes = new ObservableCollection<WorkspaceNodeCardViewModel>(nodes);
    }

    public int Id { get; }

    public string Name { get; }

    public int Order { get; }

    public string Status { get; }

    public int NodeCount => Nodes.Count;

    public ObservableCollection<WorkspaceNodeCardViewModel> Nodes { get; }

    [ObservableProperty]
    private bool isSelected;
}
