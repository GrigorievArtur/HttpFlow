using Httpflow.Desktop.Features.Projects.Services;
using Httpflow.Desktop.Models.Nodes;
using Xunit;

namespace Httpflow.Desktop.Tests.Features.Projects.Services;

public sealed class NodePickerSearchTests
{
    [Theory]
    [InlineData(" Request Node ", "requestnode")]
    [InlineData("EXPECTED   response", "expectedresponse")]
    [InlineData("Req\tuest\nNode", "requestnode")]
    public void NormalizeSearchText_RemovesWhitespaceAndLowercases(string value, string expected)
    {
        Assert.Equal(expected, NodePickerSearch.NormalizeSearchText(value));
    }

    [Fact]
    public void Filter_MatchesNodeName_WhenQueryHasSpacesAndDifferentCasing()
    {
        var nodes = new[]
        {
            new NodeDefinition("Request", NodeTypeNames.Request, "HTTP request"),
            new NodeDefinition("Expected", NodeTypeNames.Expected, "Expected response")
        };

        var result = NodePickerSearch.Filter(nodes, "  e X p ");

        Assert.Single(result);
        Assert.Equal(NodeTypeNames.Expected, result[0].NodeType);
    }

    [Fact]
    public void Filter_ReturnsAllNodes_WhenQueryIsEmptyWhitespace()
    {
        var nodes = new[]
        {
            new NodeDefinition("Request", NodeTypeNames.Request, "HTTP request"),
            new NodeDefinition("Expected", NodeTypeNames.Expected, "Expected response")
        };

        var result = NodePickerSearch.Filter(nodes, "   ");

        Assert.Equal(2, result.Count);
    }
}
