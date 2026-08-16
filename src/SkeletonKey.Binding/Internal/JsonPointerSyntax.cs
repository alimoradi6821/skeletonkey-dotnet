namespace SkeletonKey.Binding.Internal;

internal static class JsonPointerSyntax
{
    public static bool IsValidReadPointer(string value)
    {
        if (value.Length == 0)
        {
            return true;
        }

        if (!value.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        string[] tokens = value[1..].Split('/');
        foreach (string token in tokens)
        {
            if (token == "-")
            {
                return false;
            }

            for (int index = 0; index < token.Length; index++)
            {
                if (token[index] == '~')
                {
                    if (index + 1 >= token.Length || token[index + 1] is not ('0' or '1'))
                    {
                        return false;
                    }

                    index++;
                }
            }
        }

        return true;
    }

    public static string Combine(string path, string token)
    {
        string escaped = token.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
        return path.Length == 0 ? "/" + escaped : path + "/" + escaped;
    }

    public static string Combine(string path, int index)
    {
        return path.Length == 0 ? "/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) : path + "/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
