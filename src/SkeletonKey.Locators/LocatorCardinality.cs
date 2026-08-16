namespace SkeletonKey.Locators;

/// <summary>
/// Describes the expected match count for a semantic locator.
/// </summary>
public enum LocatorCardinality
{
    /// <summary>Exactly one match is expected.</summary>
    One,

    /// <summary>Zero or one match is acceptable.</summary>
    ZeroOrOne,

    /// <summary>At least one match is expected.</summary>
    OneOrMore,

    /// <summary>Any number of matches, including zero, is acceptable.</summary>
    Many,
}
