namespace SkeletonKey.Catalog.Json;

/// <summary>
/// Represents strict node catalog JSON serialization and deserialization failures.
/// </summary>
public sealed class NodeCatalogSerializationException : Exception
{
    /// <summary>
    /// Initializes a node catalog serialization exception.
    /// </summary>
    /// <param name="message">The failure message.</param>
    public NodeCatalogSerializationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a node catalog serialization exception.
    /// </summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public NodeCatalogSerializationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
