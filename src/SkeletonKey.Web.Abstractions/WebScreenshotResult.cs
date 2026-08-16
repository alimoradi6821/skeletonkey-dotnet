namespace SkeletonKey.Web.Abstractions;

/// <summary>
/// Represents owned screenshot data returned from a web page adapter.
/// </summary>
public sealed class WebScreenshotResult
{
    private readonly byte[] _bytes;

    /// <summary>
    /// Initializes an owned screenshot result.
    /// </summary>
    public WebScreenshotResult(string mediaType, byte[] bytes)
    {
        MediaType = mediaType;
        _bytes = bytes is null ? throw new ArgumentNullException(nameof(bytes)) : [.. bytes];
    }

    /// <summary>Gets the screenshot media type.</summary>
    public string MediaType { get; }

    /// <summary>Gets a defensive copy of the screenshot bytes.</summary>
    public byte[] Bytes => [.. _bytes];

    /// <summary>Gets the base64 encoded screenshot payload.</summary>
    public string Base64 => Convert.ToBase64String(_bytes);
}
