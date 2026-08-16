namespace SkeletonKey.Validation.Internal;

internal static class JsonPointer
{
    public static string Combine(params ReadOnlySpan<string> tokens)
    {
        if (tokens.Length == 0)
        {
            return string.Empty;
        }

        return "/" + string.Join("/", tokens.ToArray().Select(Escape));
    }

    public static string Escape(string token)
    {
        return token.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
    }
}
