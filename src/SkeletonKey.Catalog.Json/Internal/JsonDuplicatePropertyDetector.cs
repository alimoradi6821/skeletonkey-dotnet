using System.Text;
using System.Text.Json;

namespace SkeletonKey.Catalog.Json.Internal;

internal static class JsonDuplicatePropertyDetector
{
    public static void RejectDuplicates(string json)
    {
        var stack = new Stack<HashSet<string>>();
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json), new JsonReaderOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
        });

        while (reader.Read())
        {
            if (reader.TokenType is JsonTokenType.StartObject)
            {
                stack.Push(new HashSet<string>(StringComparer.Ordinal));
            }
            else if (reader.TokenType is JsonTokenType.EndObject)
            {
                stack.Pop();
            }
            else if (reader.TokenType is JsonTokenType.PropertyName)
            {
                string propertyName = reader.GetString() ?? string.Empty;
                if (!stack.Peek().Add(propertyName))
                {
                    throw new JsonException($"Duplicate JSON property '{propertyName}' is not allowed.");
                }
            }
        }
    }
}
