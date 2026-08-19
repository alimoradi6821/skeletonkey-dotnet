using System.Text.Json;
using System.Text.Json.Nodes;

namespace SkeletonKey.Desktop.FlaUI;

/// <summary>Describes validated declarative FlaUI application constraints.</summary>
public sealed record FlaUiApplicationConstraints
{
    /// <summary>Gets whether the provider launches or attaches to an application.</summary>
    public string Mode { get; init; } = "launch";

    /// <summary>Gets the executable used by launch mode.</summary>
    public string? Executable { get; init; }

    /// <summary>Gets bounded arguments supplied without shell execution.</summary>
    public string Arguments { get; init; } = string.Empty;

    /// <summary>Gets the exact process ID used by attach mode.</summary>
    public int? ProcessId { get; init; }

    /// <summary>Gets the exact process name used by attach mode when no process ID is supplied.</summary>
    public string? ProcessName { get; init; }

    /// <summary>Gets whether disposal should close the target application.</summary>
    public bool CloseOnDispose { get; init; } = true;

    /// <summary>Gets the main-window discovery timeout.</summary>
    public int MainWindowTimeoutMilliseconds { get; init; } = 30000;

    /// <summary>Gets the default desktop locator timeout.</summary>
    public int DefaultTimeoutMilliseconds { get; init; } = 30000;

    /// <summary>Parses a closed desktop application constraint object.</summary>
    public static FlaUiApplicationConstraints Parse(JsonObject? constraints)
    {
        FlaUiApplicationConstraints result = new();
        if (constraints is null)
        {
            throw new ArgumentException("Desktop application constraints are required.");
        }

        bool closeOnDisposeSpecified = false;
        foreach (KeyValuePair<string, JsonNode?> property in constraints)
        {
            result = property.Key switch
            {
                "mode" => result with { Mode = ReadString(property.Value, property.Key, 16) },
                "executable" => result with { Executable = ReadString(property.Value, property.Key, 4096) },
                "arguments" => result with { Arguments = ReadString(property.Value, property.Key, 16384) },
                "processId" => result with { ProcessId = ReadPositiveInt(property.Value, property.Key) },
                "processName" => result with { ProcessName = ReadString(property.Value, property.Key, 260) },
                "closeOnDispose" => result with { CloseOnDispose = ReadBoolean(property.Value, property.Key) },
                "mainWindowTimeoutMilliseconds" => result with { MainWindowTimeoutMilliseconds = ReadTimeout(property.Value, property.Key) },
                "defaultTimeoutMilliseconds" => result with { DefaultTimeoutMilliseconds = ReadTimeout(property.Value, property.Key) },
                _ => throw new ArgumentException("Unknown desktop application constraint is not allowed."),
            };
            closeOnDisposeSpecified |= property.Key == "closeOnDispose";
        }

        if (result.Mode == "launch")
        {
            if (string.IsNullOrWhiteSpace(result.Executable) || result.ProcessId is not null || result.ProcessName is not null)
            {
                throw new ArgumentException("Launch mode requires executable and forbids attach selectors.");
            }
        }
        else if (result.Mode == "attach")
        {
            if (result.Executable is not null || (result.ProcessId is null) == (result.ProcessName is null) ||
                result.ProcessName is not null && (string.IsNullOrWhiteSpace(result.ProcessName) || result.ProcessName.IndexOfAny(['/', '\\']) >= 0))
            {
                throw new ArgumentException("Attach mode requires exactly one of processId or processName and forbids executable.");
            }

            if (!closeOnDisposeSpecified)
            {
                result = result with { CloseOnDispose = false };
            }
        }
        else
        {
            throw new ArgumentException("Desktop application mode must be launch or attach.");
        }

        return result;
    }

    private static string ReadString(JsonNode? value, string property, int maximumLength)
    {
        string text = value is not null && value.GetValueKind() == JsonValueKind.String
            ? value.GetValue<string>()
            : throw new ArgumentException($"Constraint '{property}' must be a string.");
        return text.Length <= maximumLength && !text.Any(char.IsControl)
            ? text
            : throw new ArgumentException($"Constraint '{property}' is invalid or exceeds its limit.");
    }

    private static int ReadPositiveInt(JsonNode? value, string property)
    {
        int number = value is not null && value.GetValueKind() == JsonValueKind.Number
            ? value.GetValue<int>()
            : throw new ArgumentException($"Constraint '{property}' must be an integer.");
        return number > 0 ? number : throw new ArgumentException($"Constraint '{property}' must be positive.");
    }

    private static int ReadTimeout(JsonNode? value, string property)
    {
        int number = ReadPositiveInt(value, property);
        return number <= 300000 ? number : throw new ArgumentException($"Constraint '{property}' must not exceed 300000 milliseconds.");
    }

    private static bool ReadBoolean(JsonNode? value, string property)
    {
        return value is not null && value.GetValueKind() is JsonValueKind.True or JsonValueKind.False
            ? value.GetValue<bool>()
            : throw new ArgumentException($"Constraint '{property}' must be a boolean.");
    }
}
