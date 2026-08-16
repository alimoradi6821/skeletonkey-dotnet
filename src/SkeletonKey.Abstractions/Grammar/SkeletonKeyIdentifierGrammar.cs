using System.Text.RegularExpressions;

namespace SkeletonKey.Abstractions.Grammar;

/// <summary>
/// Provides shared provider-neutral identifier grammar checks used by workflow and catalog contracts.
/// </summary>
public static partial class SkeletonKeyIdentifierGrammar
{
    /// <summary>
    /// Gets the canonical JSON Schema regular-expression pattern for node type identifiers.
    /// </summary>
    public const string NodeTypePattern = "^[a-z](?:(?:[A-Za-z0-9]|-(?!-))*[A-Za-z0-9])?(?:\\.[a-z](?:(?:[A-Za-z0-9]|-(?!-))*[A-Za-z0-9])?)+$";

    /// <summary>
    /// Determines whether a value is a canonical node type identifier.
    /// </summary>
    /// <param name="value">The identifier to inspect.</param>
    /// <returns><see langword="true" /> when the value matches the canonical grammar; otherwise, <see langword="false" />.</returns>
    public static bool IsNodeType(string value)
    {
        return NodeTypeRegex().IsMatch(value);
    }

    [GeneratedRegex(NodeTypePattern, RegexOptions.CultureInvariant)]
    private static partial Regex NodeTypeRegex();
}
