namespace SkeletonKey.Locators;

/// <summary>
/// Describes a discovered locator reference wrapper and its JSON Pointer location.
/// </summary>
/// <param name="Path">The JSON Pointer path to the wrapper.</param>
/// <param name="Reference">The immutable locator reference.</param>
public sealed record LocatorReferenceOccurrence(string Path, LocatorReference Reference);
