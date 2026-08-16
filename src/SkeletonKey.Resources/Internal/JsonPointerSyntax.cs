using System.Globalization;

namespace SkeletonKey.Resources.Internal;

internal static class JsonPointerSyntax
{
    public static string Combine(string path, string token)
    {
        return path + "/" + Escape(token);
    }

    public static string Combine(string path, int index)
    {
        return path + "/" + index.ToString(CultureInfo.InvariantCulture);
    }

    private static string Escape(string token)
    {
        return token.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
    }
}
