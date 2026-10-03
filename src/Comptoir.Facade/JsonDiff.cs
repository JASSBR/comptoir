using System.Globalization;
using System.Text.Json.Nodes;

namespace Comptoir.Facade;

/// <summary>
/// Semantic comparison of two JSON documents: numbers by value (Web API 2 writes 20, System.Text.Json 20.00), objects
/// by key whatever the order, arrays by position. Returns the paths that differ, first ones first.
/// </summary>
public static class JsonDiff
{
    public static IReadOnlyList<string> Compare(JsonNode? legacy, JsonNode? candidate, int max = 5)
    {
        var differences = new List<string>();
        Walk("$", legacy, candidate, differences, max);
        return differences;
    }

    private static void Walk(string path, JsonNode? legacy, JsonNode? candidate, List<string> differences, int max)
    {
        if (differences.Count >= max)
        {
            return;
        }

        switch (legacy, candidate)
        {
            case (null, null):
                return;
            case (JsonObject left, JsonObject right):
                foreach (var key in left.Select(p => p.Key).Union(right.Select(p => p.Key), StringComparer.Ordinal))
                {
                    Walk($"{path}.{key}", left[key], right[key], differences, max);
                }

                return;
            case (JsonArray left, JsonArray right):
                if (left.Count != right.Count)
                {
                    differences.Add($"{path}: {left.Count} items in the legacy, {right.Count} in the new");
                }

                for (var i = 0; i < Math.Min(left.Count, right.Count); i++)
                {
                    Walk($"{path}[{i}]", left[i], right[i], differences, max);
                }

                return;
            case (JsonValue left, JsonValue right) when TryNumber(left, out var a) && TryNumber(right, out var b):
                if (a != b)
                {
                    differences.Add($"{path}: legacy {a.ToString(CultureInfo.InvariantCulture)}, new {b.ToString(CultureInfo.InvariantCulture)}");
                }

                return;
            default:
                var l = legacy?.ToJsonString() ?? "null";
                var r = candidate?.ToJsonString() ?? "null";
                if (!string.Equals(l, r, StringComparison.Ordinal))
                {
                    differences.Add($"{path}: legacy {Truncate(l)}, new {Truncate(r)}");
                }

                return;
        }
    }

    private static bool TryNumber(JsonValue value, out decimal number)
    {
        number = 0;
        return value.GetValueKind() == System.Text.Json.JsonValueKind.Number && value.TryGetValue(out number);
    }

    private static string Truncate(string text) => text.Length <= 60 ? text : string.Concat(text.AsSpan(0, 57), "...");
}
