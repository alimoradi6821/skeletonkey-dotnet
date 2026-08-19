using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Web.Abstractions;

namespace SkeletonKey.Web.Playwright;

internal static class PlaywrightNetworkPolicyParser
{
    public static WebNetworkInterceptionPolicy Parse(JsonNode? value)
    {
        if (value is not JsonObject policy)
        {
            throw new ArgumentException("Network interception constraint must be an object.");
        }

        EnsureClosed(policy, ["defaultAction", "maximumInterceptions", "rules"], "network policy");
        WebNetworkInterceptionAction defaultAction = policy["defaultAction"] is null
            ? WebNetworkInterceptionAction.Allow
            : ReadAction(policy["defaultAction"], allowModifyAndFulfill: false);
        int maximumInterceptions = policy["maximumInterceptions"] is null ? 10_000 : ReadInteger(policy["maximumInterceptions"], "maximumInterceptions");
        List<WebNetworkInterceptionRule> rules = [];
        if (policy["rules"] is not null)
        {
            if (policy["rules"] is not JsonArray ruleArray)
            {
                throw new ArgumentException("Network rules must be an array.");
            }

            foreach (JsonNode? ruleNode in ruleArray)
            {
                rules.Add(ReadRule(ruleNode));
            }
        }

        return new WebNetworkInterceptionPolicy(rules, defaultAction, maximumInterceptions);
    }

    private static WebNetworkInterceptionRule ReadRule(JsonNode? value)
    {
        if (value is not JsonObject rule)
        {
            throw new ArgumentException("Each network rule must be an object.");
        }

        EnsureClosed(
            rule,
            ["id", "urlPattern", "action", "methods", "resourceTypes", "setRequestHeaders", "removeRequestHeaders", "status", "contentType", "body", "responseHeaders"],
            "network rule");
        return new WebNetworkInterceptionRule(
            ReadRequiredString(rule, "id"),
            ReadRequiredString(rule, "urlPattern"),
            ReadAction(rule["action"], allowModifyAndFulfill: true),
            ReadStringArray(rule["methods"], "methods"),
            ReadStringArray(rule["resourceTypes"], "resourceTypes"),
            ReadHeaders(rule["setRequestHeaders"], "setRequestHeaders"),
            ReadStringArray(rule["removeRequestHeaders"], "removeRequestHeaders"),
            rule["status"] is null ? null : ReadInteger(rule["status"], "status"),
            ReadOptionalString(rule["contentType"], "contentType"),
            ReadOptionalString(rule["body"], "body"),
            ReadHeaders(rule["responseHeaders"], "responseHeaders"));
    }

    private static WebNetworkInterceptionAction ReadAction(JsonNode? value, bool allowModifyAndFulfill)
    {
        string action = ReadString(value, "action");
        return action switch
        {
            "allow" => WebNetworkInterceptionAction.Allow,
            "block" => WebNetworkInterceptionAction.Block,
            "modify" when allowModifyAndFulfill => WebNetworkInterceptionAction.Modify,
            "fulfill" when allowModifyAndFulfill => WebNetworkInterceptionAction.Fulfill,
            _ => throw new ArgumentException("Network interception action is invalid."),
        };
    }

    private static IReadOnlyList<string>? ReadStringArray(JsonNode? value, string property)
    {
        if (value is null)
        {
            return null;
        }

        if (value is not JsonArray array)
        {
            throw new ArgumentException($"Network rule property '{property}' must be an array.");
        }

        List<string> result = [];
        foreach (JsonNode? item in array)
        {
            result.Add(ReadString(item, property));
        }

        return result.AsReadOnly();
    }

    private static IReadOnlyDictionary<string, string>? ReadHeaders(JsonNode? value, string property)
    {
        if (value is null)
        {
            return null;
        }

        if (value is not JsonObject headers)
        {
            throw new ArgumentException($"Network rule property '{property}' must be an object.");
        }

        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, JsonNode?> header in headers)
        {
            if (!result.TryAdd(header.Key, ReadString(header.Value, property)))
            {
                throw new ArgumentException($"Network rule property '{property}' contains duplicate headers.");
            }
        }

        return result;
    }

    private static string ReadRequiredString(JsonObject value, string property)
    {
        return value[property] is null ? throw new ArgumentException($"Network rule property '{property}' is required.") : ReadString(value[property], property);
    }

    private static string? ReadOptionalString(JsonNode? value, string property)
    {
        return value is null ? null : ReadString(value, property);
    }

    private static string ReadString(JsonNode? value, string property)
    {
        return value is not null && value.GetValueKind() == JsonValueKind.String
            ? value.GetValue<string>()
            : throw new ArgumentException($"Network property '{property}' must be a string.");
    }

    private static int ReadInteger(JsonNode? value, string property)
    {
        return value is not null && value.GetValueKind() == JsonValueKind.Number
            ? value.GetValue<int>()
            : throw new ArgumentException($"Network property '{property}' must be an integer.");
    }

    private static void EnsureClosed(JsonObject value, IReadOnlyList<string> allowed, string subject)
    {
        foreach (string property in value.Select(static pair => pair.Key))
        {
            if (!allowed.Contains(property, StringComparer.Ordinal))
            {
                throw new ArgumentException($"Unknown {subject} property '{property}' is not allowed.");
            }
        }
    }
}
