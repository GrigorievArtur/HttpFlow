using System.Collections.Generic;
using System.Linq;
using Httpflow.Desktop.Models.Projects;

namespace Httpflow.Desktop.Services.Projects;

public static class ProjectRunOrderPlanner
{
    public static IReadOnlyList<IReadOnlyList<ProjectTestState>> BuildOrderCycles(IEnumerable<ProjectTestState> tests)
    {
        return tests
            .OrderBy(test => NormalizeOrder(test.Order))
            .ThenBy(test => test.Id)
            .GroupBy(test => NormalizeOrder(test.Order))
            .Select(group => (IReadOnlyList<ProjectTestState>)group.ToList())
            .ToList();
    }

    private static int NormalizeOrder(int order)
    {
        return order <= 0 ? 1 : order;
    }
}
