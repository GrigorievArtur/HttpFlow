using Httpflow.Desktop.Features.Projects.ViewModels;
using Httpflow.Desktop.Models.Nodes;
using Xunit;

namespace Httpflow.Desktop.Tests.Features.Projects.ViewModels;

public sealed class WorkspaceNodeCardViewModelTests
{
    [Fact]
    public void Host_ReturnsHost_WhenRequestUrlIsAbsolute()
    {
        var node = new CanvasNodeRecord(
            1,
            "Health request",
            NodeTypeNames.Request,
            0,
            0,
            [
                new NodeValueRecord("Method", "GET"),
                new NodeValueRecord("Url", "http://localhost:5157/api/v1/health")
            ]);

        var viewModel = new WorkspaceNodeCardViewModel(1, node, false);

        Assert.Equal("localhost", viewModel.Host);
        Assert.Equal("GET", viewModel.PrimaryInfo);
        Assert.Equal("localhost", viewModel.SecondaryInfo);
    }

    [Fact]
    public void ExpectedNode_DisplaysExpectedCodeAndContinueState()
    {
        var node = new CanvasNodeRecord(
            2,
            "Expected 200",
            NodeTypeNames.Expected,
            0,
            1,
            [
                new NodeValueRecord("ExpectedCode", "200"),
                new NodeValueRecord("ContinueTest", "False")
            ]);

        var viewModel = new WorkspaceNodeCardViewModel(1, node, false);

        Assert.Equal("Expect 200", viewModel.PrimaryInfo);
        Assert.Equal("Stop", viewModel.SecondaryInfo);
    }
}
