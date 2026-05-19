using System.Collections.Generic;
using System.Linq;
using System.Text;
using Httpflow.Desktop.Models.Nodes;

namespace Httpflow.Desktop.Features.Projects.Services;

public static class NodePickerSearch
{
    public static IReadOnlyList<NodeDefinition> Filter(IEnumerable<NodeDefinition> nodes, string query)
    {
        return nodes
            .Select(node => new
            {
                Node = node,
                Score = GetFuzzyScore(node.Name, query)
            })
            .Where(item => item.Score >= 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Node.Name)
            .Select(item => item.Node)
            .ToList();
    }

    public static int GetFuzzyScore(string value, string query)
    {
        var normalizedValue = NormalizeSearchText(value);
        var normalizedQuery = NormalizeSearchText(query);

        if (normalizedQuery.Length == 0)
        {
            return 1;
        }

        var score = 0;
        var queryIndex = 0;
        for (var valueIndex = 0; valueIndex < normalizedValue.Length && queryIndex < normalizedQuery.Length; valueIndex++)
        {
            if (normalizedValue[valueIndex] != normalizedQuery[queryIndex])
            {
                continue;
            }

            score += valueIndex == queryIndex ? 3 : 1;
            queryIndex++;
        }

        return queryIndex == normalizedQuery.Length ? score : -1;
    }

    public static string NormalizeSearchText(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (!char.IsWhiteSpace(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }
}
