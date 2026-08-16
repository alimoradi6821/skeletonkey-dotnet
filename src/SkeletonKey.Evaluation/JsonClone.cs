using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace SkeletonKey.Evaluation;

internal static class JsonClone
{
    internal static JsonNode? CloneNode(JsonNode? value)
    {
        return value?.DeepClone();
    }

    internal static JsonObject CloneObject(IReadOnlyDictionary<string, JsonNode?> values)
    {
        JsonObject clone = [];
        foreach (KeyValuePair<string, JsonNode?> value in values)
        {
            clone[value.Key] = value.Value?.DeepClone();
        }

        return clone;
    }

    internal static IReadOnlyDictionary<string, JsonNode?> CloneDictionary(IReadOnlyDictionary<string, JsonNode?> values)
    {
        Dictionary<string, JsonNode?> clone = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, JsonNode?> value in values)
        {
            clone[value.Key] = value.Value?.DeepClone();
        }

        return new ReadOnlyDictionary<string, JsonNode?>(clone);
    }
}
