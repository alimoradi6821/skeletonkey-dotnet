using System.Net;
using System.Net.Sockets;
using System.Text;
using SkeletonKey.Http.Abstractions;
using SkeletonKey.Http.HttpClientProvider;

namespace SkeletonKey.Runner.Core.Tests;

/// <summary>Exercises the concrete Phase 1 HTTP transport against a local protocol peer.</summary>
public sealed class Phase1HttpTransportIntegrationTests
{
    /// <summary>Verifies a bounded JSON POST round-trips through the concrete transport.</summary>
    [Fact]
    public async Task SystemHttpTransportPostsJsonToLocalServer()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task<string> server = ServeOneAsync(listener, respond: true);

        using SystemHttpTransport transport = new();
        HttpTransportRequest request = new(
            "POST",
            new Uri($"http://127.0.0.1:{port}/respond", UriKind.Absolute),
            body: "{\"message\":\"hello\"}",
            contentType: "application/json",
            timeoutMilliseconds: 5000,
            maximumResponseBytes: 4096);
        HttpTransportResponse response = await transport.SendAsync(request);

        string received = await server;
        Assert.Contains("POST /respond HTTP/", received, StringComparison.Ordinal);
        Assert.Contains("{\"message\":\"hello\"}", received, StringComparison.Ordinal);
        Assert.Equal(200, response.StatusCode);
        Assert.Equal("{\"answer\":\"ok\"}", response.Body);
    }

    /// <summary>Verifies caller cancellation interrupts an in-flight request.</summary>
    [Fact]
    public async Task SystemHttpTransportHonorsCallerCancellation()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task<string> server = ServeOneAsync(listener, respond: false);

        using SystemHttpTransport transport = new();
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(250));
        HttpTransportRequest request = new(
            "GET",
            new Uri($"http://127.0.0.1:{port}/wait", UriKind.Absolute),
            timeoutMilliseconds: 5000,
            maximumResponseBytes: 4096);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await transport.SendAsync(request, cancellation.Token).AsTask());
        _ = await server;
    }

    private static async Task<string> ServeOneAsync(TcpListener listener, bool respond)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync();
        await using NetworkStream stream = client.GetStream();
        string request = await ReadRequestAsync(stream);
        if (respond)
        {
            byte[] body = Encoding.UTF8.GetBytes("{\"answer\":\"ok\"}");
            byte[] response = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\n" +
                "Content-Type: application/json\r\n" +
                "Content-Length: " + body.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\r\n" +
                "Connection: close\r\n\r\n");
            await stream.WriteAsync(response);
            await stream.WriteAsync(body);
        }
        else
        {
            await Task.Delay(1000);
        }

        return request;
    }

    private static async Task<string> ReadRequestAsync(NetworkStream stream)
    {
        using MemoryStream buffer = new();
        byte[] chunk = new byte[1024];
        int headerEnd = -1;
        int contentLength = 0;
        while (true)
        {
            int read = await stream.ReadAsync(chunk);
            if (read == 0)
            {
                break;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read));
            string snapshot = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, checked((int)buffer.Length));
            if (headerEnd < 0)
            {
                headerEnd = snapshot.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (headerEnd >= 0)
                {
                    foreach (string line in snapshot[..headerEnd].Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        {
                            contentLength = int.Parse(line["Content-Length:".Length..].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                        }
                    }
                }
            }

            if (headerEnd >= 0 && buffer.Length >= headerEnd + 4 + contentLength)
            {
                return snapshot;
            }
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
