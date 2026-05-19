using Httpflow.Desktop.Services.Projects;
using Xunit;

namespace Httpflow.Desktop.Tests.Services.Projects;

public sealed class NodeCopyNameGeneratorTests
{
    [Fact]
    public void GetNextCopyName_AddsCopy_WhenNoCopyExists()
    {
        var result = NodeCopyNameGenerator.GetNextCopyName(["Request 1"], "Request 1");

        Assert.Equal("Request 1 Copy", result);
    }

    [Fact]
    public void GetNextCopyName_IncrementsCopyNumber_WhenCopyAlreadyExists()
    {
        var result = NodeCopyNameGenerator.GetNextCopyName(
            ["Request 1", "Request 1 Copy", "Request 1 Copy (2)"],
            "Request 1");

        Assert.Equal("Request 1 Copy (3)", result);
    }

    [Fact]
    public void GetNextCopyName_StripsExistingCopySuffix_BeforeIncrementing()
    {
        var result = NodeCopyNameGenerator.GetNextCopyName(
            ["Request 1", "Request 1 Copy", "Request 1 Copy (2)"],
            "Request 1 Copy");

        Assert.Equal("Request 1 Copy (3)", result);
    }
}
