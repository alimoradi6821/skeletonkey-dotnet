namespace SkeletonKey.Expressions.Internal;

internal static class JsonPointerSyntax
{
    public static string Combine(string path, string token)
    {
        string escaped = token.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
        return string.IsNullOrEmpty(path) ? "/" + escaped : path + "/" + escaped;
    }

    public static string Combine(string path, int index)
    {
        return Combine(path, index.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
