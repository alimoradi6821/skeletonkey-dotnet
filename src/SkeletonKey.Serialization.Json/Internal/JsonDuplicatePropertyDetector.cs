using System.Text;
using System.Text.Json;

namespace SkeletonKey.Serialization.Json.Internal;

internal static class JsonDuplicatePropertyDetector
{
    public static void RejectDuplicates(string json)
    {
        byte[] utf8Json = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(utf8Json, new JsonReaderOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
        });

        Stack<HashSet<string>> objectPropertyNames = new();

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    objectPropertyNames.Push(new HashSet<string>(StringComparer.Ordinal));
                    break;

                case JsonTokenType.EndObject:
                    _ = objectPropertyNames.Pop();
                    break;

                case JsonTokenType.PropertyName:
                    string propertyName = reader.GetString() ?? string.Empty;
                    HashSet<string> currentProperties = objectPropertyNames.Peek();
                    if (!currentProperties.Add(propertyName))
                    {
                        (long line, long bytePosition) = GetLineInfo(utf8Json, reader.TokenStartIndex);
                        throw new JsonException(
                            $"Duplicate JSON property '{propertyName}' is not allowed.",
                            null,
                            line,
                            bytePosition);
                    }

                    break;
            }
        }
    }

    private static (long Line, long BytePosition) GetLineInfo(byte[] utf8Json, long tokenStartIndex)
    {
        long line = 0;
        long lastLineStart = 0;
        long max = Math.Min(tokenStartIndex, utf8Json.LongLength);

        for (long index = 0; index < max; index++)
        {
            if (utf8Json[index] == (byte)'\n')
            {
                line++;
                lastLineStart = index + 1;
            }
        }

        return (line, tokenStartIndex - lastLineStart);
    }
}
