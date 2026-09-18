using System.Collections.ObjectModel;
using SkeletonKey.Locators;

namespace SkeletonKey.Web.Abstractions;

/// <summary>Defines one provider-neutral collection field read mode.</summary>
public enum WebCollectionFieldReadMode
{
    /// <summary>Read text content.</summary>
    Text,

    /// <summary>Read one named attribute.</summary>
    Attribute,

    /// <summary>Read a form-control value.</summary>
    Value,
}

/// <summary>Describes one field extracted relative to a matched parent item.</summary>
public sealed class WebCollectionFieldDefinition
{
    /// <summary>Initializes one field definition.</summary>
    public WebCollectionFieldDefinition(
        string name,
        ResolvedLocatorPlan locator,
        WebCollectionFieldReadMode readMode = WebCollectionFieldReadMode.Text,
        string? attributeName = null,
        bool required = false,
        int? elementIndex = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(locator);
        if (name.Length > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(name), "Collection field names cannot exceed 64 characters.");
        }

        if (readMode == WebCollectionFieldReadMode.Attribute && string.IsNullOrWhiteSpace(attributeName))
        {
            throw new ArgumentException("Attribute fields require an attribute name.", nameof(attributeName));
        }

        if (elementIndex is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elementIndex));
        }

        Name = name;
        Locator = locator;
        ReadMode = readMode;
        AttributeName = attributeName;
        Required = required;
        ElementIndex = elementIndex;
    }

    /// <summary>Gets the output field name.</summary>
    public string Name { get; }

    /// <summary>Gets the field locator resolved relative to each parent item.</summary>
    public ResolvedLocatorPlan Locator { get; }

    /// <summary>Gets the read mode.</summary>
    public WebCollectionFieldReadMode ReadMode { get; }

    /// <summary>Gets the attribute name when reading attributes.</summary>
    public string? AttributeName { get; }

    /// <summary>Gets whether a missing relative field fails extraction.</summary>
    public bool Required { get; }

    /// <summary>Gets an optional index when the relative locator matches multiple elements.</summary>
    public int? ElementIndex { get; }
}

/// <summary>Describes bounded structured collection extraction.</summary>
public sealed record WebCollectionExtractionRequest(
    WebTargetContext TargetContext,
    int TimeoutMilliseconds = 30000,
    int MaximumItems = 100,
    int MaximumOutputCharacters = 262144);

/// <summary>Represents one structured item while keeping field data provider-neutral.</summary>
public sealed class WebCollectionItem
{
    /// <summary>Initializes an immutable item.</summary>
    public WebCollectionItem(IReadOnlyDictionary<string, string?> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        Fields = new ReadOnlyDictionary<string, string?>(fields.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal));
    }

    /// <summary>Gets extracted field values by field name.</summary>
    public IReadOnlyDictionary<string, string?> Fields { get; }
}

/// <summary>Represents one bounded collection extraction result.</summary>
public sealed class WebCollectionExtractionResult
{
    /// <summary>Initializes a collection result.</summary>
    public WebCollectionExtractionResult(IReadOnlyList<WebCollectionItem> items, int totalMatchedCount, bool truncated)
    {
        ArgumentNullException.ThrowIfNull(items);
        Items = Array.AsReadOnly([.. items]);
        TotalMatchedCount = totalMatchedCount;
        Truncated = truncated;
    }

    /// <summary>Gets returned items in parent DOM order.</summary>
    public IReadOnlyList<WebCollectionItem> Items { get; }

    /// <summary>Gets the number of parent elements matched before bounding.</summary>
    public int TotalMatchedCount { get; }

    /// <summary>Gets whether item bounding truncated the result.</summary>
    public bool Truncated { get; }
}

/// <summary>Describes key-by-key text entry.</summary>
public sealed record WebTypeRequest(
    string Value,
    int DelayMilliseconds = 0,
    int TimeoutMilliseconds = 30000,
    int? ElementIndex = null,
    WebTargetContext? TargetContext = null);

/// <summary>Describes direct text insertion into a focused target.</summary>
public sealed record WebInsertTextRequest(
    string Value,
    int TimeoutMilliseconds = 30000,
    int? ElementIndex = null,
    WebTargetContext? TargetContext = null);

/// <summary>Describes a bounded target-only action such as clear or focus.</summary>
public sealed record WebElementActionRequest(
    int TimeoutMilliseconds = 30000,
    int? ElementIndex = null,
    WebTargetContext? TargetContext = null);

/// <summary>Describes page or element scrolling.</summary>
public sealed record WebScrollRequest(
    double DeltaX = 0,
    double DeltaY = 0,
    int TimeoutMilliseconds = 30000,
    int? ElementIndex = null,
    WebTargetContext? TargetContext = null);

/// <summary>Represents observable scroll position and extent.</summary>
public sealed record WebScrollResult(
    double OffsetX,
    double OffsetY,
    double ExtentWidth,
    double ExtentHeight,
    double ViewportWidth,
    double ViewportHeight,
    bool AtHorizontalStart,
    bool AtHorizontalEnd,
    bool AtVerticalStart,
    bool AtVerticalEnd);

/// <summary>Defines bounded observable wait conditions.</summary>
public enum WebWaitConditionKind
{
    /// <summary>At least one matching element exists.</summary>
    Exists,

    /// <summary>At least one matching element is visible.</summary>
    Visible,

    /// <summary>No matching element is visible.</summary>
    Hidden,

    /// <summary>One selected element text equals the expected value.</summary>
    TextEquals,

    /// <summary>One selected element text contains the expected value.</summary>
    TextContains,

    /// <summary>One selected element attribute equals the expected value.</summary>
    AttributeEquals,

    /// <summary>The matched element count equals the expected count.</summary>
    CountEquals,

    /// <summary>The matched element count is greater than the expected count.</summary>
    CountGreaterThan,

    /// <summary>One selected form-control value equals the expected value.</summary>
    ValueEquals,
}

/// <summary>Describes one bounded observable wait.</summary>
public sealed record WebWaitForConditionRequest(
    WebWaitConditionKind Condition,
    string? ExpectedValue = null,
    string? AttributeName = null,
    int? ExpectedCount = null,
    int TimeoutMilliseconds = 30000,
    int PollIntervalMilliseconds = 50,
    int? ElementIndex = null,
    WebTargetContext? TargetContext = null);

/// <summary>Represents the final observable state that satisfied a wait.</summary>
public sealed record WebWaitConditionResult(int Count, string? ActualValue = null);
