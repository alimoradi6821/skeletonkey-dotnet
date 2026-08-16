namespace SkeletonKey.Catalog.Validation;

/// <summary>
/// Describes one deterministic node catalog validation diagnostic.
/// </summary>
/// <param name="Code">The stable diagnostic code.</param>
/// <param name="Message">The diagnostic message.</param>
/// <param name="Path">The JSON Pointer path.</param>
public sealed record NodeCatalogValidationIssue(string Code, string Message, string Path);
