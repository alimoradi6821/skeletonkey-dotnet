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

        if (result.Persistent && string.IsNullOrWhiteSpace(result.UserDataDirectory))
        {
            throw new ArgumentException("Persistent browser profiles require an explicit user-data directory.");
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
