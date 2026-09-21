using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Web.Abstractions;

namespace SkeletonKey.Web.Playwright;

/// <summary>
/// Describes validated declarative Playwright page constraints.
/// </summary>
public sealed record PlaywrightPageConstraints
{
    /// <summary>Gets the selected browser engine.</summary>
    public string Engine { get; init; } = "chromium";

    /// <summary>Gets whether Playwright launches a browser or attaches over CDP.</summary>
    public string Connection { get; init; } = "launch";

    /// <summary>Gets the Chrome DevTools Protocol endpoint used by CDP attachment mode.</summary>
    public string? CdpEndpoint { get; init; }

    /// <summary>Gets an optional installed Chromium channel such as msedge or chrome.</summary>
    public string? Channel { get; init; }

    /// <summary>Gets whether the browser is headless.</summary>
    public bool Headless { get; init; } = true;

    /// <summary>Gets whether a persistent browser profile is requested.</summary>
    public bool Persistent { get; init; }

    /// <summary>Gets the explicit persistent user-data directory.</summary>
    public string? UserDataDirectory { get; init; }

    /// <summary>Gets optional viewport width.</summary>
    public int? ViewportWidth { get; init; }

    /// <summary>Gets optional viewport height.</summary>
    public int? ViewportHeight { get; init; }

    /// <summary>Gets optional locale.</summary>
    public string? Locale { get; init; }

    /// <summary>Gets optional user agent.</summary>
    public string? UserAgent { get; init; }

    /// <summary>Gets the default operation timeout in milliseconds.</summary>
    public int DefaultTimeoutMilliseconds { get; init; } = 30000;

    /// <summary>Gets the optional bounded network interception policy.</summary>
    public WebNetworkInterceptionPolicy? NetworkPolicy { get; init; }

    /// <summary>
    /// Parses provider-neutral resource constraint JSON.
    /// </summary>
    public static PlaywrightPageConstraints Parse(JsonObject? constraints)
    {
        PlaywrightPageConstraints result = new();
        if (constraints is null)
        {
            return result;
        }

        foreach (KeyValuePair<string, JsonNode?> property in constraints)
        {
            result = property.Key switch
            {
                "engine" => result.WithEngine(ReadString(property.Value, property.Key)),
                "connection" => result with { Connection = ReadString(property.Value, property.Key) },
                "cdpEndpoint" => result with { CdpEndpoint = ReadString(property.Value, property.Key) },
                "channel" => result with { Channel = ReadString(property.Value, property.Key) },
                "visibility" => result.WithVisibility(ReadString(property.Value, property.Key)),
                "profile" => result.WithProfile(ReadString(property.Value, property.Key)),
                "userDataDirectory" => result with { UserDataDirectory = ReadString(property.Value, property.Key) },
                "viewportWidth" => result with { ViewportWidth = ReadPositiveInt(property.Value, property.Key) },
                "viewportHeight" => result with { ViewportHeight = ReadPositiveInt(property.Value, property.Key) },
                "locale" => result with { Locale = ReadString(property.Value, property.Key) },
                "userAgent" => result with { UserAgent = ReadString(property.Value, property.Key) },
                "defaultTimeoutMilliseconds" => result with { DefaultTimeoutMilliseconds = ReadBoundedTimeout(property.Value, property.Key) },
                "network" => result with { NetworkPolicy = PlaywrightNetworkPolicyParser.Parse(property.Value) },
                _ => throw new ArgumentException("Unknown browser resource constraint is not allowed."),
            };
        }

        if (result.Engine is not ("chromium" or "firefox" or "webkit"))
        {
            throw new ArgumentException("Browser engine must be chromium, firefox, or webkit.");
        }

        if (result.Connection is not ("launch" or "cdp" or "cdp-managed"))
        {
            throw new ArgumentException("Browser connection must be launch, cdp, or cdp-managed.");
        }

        if (!string.IsNullOrWhiteSpace(result.Channel) && result.Engine != "chromium")
        {
            throw new ArgumentException("Browser channels are supported only with the chromium engine.");
        }

        if (result.Connection == "cdp")
        {
            if (result.Engine != "chromium")
            {
                throw new ArgumentException("CDP attachment is supported only with the chromium engine.");
            }

            if (string.IsNullOrWhiteSpace(result.CdpEndpoint))
            {
                throw new ArgumentException("CDP attachment requires an explicit cdpEndpoint.");
            }

            ValidateCdpEndpoint(result.CdpEndpoint);
            if (constraints.ContainsKey("channel") ||
                constraints.ContainsKey("visibility") ||
                constraints.ContainsKey("profile") ||
                constraints.ContainsKey("userDataDirectory") ||
                constraints.ContainsKey("viewportWidth") ||
                constraints.ContainsKey("viewportHeight") ||
                constraints.ContainsKey("locale") ||
                constraints.ContainsKey("userAgent"))
            {
                throw new ArgumentException("CDP attachment cannot use browser launch or context-creation constraints.");
            }

            if (result.NetworkPolicy is not null)
            {
                throw new ArgumentException("CDP attachment does not support declarative network interception.");
            }
        }
        else if (result.Connection == "cdp-managed")
        {
            if (result.Engine != "chromium")
            {
                throw new ArgumentException("Managed CDP is supported only with the chromium engine.");
            }

            if (!string.IsNullOrWhiteSpace(result.CdpEndpoint))
            {
                throw new ArgumentException("Managed CDP chooses a loopback endpoint automatically and does not accept cdpEndpoint.");
            }

            if (result.Channel is not ("msedge" or "chrome"))
            {
                throw new ArgumentException("Managed CDP requires channel msedge or chrome.");
            }

            if (!result.Persistent || string.IsNullOrWhiteSpace(result.UserDataDirectory))
            {
                throw new ArgumentException("Managed CDP requires a persistent profile and explicit user-data directory.");
            }

            if (constraints.ContainsKey("viewportWidth") ||
                constraints.ContainsKey("viewportHeight") ||
                constraints.ContainsKey("locale") ||
                constraints.ContainsKey("userAgent"))
            {
                throw new ArgumentException("Managed CDP does not accept context-creation constraints.");
            }

            if (result.NetworkPolicy is not null)
            {
                throw new ArgumentException("Managed CDP does not support declarative network interception.");
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(result.CdpEndpoint))
            {
                throw new ArgumentException("cdpEndpoint is valid only when connection is cdp.");
            }

            if (result.Persistent && string.IsNullOrWhiteSpace(result.UserDataDirectory))
            {
                throw new ArgumentException("Persistent browser profiles require an explicit user-data directory.");
            }
        }

        return result;
    }

    private PlaywrightPageConstraints WithEngine(string engine)
    {
        return this with { Engine = engine };
    }

    private PlaywrightPageConstraints WithVisibility(string visibility)
    {
        return visibility switch
        {
            "any" or "headless" => this with { Headless = true },
            "headful" => this with { Headless = false },
            _ => throw new ArgumentException("Browser visibility must be any, headless, or headful."),
        };
    }

    private PlaywrightPageConstraints WithProfile(string profile)
    {
        return profile switch
        {
            "any" or "ephemeral" => this with { Persistent = false },
            "persistent" => this with { Persistent = true },
            _ => throw new ArgumentException("Browser profile must be any, ephemeral, or persistent."),
        };
    }

    private static void ValidateCdpEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme is not ("http" or "https" or "ws" or "wss"))
        {
            throw new ArgumentException("cdpEndpoint must be an absolute HTTP(S) or WebSocket URL.");
        }
    }

    private static string ReadString(JsonNode? value, string property)
    {
        return value is not null && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : throw new ArgumentException($"Constraint '{property}' must be a string.");
    }

    private static int ReadPositiveInt(JsonNode? value, string property)
    {
        int number = value is not null && value.GetValueKind() == JsonValueKind.Number ? value.GetValue<int>() : throw new ArgumentException($"Constraint '{property}' must be an integer.");
        return number > 0 ? number : throw new ArgumentException($"Constraint '{property}' must be positive.");
    }

    private static int ReadBoundedTimeout(JsonNode? value, string property)
    {
        int number = ReadPositiveInt(value, property);
        return number <= 300000 ? number : throw new ArgumentException($"Constraint '{property}' must be bounded.");
    }
}