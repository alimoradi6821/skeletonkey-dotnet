using System.Text.Json;

namespace SkeletonKey.Serialization.Json.Internal;

internal static class JsonExceptionFactory
{
    public static JsonException Create(string message, string jsonPointerPath)
    {
        return new JsonException(message, jsonPointerPath, null, null);
    }
}
