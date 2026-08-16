namespace SkeletonKey.Locators.Validation;

/// <summary>
/// Describes one deterministic locator validation diagnostic.
/// </summary>
/// <param name="Code">The stable diagnostic code.</param>
/// <param name="Message">The diagnostic message.</param>
/// <param name="Path">The JSON Pointer path.</param>
public sealed record LocatorValidationIssue(string Code, string Message, string Path);
