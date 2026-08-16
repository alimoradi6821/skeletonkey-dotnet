namespace SkeletonKey.Locators;

/// <summary>
/// Describes one locator strategy resolution attempt for diagnostics.
/// </summary>
public sealed class LocatorStrategyAttempt
{
    /// <summary>
    /// Initializes a locator strategy attempt.
    /// </summary>
    public LocatorStrategyAttempt(string kind, int order, int? matchedCount = null, bool accepted = false, string? errorCode = null)
    {
        Kind = kind;
        Order = order;
        MatchedCount = matchedCount;
        Accepted = accepted;
        ErrorCode = errorCode;
    }

    /// <summary>Gets the strategy kind.</summary>
    public string Kind { get; }

    /// <summary>Gets the zero-based strategy order.</summary>
    public int Order { get; }

    /// <summary>Gets the matched count, when a provider produced one.</summary>
    public int? MatchedCount { get; }

    /// <summary>Gets whether this strategy was accepted.</summary>
    public bool Accepted { get; }

    /// <summary>Gets an optional structured provider-neutral error code.</summary>
    public string? ErrorCode { get; }
}
