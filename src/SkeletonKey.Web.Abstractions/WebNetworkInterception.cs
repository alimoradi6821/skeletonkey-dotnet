using System.Collections.ObjectModel;
using System.Text;

namespace SkeletonKey.Web.Abstractions;

/// <summary>Defines one deterministic network interception action.</summary>
public enum WebNetworkInterceptionAction
{
    /// <summary>Continues the request unchanged.</summary>
    Allow,

    /// <summary>Aborts the request before network dispatch.</summary>
    Block,

    /// <summary>Continues the request with bounded header changes.</summary>
    Modify,

    /// <summary>Returns a bounded synthetic response without network dispatch.</summary>
    Fulfill,
}

/// <summary>Describes one provider-neutral intercepted request.</summary>
public sealed class WebNetworkRequest
{
    /// <summary>Initializes intercepted request metadata.</summary>
    public WebNetworkRequest(string method, string url, string resourceType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceType);
        Method = method.ToUpperInvariant();
        Url = url;
        ResourceType = resourceType.ToLowerInvariant();
    }

    /// <summary>Gets the normalized HTTP method.</summary>
    public string Method { get; }

    /// <summary>Gets the absolute request URL.</summary>
    public string Url { get; }

    /// <summary>Gets the provider-neutral resource type.</summary>
    public string ResourceType { get; }
}

/// <summary>Defines one ordered, bounded network interception rule.</summary>
public sealed class WebNetworkInterceptionRule
{
    private static readonly HashSet<string> _resourceTypes = new(StringComparer.Ordinal)
    {
        "document", "stylesheet", "image", "media", "font", "script", "texttrack", "xhr", "fetch", "eventsource", "websocket", "manifest", "other",
    };
    private static readonly HashSet<string> _forbiddenRequestHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization", "content-length", "cookie", "host", "proxy-authorization",
    };
    private static readonly HashSet<string> _forbiddenResponseHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "content-length", "set-cookie",
    };
    private readonly IReadOnlyDictionary<string, string> _requestHeaders;
    private readonly IReadOnlyList<string> _removedRequestHeaders;
    private readonly IReadOnlyDictionary<string, string> _responseHeaders;

    /// <summary>Initializes one interception rule.</summary>
    public WebNetworkInterceptionRule(
        string id,
        string urlPattern,
        WebNetworkInterceptionAction action,
        IReadOnlyList<string>? methods = null,
        IReadOnlyList<string>? resourceTypes = null,
        IReadOnlyDictionary<string, string>? requestHeaders = null,
        IReadOnlyList<string>? removeRequestHeaders = null,
        int? responseStatus = null,
        string? responseContentType = null,
        string? responseBody = null,
        IReadOnlyDictionary<string, string>? responseHeaders = null)
    {
        if (!IsIdentifier(id))
        {
            throw new ArgumentException("Network rule identifier is invalid.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(urlPattern) || urlPattern.Length > 2048)
        {
            throw new ArgumentException("Network rule URL pattern is invalid.", nameof(urlPattern));
        }

        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action), action, "Network rule action is invalid.");
        }

        Id = id;
        UrlPattern = urlPattern;
        Action = action;
        Methods = CopyMethods(methods);
        ResourceTypes = CopyResourceTypes(resourceTypes);
        _requestHeaders = CopyHeaders(requestHeaders, _forbiddenRequestHeaders, nameof(requestHeaders));
        _removedRequestHeaders = CopyRemovedHeaders(removeRequestHeaders);
        _responseHeaders = CopyHeaders(responseHeaders, _forbiddenResponseHeaders, nameof(responseHeaders));
        ResponseStatus = responseStatus ?? 200;
        ResponseContentType = ValidateText(responseContentType, 256, nameof(responseContentType));
        ResponseBody = ValidateBody(responseBody);
        if (ResponseContentType is not null && _responseHeaders.ContainsKey("content-type"))
        {
            throw new ArgumentException("Synthetic response content type must be declared only once.", nameof(responseHeaders));
        }

        ValidateActionShape(responseStatus);
    }

    /// <summary>Gets the stable rule identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the bounded wildcard URL pattern.</summary>
    public string UrlPattern { get; }

    /// <summary>Gets the rule action.</summary>
    public WebNetworkInterceptionAction Action { get; }

    /// <summary>Gets optional normalized HTTP methods.</summary>
    public IReadOnlyList<string> Methods { get; }

    /// <summary>Gets optional normalized resource types.</summary>
    public IReadOnlyList<string> ResourceTypes { get; }

    /// <summary>Gets request headers to set for a modify action.</summary>
    public IReadOnlyDictionary<string, string> RequestHeaders => new ReadOnlyDictionary<string, string>(
        _requestHeaders.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase));

    /// <summary>Gets request header names to remove for a modify action.</summary>
    public IReadOnlyList<string> RemovedRequestHeaders => Array.AsReadOnly([.. _removedRequestHeaders]);

    /// <summary>Gets the synthetic response status.</summary>
    public int ResponseStatus { get; }

    /// <summary>Gets the optional synthetic response content type.</summary>
    public string? ResponseContentType { get; }

    /// <summary>Gets the optional synthetic response body.</summary>
    public string? ResponseBody { get; }

    /// <summary>Gets synthetic response headers.</summary>
    public IReadOnlyDictionary<string, string> ResponseHeaders => new ReadOnlyDictionary<string, string>(
        _responseHeaders.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase));

    internal bool Matches(WebNetworkRequest request)
    {
        return (Methods.Count == 0 || Methods.Contains(request.Method, StringComparer.Ordinal)) &&
            (ResourceTypes.Count == 0 || ResourceTypes.Contains(request.ResourceType, StringComparer.Ordinal)) &&
            WildcardMatches(UrlPattern, request.Url);
    }

    private void ValidateActionShape(int? responseStatus)
    {
        bool hasRequestChanges = _requestHeaders.Count > 0 || _removedRequestHeaders.Count > 0;
        bool hasResponse = responseStatus is not null || ResponseContentType is not null || ResponseBody is not null || _responseHeaders.Count > 0;
        if (Action == WebNetworkInterceptionAction.Modify && !hasRequestChanges)
        {
            throw new ArgumentException("Modify network rules require at least one request header change.");
        }

        if (Action != WebNetworkInterceptionAction.Modify && hasRequestChanges)
        {
            throw new ArgumentException("Request header changes are only valid for modify network rules.");
        }

        if (Action != WebNetworkInterceptionAction.Fulfill && hasResponse)
        {
            throw new ArgumentException("Synthetic response properties are only valid for fulfill network rules.");
        }

        if (Action == WebNetworkInterceptionAction.Fulfill && ResponseStatus is < 200 or > 599)
        {
            throw new ArgumentOutOfRangeException(nameof(responseStatus), ResponseStatus, "Synthetic response status must be between 200 and 599.");
        }
    }

    private static IReadOnlyList<string> CopyMethods(IReadOnlyList<string>? methods)
    {
        if (methods is null)
        {
            return Array.AsReadOnly(Array.Empty<string>());
        }

        if (methods.Count > 32)
        {
            throw new ArgumentException("A network rule cannot declare more than 32 methods.", nameof(methods));
        }

        List<string> result = [];
        HashSet<string> unique = new(StringComparer.Ordinal);
        foreach (string method in methods)
        {
            if (string.IsNullOrEmpty(method))
            {
                throw new ArgumentException("Network rule methods must be unique HTTP tokens.", nameof(methods));
            }

            string normalized = method.ToUpperInvariant();
            if (!IsToken(normalized) || !unique.Add(normalized))
            {
                throw new ArgumentException("Network rule methods must be unique HTTP tokens.", nameof(methods));
            }

            result.Add(normalized);
        }

        return result.AsReadOnly();
    }

    private static IReadOnlyList<string> CopyResourceTypes(IReadOnlyList<string>? resourceTypes)
    {
        if (resourceTypes is null)
        {
            return Array.AsReadOnly(Array.Empty<string>());
        }

        List<string> result = [];
        HashSet<string> unique = new(StringComparer.Ordinal);
        foreach (string resourceType in resourceTypes)
        {
            if (string.IsNullOrEmpty(resourceType))
            {
                throw new ArgumentException("Network rule resource types are invalid or duplicated.", nameof(resourceTypes));
            }

            string normalized = resourceType.ToLowerInvariant();
            if (!_resourceTypes.Contains(normalized) || !unique.Add(normalized))
            {
                throw new ArgumentException("Network rule resource types are invalid or duplicated.", nameof(resourceTypes));
            }

            result.Add(normalized);
        }

        return result.AsReadOnly();
    }

    private static IReadOnlyDictionary<string, string> CopyHeaders(IReadOnlyDictionary<string, string>? headers, HashSet<string> forbidden, string parameter)
    {
        if (headers is null)
        {
            return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        if (headers.Count > 64)
        {
            throw new ArgumentException("A network rule cannot declare more than 64 headers.", parameter);
        }

        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> header in headers)
        {
            if (!IsToken(header.Key) || forbidden.Contains(header.Key) || ValidateText(header.Value, 8192, parameter) is null || !result.TryAdd(header.Key, header.Value))
            {
                throw new ArgumentException("Network rule header is invalid, duplicated, or protected.", parameter);
            }
        }

        return new ReadOnlyDictionary<string, string>(result);
    }

    private static IReadOnlyList<string> CopyRemovedHeaders(IReadOnlyList<string>? headers)
    {
        if (headers is null)
        {
            return Array.AsReadOnly(Array.Empty<string>());
        }

        if (headers.Count > 64)
        {
            throw new ArgumentException("A network rule cannot remove more than 64 headers.", nameof(headers));
        }

        List<string> result = [];
        HashSet<string> unique = new(StringComparer.OrdinalIgnoreCase);
        foreach (string header in headers)
        {
            if (string.IsNullOrEmpty(header) || !IsToken(header) || !unique.Add(header))
            {
                throw new ArgumentException("Removed request headers must be unique HTTP tokens.", nameof(headers));
            }

            result.Add(header);
        }

        return result.AsReadOnly();
    }

    private static string? ValidateText(string? value, int maximumLength, string parameter)
    {
        if (value is null)
        {
            return null;
        }

        if (value.Length > maximumLength || value.Contains('\r') || value.Contains('\n'))
        {
            throw new ArgumentException("Network rule text is invalid or exceeds its limit.", parameter);
        }

        return value;
    }

    private static string? ValidateBody(string? body)
    {
        if (body is not null && Encoding.UTF8.GetByteCount(body) > 1024 * 1024)
        {
            throw new ArgumentException("Synthetic response body exceeds the 1 MiB limit.", nameof(body));
        }

        return body;
    }

    private static bool IsIdentifier(string value)
    {
        return value.Length is > 0 and <= 64 && value.All(static character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');
    }

    private static bool IsToken(string value)
    {
        return value.Length is > 0 and <= 128 && value.All(static character => char.IsAsciiLetterOrDigit(character) || character is '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~');
    }

    private static bool WildcardMatches(string pattern, string value)
    {
        int patternIndex = 0;
        int valueIndex = 0;
        int starIndex = -1;
        int retryValueIndex = -1;
        while (valueIndex < value.Length)
        {
            if (patternIndex < pattern.Length && (pattern[patternIndex] == '?' || pattern[patternIndex] == value[valueIndex]))
            {
                patternIndex++;
                valueIndex++;
            }
            else if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                starIndex = patternIndex++;
                retryValueIndex = valueIndex;
            }
            else if (starIndex >= 0)
            {
                patternIndex = starIndex + 1;
                valueIndex = ++retryValueIndex;
            }
            else
            {
                return false;
            }
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
        {
            patternIndex++;
        }

        return patternIndex == pattern.Length;
    }
}

/// <summary>Contains one immutable network interception decision.</summary>
public sealed class WebNetworkInterceptionDecision
{
    internal WebNetworkInterceptionDecision(WebNetworkInterceptionAction action, WebNetworkInterceptionRule? rule)
    {
        Action = action;
        Rule = rule;
    }

    /// <summary>Gets the selected action.</summary>
    public WebNetworkInterceptionAction Action { get; }

    /// <summary>Gets the first matching rule, or null when the default action was selected.</summary>
    public WebNetworkInterceptionRule? Rule { get; }
}

/// <summary>Evaluates ordered network interception rules with deterministic first-match semantics.</summary>
public sealed class WebNetworkInterceptionPolicy
{
    /// <summary>Initializes an immutable interception policy.</summary>
    public WebNetworkInterceptionPolicy(
        IReadOnlyList<WebNetworkInterceptionRule>? rules = null,
        WebNetworkInterceptionAction defaultAction = WebNetworkInterceptionAction.Allow,
        int maximumInterceptions = 10_000)
    {
        if (!Enum.IsDefined(defaultAction) || defaultAction is not (WebNetworkInterceptionAction.Allow or WebNetworkInterceptionAction.Block))
        {
            throw new ArgumentException("Default network action must be allow or block.", nameof(defaultAction));
        }

        if (maximumInterceptions is < 1 or > 100_000)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumInterceptions), maximumInterceptions, "Maximum interceptions must be between 1 and 100000.");
        }

        if (rules?.Count > 128)
        {
            throw new ArgumentException("A network policy cannot contain more than 128 rules.", nameof(rules));
        }

        Rules = rules is null ? Array.AsReadOnly(Array.Empty<WebNetworkInterceptionRule>()) : Array.AsReadOnly([.. rules]);
        if (Rules.Select(static rule => rule.Id).Distinct(StringComparer.Ordinal).Count() != Rules.Count)
        {
            throw new ArgumentException("Network rule identifiers must be unique.", nameof(rules));
        }

        DefaultAction = defaultAction;
        MaximumInterceptions = maximumInterceptions;
    }

    /// <summary>Gets ordered rules.</summary>
    public IReadOnlyList<WebNetworkInterceptionRule> Rules { get; }

    /// <summary>Gets the default allow or block action.</summary>
    public WebNetworkInterceptionAction DefaultAction { get; }

    /// <summary>Gets the maximum number of routed requests in one resource lifetime.</summary>
    public int MaximumInterceptions { get; }

    /// <summary>Evaluates one request using deterministic first-match semantics.</summary>
    public WebNetworkInterceptionDecision Evaluate(WebNetworkRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        WebNetworkInterceptionRule? rule = Rules.FirstOrDefault(candidate => candidate.Matches(request));
        return rule is null
            ? new WebNetworkInterceptionDecision(DefaultAction, null)
            : new WebNetworkInterceptionDecision(rule.Action, rule);
    }
}
