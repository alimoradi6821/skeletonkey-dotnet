namespace SkeletonKey.Locators;

/// <summary>
/// Describes one immutable provider-neutral locator strategy.
/// </summary>
/// <remarks>
/// Strategies are ordered by their containing locator definition: the first strategy is preferred and
/// later strategies are deterministic fallbacks. This contract stores selector text only and never
/// executes CSS, XPath, browser, or accessibility lookups.
/// </remarks>
public sealed class LocatorStrategy
{
    /// <summary>
    /// Initializes a locator strategy.
    /// </summary>
    /// <param name="kind">The strategy kind.</param>
    /// <param name="role">The role used by role strategies.</param>
    /// <param name="name">The accessible name used by role strategies.</param>
    /// <param name="value">The semantic text used by text-like strategies.</param>
    /// <param name="selector">The selector text used by CSS and XPath fallback strategies.</param>
    /// <param name="match">The text matching mode for semantic text.</param>
    /// <param name="caseSensitive">Whether semantic text matching is case-sensitive.</param>
    public LocatorStrategy(
        string kind,
        string? role = null,
        string? name = null,
        string? value = null,
        string? selector = null,
        LocatorTextMatchMode match = LocatorTextMatchMode.Exact,
        bool caseSensitive = true)
    {
        Kind = kind;
        Role = role;
        Name = name;
        Value = value;
        Selector = selector;
        Match = match;
        CaseSensitive = caseSensitive;
    }

    /// <summary>Gets the strategy kind.</summary>
    public string Kind { get; }

    /// <summary>Gets the role used by role strategies.</summary>
    public string? Role { get; }

    /// <summary>Gets the accessible name used by role strategies.</summary>
    public string? Name { get; }

    /// <summary>Gets the semantic text value used by text-like strategies.</summary>
    public string? Value { get; }

    /// <summary>Gets the selector text used by CSS and XPath fallback strategies.</summary>
    public string? Selector { get; }

    /// <summary>Gets the semantic text match mode.</summary>
    public LocatorTextMatchMode Match { get; }

    /// <summary>Gets a value indicating whether semantic text matching is case-sensitive.</summary>
    public bool CaseSensitive { get; }
}
