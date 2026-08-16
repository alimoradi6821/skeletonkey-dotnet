namespace SkeletonKey.Locators.Json;

/// <summary>
/// Reports strict locator JSON serialization or deserialization failures.
/// </summary>
public sealed class LocatorSerializationException : Exception
{
    /// <summary>
    /// Initializes the exception.
    /// </summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">Optional underlying exception.</param>
    public LocatorSerializationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
