using System.Collections.ObjectModel;

namespace SkeletonKey.Http.Abstractions;

/// <summary>Stable error codes produced by the generic HTTP capability.</summary>
public static class HttpCapabilityErrorCodes
{
    /// <summary>The request contract is invalid.</summary>
    public const string InvalidRequest = "SKH1001";

    /// <summary>The transport failed before a valid response was produced.</summary>
    public const string TransportFailure = "SKH1002";

    /// <summary>The response body exceeded the configured bound.</summary>
    public const string ResponseTooLarge = "SKH1003";

    /// <summary>The response could not be represented by the provider-neutral contract.</summary>
    public const string InvalidResponse = "SKH1004";

    /// <summary>The request exceeded its configured timeout.</summary>
    public const string RequestTimeout = "SKH1005";

    /// <summary>The caller cancelled the request.</summary>
    public const string RequestCancelled = "SKH1006";
}

/// <summary>Represents an expected provider-neutral HTTP failure.</summary>
public sealed class HttpCapabilityException : Exception
{
    /// <summary>Initializes a capability failure.</summary>
    public HttpCapabilityException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    /// <summary>Gets the stable capability error code.</summary>
    public string Code { get; }
}

/// <summary>Represents one bounded provider-neutral HTTP request.</summary>
public sealed class HttpTransportRequest
{
    private const int MaximumAllowedResponseBytes = 16 * 1024 * 1024;
    private readonly IReadOnlyDictionary<string, string> _headers;

    /// <summary>Initializes an HTTP request.</summary>
    public HttpTransportRequest(
        string method,
        Uri uri,
        IReadOnlyDictionary<string, string>? headers = null,
        string? body = null,
        string? contentType = null,
        int timeoutMilliseconds = 30000,
        int maximumResponseBytes = 1024 * 1024,
        bool followRedirects = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("HTTP request URI must be an absolute http or https URI.", nameof(uri));
        }

        if (timeoutMilliseconds is < 1 or > 300000)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds), timeoutMilliseconds, "HTTP timeout must be between 1 and 300000 milliseconds.");
        }

        if (maximumResponseBytes is < 1 or > MaximumAllowedResponseBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumResponseBytes), maximumResponseBytes, "Maximum response bytes must be between 1 and 16777216.");
        }

        Dictionary<string, string> copiedHeaders = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> header in headers ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(header.Key);
            if (header.Key.Contains('\r') || header.Key.Contains('\n') || header.Value.Contains('\r') || header.Value.Contains('\n'))
            {
                throw new ArgumentException("HTTP header names and values cannot contain CR or LF characters.", nameof(headers));
            }

            copiedHeaders.Add(header.Key, header.Value);
        }

        Method = method.Trim().ToUpperInvariant();
        Uri = uri;
        _headers = new ReadOnlyDictionary<string, string>(copiedHeaders);
        Body = body;
        ContentType = contentType;
        TimeoutMilliseconds = timeoutMilliseconds;
        MaximumResponseBytes = maximumResponseBytes;
        FollowRedirects = followRedirects;
    }

    /// <summary>Gets the normalized HTTP method.</summary>
    public string Method { get; }

    /// <summary>Gets the absolute request URI.</summary>
    public Uri Uri { get; }

    /// <summary>Gets a defensive read-only header map.</summary>
    public IReadOnlyDictionary<string, string> Headers => new ReadOnlyDictionary<string, string>(
        _headers.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase));

    /// <summary>Gets the optional request body.</summary>
    public string? Body { get; }

    /// <summary>Gets the optional request content type.</summary>
    public string? ContentType { get; }

    /// <summary>Gets the request timeout in milliseconds.</summary>
    public int TimeoutMilliseconds { get; }

    /// <summary>Gets the maximum accepted response body size.</summary>
    public int MaximumResponseBytes { get; }

    /// <summary>Gets whether redirects may be followed.</summary>
    public bool FollowRedirects { get; }
}

/// <summary>Represents one bounded provider-neutral HTTP response.</summary>
public sealed class HttpTransportResponse
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _headers;

    /// <summary>Initializes an HTTP response.</summary>
    public HttpTransportResponse(int statusCode, IReadOnlyDictionary<string, IReadOnlyList<string>>? headers, string body, Uri finalUri)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(finalUri);
        if (statusCode is < 100 or > 999)
        {
            throw new ArgumentOutOfRangeException(nameof(statusCode));
        }

        Dictionary<string, IReadOnlyList<string>> copied = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, IReadOnlyList<string>> header in headers ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase))
        {
            copied[header.Key] = Array.AsReadOnly([.. header.Value]);
        }

        StatusCode = statusCode;
        _headers = new ReadOnlyDictionary<string, IReadOnlyList<string>>(copied);
        Body = body;
        FinalUri = finalUri;
    }

    /// <summary>Gets the numeric HTTP status code.</summary>
    public int StatusCode { get; }

    /// <summary>Gets a defensive read-only response header map.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers => new ReadOnlyDictionary<string, IReadOnlyList<string>>(
        _headers.ToDictionary(static pair => pair.Key, static pair => (IReadOnlyList<string>)Array.AsReadOnly([.. pair.Value]), StringComparer.OrdinalIgnoreCase));

    /// <summary>Gets the bounded response body text.</summary>
    public string Body { get; }

    /// <summary>Gets the final URI after any provider-managed redirects.</summary>
    public Uri FinalUri { get; }
}

/// <summary>Defines a provider-neutral HTTP transport boundary.</summary>
public interface IHttpTransport
{
    /// <summary>Sends one bounded request and returns one bounded response.</summary>
    public ValueTask<HttpTransportResponse> SendAsync(HttpTransportRequest request, CancellationToken cancellationToken = default);
}
