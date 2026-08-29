using System.Globalization;
using System.Text.Json;
using System.Xml;

namespace SkeletonKey.Runner.Core;

/// <summary>Supported standalone host schedule kinds.</summary>
public enum StandaloneScheduleKind
{
    /// <summary>Execute exactly one occurrence and exit.</summary>
    Once,

    /// <summary>Execute on a fixed elapsed-time cadence.</summary>
    Interval,

    /// <summary>Execute once per local calendar day.</summary>
    Daily,
}

/// <summary>Supported overlap behavior for standalone schedules.</summary>
public enum StandaloneOverlapPolicy
{
    /// <summary>Do not start occurrences whose due boundary passed while an earlier occurrence was still active.</summary>
    Skip,
}

/// <summary>A validated standalone schedule.</summary>
public sealed record StandaloneSchedule(StandaloneScheduleKind Kind, TimeSpan? Interval, TimeOnly? DailyTime);

/// <summary>Host-level execution behavior that remains outside the workflow document.</summary>
public sealed record StandaloneExecutionPolicy(bool RunImmediately, StandaloneOverlapPolicy Overlap, bool ContinueAfterFailure);

/// <summary>Strict versioned execution settings for a sealed standalone application.</summary>
public sealed record StandaloneExecutionSettings(string SpecVersion, StandaloneSchedule Schedule, StandaloneExecutionPolicy Execution)
{
    /// <summary>The only settings version currently supported.</summary>
    public const string CurrentSpecVersion = "0.1";

    /// <summary>Minimum accepted recurring interval.</summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(1);

    /// <summary>Maximum accepted recurring interval.</summary>
    public static readonly TimeSpan MaximumInterval = TimeSpan.FromDays(365);

    /// <summary>Parses and strictly validates an execution-settings document.</summary>
    public static StandaloneExecutionSettings Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 16,
            });
        }
        catch (JsonException exception)
        {
            throw new StandaloneSettingsException("SKX1001", "Execution settings are not valid JSON.", exception);
        }

        using (document)
        {
            JsonElement root = RequireObject(document.RootElement, "$", "SKX1002");
            RejectUnknownProperties(root, "$", ["specVersion", "schedule", "execution"]);

            string specVersion = RequireString(root, "specVersion", "$", "SKX1003");
            if (!string.Equals(specVersion, CurrentSpecVersion, StringComparison.Ordinal))
            {
                throw new StandaloneSettingsException("SKX1004", $"Unsupported standalone settings specVersion '{specVersion}'.");
            }

            if (!root.TryGetProperty("schedule", out JsonElement scheduleElement))
            {
                throw new StandaloneSettingsException("SKX1005", "Execution settings require $.schedule.");
            }

            StandaloneSchedule schedule = ParseSchedule(RequireObject(scheduleElement, "$.schedule", "SKX1006"));
            StandaloneExecutionPolicy execution = root.TryGetProperty("execution", out JsonElement executionElement)
                ? ParseExecution(RequireObject(executionElement, "$.execution", "SKX1007"))
                : new StandaloneExecutionPolicy(false, StandaloneOverlapPolicy.Skip, true);

            return new StandaloneExecutionSettings(specVersion, schedule, execution);
        }
    }

    private static StandaloneSchedule ParseSchedule(JsonElement schedule)
    {
        RejectUnknownProperties(schedule, "$.schedule", ["type", "interval", "time"]);
        string type = RequireString(schedule, "type", "$.schedule", "SKX1010");

        return type switch
        {
            "once" => ParseOnce(schedule),
            "interval" => ParseInterval(schedule),
            "daily" => ParseDaily(schedule),
            _ => throw new StandaloneSettingsException("SKX1011", "$.schedule.type must be once, interval, or daily."),
        };
    }

    private static StandaloneSchedule ParseOnce(JsonElement schedule)
    {
        RejectPresent(schedule, "interval", "SKX1012", "$.schedule.interval is not valid for a once schedule.");
        RejectPresent(schedule, "time", "SKX1013", "$.schedule.time is not valid for a once schedule.");
        return new StandaloneSchedule(StandaloneScheduleKind.Once, null, null);
    }

    private static StandaloneSchedule ParseInterval(JsonElement schedule)
    {
        RejectPresent(schedule, "time", "SKX1014", "$.schedule.time is not valid for an interval schedule.");
        string text = RequireString(schedule, "interval", "$.schedule", "SKX1015");
        int timeSeparator = text.IndexOf('T');
        string datePart = timeSeparator < 0 ? text : text[..timeSeparator];
        if (datePart.Contains('Y') || (datePart.Length > 1 && datePart.AsSpan(1).Contains('M')))
        {
            throw new StandaloneSettingsException("SKX1025", "$.schedule.interval must be a fixed duration and cannot contain calendar years or months.");
        }

        TimeSpan interval;
        try
        {
            interval = XmlConvert.ToTimeSpan(text);
        }
        catch (Exception exception) when (exception is FormatException or OverflowException)
        {
            throw new StandaloneSettingsException("SKX1016", "$.schedule.interval must be an ISO-8601 duration.", exception);
        }

        if (interval < MinimumInterval || interval > MaximumInterval)
        {
            throw new StandaloneSettingsException(
                "SKX1017",
                $"$.schedule.interval must be between {XmlConvert.ToString(MinimumInterval)} and {XmlConvert.ToString(MaximumInterval)}.");
        }

        return new StandaloneSchedule(StandaloneScheduleKind.Interval, interval, null);
    }

    private static StandaloneSchedule ParseDaily(JsonElement schedule)
    {
        RejectPresent(schedule, "interval", "SKX1018", "$.schedule.interval is not valid for a daily schedule.");
        string text = RequireString(schedule, "time", "$.schedule", "SKX1019");
        if (!TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly time))
        {
            throw new StandaloneSettingsException("SKX1020", "$.schedule.time must use exact HH:mm local-wall-clock form.");
        }

        return new StandaloneSchedule(StandaloneScheduleKind.Daily, null, time);
    }

    private static StandaloneExecutionPolicy ParseExecution(JsonElement execution)
    {
        RejectUnknownProperties(execution, "$.execution", ["runImmediately", "overlap", "continueAfterFailure"]);

        bool runImmediately = OptionalBoolean(execution, "runImmediately", false, "SKX1021");
        bool continueAfterFailure = OptionalBoolean(execution, "continueAfterFailure", true, "SKX1022");
        string overlap = OptionalString(execution, "overlap", "skip", "SKX1023");
        if (!string.Equals(overlap, "skip", StringComparison.Ordinal))
        {
            throw new StandaloneSettingsException("SKX1024", "$.execution.overlap currently supports only 'skip'.");
        }

        return new StandaloneExecutionPolicy(runImmediately, StandaloneOverlapPolicy.Skip, continueAfterFailure);
    }

    private static JsonElement RequireObject(JsonElement element, string path, string code)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new StandaloneSettingsException(code, path + " must be a JSON object.");
        }

        return element;
    }

    private static string RequireString(JsonElement obj, string propertyName, string path, string code)
    {
        if (!obj.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind != JsonValueKind.String)
        {
            throw new StandaloneSettingsException(code, path + "." + propertyName + " must be a string.");
        }

        string? text = value.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new StandaloneSettingsException(code, path + "." + propertyName + " cannot be empty.");
        }

        return text;
    }

    private static string OptionalString(JsonElement obj, string propertyName, string defaultValue, string code)
    {
        if (!obj.TryGetProperty(propertyName, out JsonElement value))
        {
            return defaultValue;
        }

        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new StandaloneSettingsException(code, "$.execution." + propertyName + " must be a non-empty string.");
        }

        return value.GetString()!;
    }

    private static bool OptionalBoolean(JsonElement obj, string propertyName, bool defaultValue, string code)
    {
        if (!obj.TryGetProperty(propertyName, out JsonElement value))
        {
            return defaultValue;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new StandaloneSettingsException(code, "$.execution." + propertyName + " must be a boolean."),
        };
    }

    private static void RejectUnknownProperties(JsonElement obj, string path, IReadOnlyCollection<string> allowed)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in obj.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                throw new StandaloneSettingsException("SKX1009", $"Duplicate standalone settings property: {path}.{property.Name}.");
            }

            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
            {
                throw new StandaloneSettingsException("SKX1008", $"Unknown standalone settings property: {path}.{property.Name}.");
            }
        }
    }

    private static void RejectPresent(JsonElement obj, string propertyName, string code, string message)
    {
        if (obj.TryGetProperty(propertyName, out _))
        {
            throw new StandaloneSettingsException(code, message);
        }
    }
}

/// <summary>Thrown when standalone execution settings violate the 0.1 host contract.</summary>
public sealed class StandaloneSettingsException : Exception
{
    /// <summary>Initializes a settings exception with a stable diagnostic code.</summary>
    public StandaloneSettingsException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>Stable standalone settings diagnostic code.</summary>
    public string Code { get; }
}
