using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Httpflow.Desktop.Services.Projects;

public static class NodeCopyNameGenerator
{
    public static string GetNextCopyName(IEnumerable<string> existingNodeNames, string sourceName)
    {
        var baseName = Regex.Replace(sourceName, @"\s+Copy(?:\s+\(\d+\))?$", string.Empty, RegexOptions.IgnoreCase).Trim();
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "Node";
        }

        var existingNames = existingNodeNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var firstCopyName = $"{baseName} Copy";
        if (!existingNames.Contains(firstCopyName))
        {
            return firstCopyName;
        }

        for (var index = 2; ; index++)
        {
            var candidate = $"{baseName} Copy ({index})";
            if (!existingNames.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
