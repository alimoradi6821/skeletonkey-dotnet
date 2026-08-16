namespace SkeletonKey.Catalog;

/// <summary>
/// Describes how a node handler is allowed to use a declared locator slot.
/// </summary>
public enum LocatorUsageMode
{
    /// <summary>The node requires one selected element.</summary>
    Single,

    /// <summary>The node may operate on an ordered collection of matched elements.</summary>
    Collection,

    /// <summary>The node may operate on zero or one selected element.</summary>
    OptionalSingle,
}
