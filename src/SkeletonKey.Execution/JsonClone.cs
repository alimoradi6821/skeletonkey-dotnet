using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace SkeletonKey.Execution;

internal static class JsonClone
{
    internal static JsonObject CloneObject(JsonObject value)
    {
        return (JsonObject)value.DeepClone();
    }

    internal static JsonObject? CloneOptionalObject(JsonObject? value)
    {
        return value is null ? null : (JsonObject)value.DeepClone();
    }

    internal static JsonNode? CloneNode(JsonNode? value)
    {
        return value?.DeepClone();
    }

    internal static IReadOnlyList<JsonNode?> CloneNodes(IEnumerable<JsonNode?> values)
    {
        return new ReadOnlyCollection<JsonNode?>(values.Select(static value => value?.DeepClone()).ToArray());
    }
}
