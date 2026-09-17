using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using SkeletonKey.Http.Abstractions;
using NetHttpClient = System.Net.Http.HttpClient;

namespace SkeletonKey.Http.HttpClientProvider;

/// <summary>Implements the generic HTTP transport with <see cref="NetHttpClient" />.</summary>
public sealed class SystemHttpTransport : IHttpTransport, IDisposable
{
    private readonly NetHttpClient _redirectClient;
    private readonly NetHttpClient _noRedirectClient;
    private bool _disposed;

    /// <summary>Initializes a transport with bounded redirect and connection behavior.</summary>
    public SystemHttpTransport()
    {
        _redirectClient = CreateClient(allowAutoRedirect: true);
        _noRedirectClient = CreateClient(allowAutoRedirect: false);
    }

    /// <inheritdoc />
    public async ValueTask<HttpTransportResponse> SendAsync(HttpTransportRequest request, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        using CancellationTokenSource timeout = new(request.TimeoutMilliseconds);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            using HttpRequestMessage message = new(new HttpMethod(request.Method), request.Uri);
            if (request.Body is not null)
            {
                message.Content = new StringContent(request.Body, Encoding.UTF8, request.ContentType ?? "text/plain");
            }

            foreach (KeyValuePair<string, string> header in request.Headers)
            {
                if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase) && message.Content is not null)
                {
                    message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(header.Value);
                    continue;
                }

                if (!message.Headers.TryAddWithoutValidation(header.Key, header.Value) &&
                    (message.Content is null || !message.Content.Headers.TryAddWithoutValidation(header.Key, header.Value)))
                {
                    throw new HttpCapabilityException(HttpCapabilityErrorCodes.InvalidRequest, $"HTTP header '{header.Key}' could not be applied.");
                }
            }

            NetHttpClient client = request.FollowRedirects ? _redirectClient : _noRedirectClient;
            using HttpResponseMessage response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
            if (response.Content.Headers.ContentLength is long declaredLength && declaredLength > request.MaximumResponseBytes)
            {
                throw new HttpCapabilityException(HttpCapabilityErrorCodes.ResponseTooLarge, "HTTP response body exceeded the configured maximum size.");
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);
            using MemoryStream buffer = new(Math.Min(request.MaximumResponseBytes, 8192));
            byte[] chunk = new byte[8192];
            while (true)
            {
                int read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), linked.Token).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                if (buffer.Length + read > request.MaximumResponseBytes)
                {
                    throw new HttpCapabilityException(HttpCapabilityErrorCodes.ResponseTooLarge, "HTTP response body exceeded the configured maximum size.");
                }

                await buffer.WriteAsync(chunk.AsMemory(0, read), linked.Token).ConfigureAwait(false);
            }

            string body = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, checked((int)buffer.Length));
            Dictionary<string, IReadOnlyList<string>> headers = new(StringComparer.OrdinalIgnoreCase);
            CopyHeaders(response.Headers, headers);
            CopyHeaders(response.Content.Headers, headers);
            Uri finalUri = response.RequestMessage?.RequestUri ?? request.Uri;
            return new HttpTransportResponse((int)response.StatusCode, headers, body, finalUri);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (timeout.IsCancellationRequested)
        {
            throw new HttpCapabilityException(HttpCapabilityErrorCodes.RequestTimeout, "HTTP request exceeded the configured timeout.", exception);
        }
        catch (HttpCapabilityException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            throw new HttpCapabilityException(HttpCapabilityErrorCodes.TransportFailure, "HTTP transport failed.", exception);
        }
        catch (FormatException exception)
        {
            throw new HttpCapabilityException(HttpCapabilityErrorCodes.InvalidRequest, "HTTP request contained an invalid header value.", exception);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _redirectClient.Dispose();
        _noRedirectClient.Dispose();
    }

    private static NetHttpClient CreateClient(bool allowAutoRedirect)
    {
        HttpClientHandler handler = new()
        {
            AllowAutoRedirect = allowAutoRedirect,
            AutomaticDecompression = DecompressionMethods.All,
            MaxAutomaticRedirections = 10,
            UseCookies = false,
        };
        return new NetHttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    private static void CopyHeaders(HttpHeaders source, IDictionary<string, IReadOnlyList<string>> destination)
    {
        foreach (KeyValuePair<string, IEnumerable<string>> header in source)
        {
            destination[header.Key] = new ReadOnlyCollection<string>([.. header.Value]);
        }
    }
}
