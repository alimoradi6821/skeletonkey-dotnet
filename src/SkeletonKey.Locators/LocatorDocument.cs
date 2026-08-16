using System.Collections.ObjectModel;

namespace SkeletonKey.Locators;

/// <summary>
/// Describes an immutable versioned locator catalog independent from workflow graph logic.
/// </summary>
public sealed class LocatorDocument
{
    private static readonly IReadOnlyDictionary<string, LocatorDefinition> _emptyLocators = new ReadOnlyDictionary<string, LocatorDefinition>(new Dictionary<string, LocatorDefinition>());

    /// <summary>
    /// Initializes a locator document.
    /// </summary>
    /// <param name="schema">The locator schema URI declaration.</param>
    /// <param name="specVersion">The locator specification version.</param>
    /// <param name="id">The locator document ID.</param>
    /// <param name="name">Optional display name.</param>
    /// <param name="description">Optional human-readable description.</param>
    /// <param name="locators">Local locator definitions keyed by locator ID.</param>
    public LocatorDocument(
        string? schema = LocatorSpecification.CurrentSchemaUri,
        string specVersion = LocatorSpecification.CurrentVersion,
        string id = "",
        string? name = null,
        string? description = null,
        IReadOnlyDictionary<string, LocatorDefinition>? locators = null)
    {
        Schema = schema;
        SpecVersion = specVersion;
        Id = id;
        Name = name;
        Description = description;
        Locators = locators is null
            ? _emptyLocators
            : new ReadOnlyDictionary<string, LocatorDefinition>(new Dictionary<string, LocatorDefinition>(locators));
    }

    /// <summary>Gets the locator schema URI declaration.</summary>
    public string? Schema { get; }

    /// <summary>Gets the locator specification version.</summary>
    public string SpecVersion { get; }

    /// <summary>Gets the locator document ID.</summary>
    public string Id { get; }

    /// <summary>Gets optional display name.</summary>
    public string? Name { get; }

    /// <summary>Gets optional human-readable description.</summary>
    public string? Description { get; }

    /// <summary>Gets local locator definitions keyed by locator ID.</summary>
    public IReadOnlyDictionary<string, LocatorDefinition> Locators { get; }
}
