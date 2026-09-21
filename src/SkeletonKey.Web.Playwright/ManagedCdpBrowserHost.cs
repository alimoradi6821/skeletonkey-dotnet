using System.Diagnostics;
using System.Net;

namespace SkeletonKey.Web.Playwright;

/// <summary>
/// Owns Chromium-family processes that expose loopback-only CDP endpoints and may outlive one workflow execution.
/// </summary>
public sealed class ManagedCdpBrowserHost : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, ManagedBrowserEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    internal async ValueTask<string> GetOrStartEndpointAsync(
        PlaywrightPageConstraints constraints,
        int startupTimeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(constraints);
        ObjectDisposedException.ThrowIf(_disposed, this);

        string profileDirectory = Path.GetFullPath(Environment.ExpandEnvironmentVariables(constraints.UserDataDirectory!));
        string key = constraints.Channel + "|" + profileDirectory + "|" + (constraints.Headless ? "headless" : "headful");

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_entries.TryGetValue(key, out ManagedBrowserEntry? existing))
            {
                if (await IsEndpointReadyAsync(existing.Endpoint, cancellationToken).ConfigureAwait(false))
                {
                    return existing.Endpoint;
                }

                await StopOwnedProcessAsync(existing).ConfigureAwait(false);
                _entries.Remove(key);
            }

            Directory.CreateDirectory(profileDirectory);
            string? reusableEndpoint = await TryReadReadyEndpointAsync(profileDirectory, cancellationToken).ConfigureAwait(false);
            if (reusableEndpoint is not null)
            {
                _entries[key] = new ManagedBrowserEntry(reusableEndpoint, null, false);
                return reusableEndpoint;
            }

            string executable = ResolveBrowserExecutable(constraints.Channel!);
            string activePortPath = Path.Combine(profileDirectory, "DevToolsActivePort");
            TryDelete(activePortPath);

            ProcessStartInfo start = new()
            {
                FileName = executable,
                UseShellExecute = false,
                CreateNoWindow = constraints.Headless,
            };
            start.ArgumentList.Add("--remote-debugging-address=127.0.0.1");
            start.ArgumentList.Add("--remote-debugging-port=0");
            start.ArgumentList.Add("--user-data-dir=" + profileDirectory);
            start.ArgumentList.Add("--no-first-run");
            start.ArgumentList.Add("--no-default-browser-check");
            if (constraints.Headless)
            {
                start.ArgumentList.Add("--headless=new");
            }

            start.ArgumentList.Add("about:blank");
            Process process = Process.Start(start)
                ?? throw new InvalidOperationException("Managed CDP browser process could not be started.");

            string endpoint;
            try
            {
                endpoint = await WaitForEndpointAsync(
                    profileDirectory,
                    process,
                    startupTimeoutMilliseconds,
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await StopProcessAsync(process).ConfigureAwait(false);
                throw;
            }

            _entries[key] = new ManagedBrowserEntry(endpoint, process, true);
            return endpoint;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (ManagedBrowserEntry entry in _entries.Values)
            {
                await StopOwnedProcessAsync(entry).ConfigureAwait(false);
            }

            _entries.Clear();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private static async ValueTask<string> WaitForEndpointAsync(
        string profileDirectory,
        Process process,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMilliseconds);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process.HasExited)
            {
                throw new InvalidOperationException("Managed CDP browser exited before its debugging endpoint became ready.");
            }

            string? endpoint = await TryReadReadyEndpointAsync(profileDirectory, cancellationToken).ConfigureAwait(false);
            if (endpoint is not null)
            {
                return endpoint;
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("Managed CDP browser did not expose a debugging endpoint before timeout.");
    }

    private static async ValueTask<string?> TryReadReadyEndpointAsync(string profileDirectory, CancellationToken cancellationToken)
    {
        string activePortPath = Path.Combine(profileDirectory, "DevToolsActivePort");
        string[] lines;
        try
        {
            if (!File.Exists(activePortPath))
            {
                return null;
            }

            lines = await File.ReadAllLinesAsync(activePortPath, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        if (lines.Length == 0 ||
            !int.TryParse(lines[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int port) ||
            port is <= 0 or > 65535)
        {
            return null;
        }

        string endpoint = "http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return await IsEndpointReadyAsync(endpoint, cancellationToken).ConfigureAwait(false) ? endpoint : null;
    }

    private static async ValueTask<bool> IsEndpointReadyAsync(string endpoint, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(1),
            };
            using HttpResponseMessage response = await client.GetAsync(endpoint + "/json/version", cancellationToken).ConfigureAwait(false);
            return response.StatusCode == HttpStatusCode.OK;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return false;
        }
    }

    private static string ResolveBrowserExecutable(string channel)
    {
        IEnumerable<string> candidates = channel switch
        {
            "msedge" => EdgeCandidates(),
            "chrome" => ChromeCandidates(),
            _ => [],
        };

        foreach (string candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
            {
                return candidate;
            }
        }

        string? pathExecutable = FindOnPath(channel == "msedge" ? EdgeExecutableNames() : ChromeExecutableNames());
        if (pathExecutable is not null)
        {
            return pathExecutable;
        }

        throw new FileNotFoundException("Managed CDP browser executable was not found for channel '" + channel + "'.");
    }

    private static IEnumerable<string> EdgeCandidates()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe");
            yield return Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe");
            yield return Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "Application", "msedge.exe");
        }
        else if (OperatingSystem.IsMacOS())
        {
            yield return "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge";
        }
    }

    private static IEnumerable<string> ChromeCandidates()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe");
            yield return Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe");
            yield return Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe");
        }
        else if (OperatingSystem.IsMacOS())
        {
            yield return "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
        }
    }

    private static IReadOnlyList<string> EdgeExecutableNames()
    {
        return OperatingSystem.IsWindows()
            ? ["msedge.exe"]
            : ["microsoft-edge", "microsoft-edge-stable"];
    }

    private static IReadOnlyList<string> ChromeExecutableNames()
    {
        return OperatingSystem.IsWindows()
            ? ["chrome.exe"]
            : ["google-chrome", "google-chrome-stable", "chromium", "chromium-browser"];
    }

    private static string? FindOnPath(IReadOnlyList<string> names)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (string name in names)
            {
                string candidate = Path.Combine(directory, name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static string Combine(string root, params string[] segments)
    {
        return string.IsNullOrWhiteSpace(root) ? string.Empty : Path.Combine([root, .. segments]);
    }

    private static async ValueTask StopOwnedProcessAsync(ManagedBrowserEntry entry)
    {
        if (entry.OwnsProcess && entry.Process is not null)
        {
            await StopProcessAsync(entry.Process).ConfigureAwait(false);
        }
    }

    private static async ValueTask StopProcessAsync(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                _ = process.CloseMainWindow();
                using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try
                {
                    await process.WaitForExitAsync(shutdown.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync().ConfigureAwait(false);
                    }
                }
            }
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            process.Dispose();
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record ManagedBrowserEntry(string Endpoint, Process? Process, bool OwnsProcess);
}
