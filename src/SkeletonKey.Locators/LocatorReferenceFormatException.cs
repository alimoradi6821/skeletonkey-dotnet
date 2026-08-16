namespace SkeletonKey.Locators;

/// <summary>
/// Reports a malformed reserved `$locator` workflow-value wrapper.
/// </summary>
public sealed class LocatorReferenceFormatException : FormatException
{
    /// <summary>
    /// Initializes the exception.
    /// </summary>
    /// <param name="jsonPath">The JSON Pointer location of the format error.</param>
    /// <param name="message">The error message.</param>
    public LocatorReferenceFormatException(string jsonPath, string message)
        : base(message)
    {
        JsonPath = jsonPath;
    }

    /// <summary>Gets the JSON Pointer location of the format error.</summary>
    public string JsonPath { get; }
}
