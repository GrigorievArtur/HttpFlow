using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Httpflow.Desktop.Features.Projects.ViewModels;

public sealed partial class WorkspaceTestColumnViewModel : ObservableObject
{
    public WorkspaceTestColumnViewModel(int id, string name, IEnumerable<WorkspaceNodeCardViewModel> nodes)
    {
        Id = id;
        Name = name;
        Nodes = new ObservableCollection<WorkspaceNodeCardViewModel>(nodes);
    }

    public int Id { get; }

    public string Name { get; }

    public ObservableCollection<WorkspaceNodeCardViewModel> Nodes { get; }

    [ObservableProperty]
    private bool isSelected;
}
