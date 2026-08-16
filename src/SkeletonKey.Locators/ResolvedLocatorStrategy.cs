namespace SkeletonKey.Locators;

/// <summary>
/// Represents one provider-neutral locator strategy in resolved fallback order.
/// </summary>
public sealed class ResolvedLocatorStrategy
{
    /// <summary>
    /// Initializes a resolved locator strategy.
    /// </summary>
    public ResolvedLocatorStrategy(string kind, string? role = null, string? name = null, string? value = null, string? selector = null, LocatorTextMatchMode match = LocatorTextMatchMode.Exact, bool caseSensitive = true)
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

    /// <summary>Gets CSS or XPath selector text.</summary>
    public string? Selector { get; }

    /// <summary>Gets exact or contains text match behavior.</summary>
    public LocatorTextMatchMode Match { get; }

    /// <summary>Gets a value indicating whether semantic text matching is case-sensitive.</summary>
    public bool CaseSensitive { get; }
}
