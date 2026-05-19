using Httpflow.Desktop.Models.Projects;
using Httpflow.Desktop.Services.Projects;
using Xunit;

namespace Httpflow.Desktop.Tests.Services.Projects;

public sealed class ProjectRunOrderPlannerTests
{
    [Fact]
    public void BuildOrderCycles_GroupsTestsWithSameOrderAndSortsSequentially()
    {
        var tests = new[]
        {
            new ProjectTestState { Id = 3, Name = "Third", Order = 2 },
            new ProjectTestState { Id = 1, Name = "First", Order = 1 },
            new ProjectTestState { Id = 2, Name = "Second", Order = 1 }
        };

        var cycles = ProjectRunOrderPlanner.BuildOrderCycles(tests);

        Assert.Equal(2, cycles.Count);
        Assert.Equal([1, 2], cycles[0].Select(test => test.Id));
        Assert.Equal([3], cycles[1].Select(test => test.Id));
    }

    [Fact]
    public void BuildOrderCycles_TreatsInvalidOrderAsFirstCycle()
    {
        var tests = new[]
        {
            new ProjectTestState { Id = 1, Name = "Invalid", Order = 0 },
            new ProjectTestState { Id = 2, Name = "Second", Order = 2 }
        };

        var cycles = ProjectRunOrderPlanner.BuildOrderCycles(tests);

        Assert.Equal([1], cycles[0].Select(test => test.Id));
        Assert.Equal([2], cycles[1].Select(test => test.Id));
    }
}
