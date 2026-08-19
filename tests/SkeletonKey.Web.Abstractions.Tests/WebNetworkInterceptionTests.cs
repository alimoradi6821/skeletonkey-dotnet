using SkeletonKey.Web.Abstractions;

namespace SkeletonKey.Web.Abstractions.Tests;

/// <summary>Covers bounded provider-neutral network interception policy behavior.</summary>
public sealed class WebNetworkInterceptionTests
{
    /// <summary>Verifies ordered policies select the first matching rule.</summary>
    [Fact]
    public void EvaluateUsesDeterministicFirstMatch()
    {
        WebNetworkInterceptionPolicy policy = new([
            new("block-api", "https://example.test/api/*", WebNetworkInterceptionAction.Block),
            new("allow-all", "*", WebNetworkInterceptionAction.Allow),
        ]);

        WebNetworkInterceptionDecision decision = policy.Evaluate(new WebNetworkRequest("get", "https://example.test/api/users", "xhr"));

        Assert.Equal(WebNetworkInterceptionAction.Block, decision.Action);
        Assert.Equal("block-api", decision.Rule!.Id);
    }

    /// <summary>Verifies method and resource filters are normalized and combined.</summary>
    [Fact]
    public void EvaluateCombinesMethodResourceAndUrlFilters()
    {
        WebNetworkInterceptionPolicy policy = new([
            new("post-xhr", "https://example.test/*", WebNetworkInterceptionAction.Block, methods: ["post"], resourceTypes: ["XHR"]),
        ]);

        WebNetworkInterceptionDecision allowed = policy.Evaluate(new WebNetworkRequest("GET", "https://example.test/api", "xhr"));
        WebNetworkInterceptionDecision blocked = policy.Evaluate(new WebNetworkRequest("POST", "https://example.test/api", "xhr"));

        Assert.Equal(WebNetworkInterceptionAction.Allow, allowed.Action);
        Assert.Equal(WebNetworkInterceptionAction.Block, blocked.Action);
    }

    /// <summary>Verifies policies may use a fail-closed default action.</summary>
    [Fact]
    public void EvaluateSupportsDefaultBlock()
    {
        WebNetworkInterceptionPolicy policy = new(defaultAction: WebNetworkInterceptionAction.Block);

        WebNetworkInterceptionDecision decision = policy.Evaluate(new WebNetworkRequest("GET", "https://example.test/", "document"));

        Assert.Equal(WebNetworkInterceptionAction.Block, decision.Action);
        Assert.Null(decision.Rule);
    }

    /// <summary>Verifies modify rules defensively copy bounded header changes.</summary>
    [Fact]
    public void ModifyRulesDefensivelyCopyHeaderChanges()
    {
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase) { ["x-phase"] = "21" };
        WebNetworkInterceptionRule rule = new("modify", "*", WebNetworkInterceptionAction.Modify, requestHeaders: headers, removeRequestHeaders: ["referer"]);
        headers["x-phase"] = "changed";

        Assert.Equal("21", rule.RequestHeaders["x-phase"]);
        Assert.Equal(["referer"], rule.RemovedRequestHeaders);
    }

    /// <summary>Verifies fulfill rules expose a bounded synthetic response.</summary>
    [Fact]
    public void FulfillRulesExposeSyntheticResponse()
    {
        WebNetworkInterceptionRule rule = new(
            "mock",
            "https://example.test/config",
            WebNetworkInterceptionAction.Fulfill,
            responseStatus: 201,
            responseContentType: "application/json",
            responseBody: "{}",
            responseHeaders: new Dictionary<string, string> { ["cache-control"] = "no-store" });

        Assert.Equal(201, rule.ResponseStatus);
        Assert.Equal("application/json", rule.ResponseContentType);
        Assert.Equal("{}", rule.ResponseBody);
    }

    /// <summary>Verifies sensitive request headers cannot be injected declaratively.</summary>
    [Theory]
    [InlineData("authorization")]
    [InlineData("cookie")]
    [InlineData("host")]
    public void ModifyRulesRejectProtectedHeaders(string header)
    {
        Assert.Throws<ArgumentException>(() => new WebNetworkInterceptionRule(
            "invalid",
            "*",
            WebNetworkInterceptionAction.Modify,
            requestHeaders: new Dictionary<string, string> { [header] = "secret" }));
    }

    /// <summary>Verifies header values cannot inject additional header lines.</summary>
    [Fact]
    public void RulesRejectHeaderLineInjection()
    {
        Assert.Throws<ArgumentException>(() => new WebNetworkInterceptionRule(
            "invalid",
            "*",
            WebNetworkInterceptionAction.Modify,
            requestHeaders: new Dictionary<string, string> { ["x-test"] = "safe\r\ninjected: value" }));
    }

    /// <summary>Verifies policy rule count and interception count are bounded.</summary>
    [Fact]
    public void PolicyLimitsAreValidated()
    {
        WebNetworkInterceptionRule rule = new("allow", "*", WebNetworkInterceptionAction.Allow);

        Assert.Throws<ArgumentOutOfRangeException>(() => new WebNetworkInterceptionPolicy(maximumInterceptions: 0));
        Assert.Throws<ArgumentException>(() => new WebNetworkInterceptionPolicy(Enumerable.Repeat(rule, 129).ToArray()));
    }

    /// <summary>Verifies undefined actions and ambiguous content types fail closed.</summary>
    [Fact]
    public void RulesRejectUndefinedActionsAndAmbiguousContentTypes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WebNetworkInterceptionRule("invalid", "*", (WebNetworkInterceptionAction)99));
        Assert.Throws<ArgumentException>(() => new WebNetworkInterceptionRule(
            "ambiguous",
            "*",
            WebNetworkInterceptionAction.Fulfill,
            responseContentType: "application/json",
            responseHeaders: new Dictionary<string, string> { ["content-type"] = "text/plain" }));
    }

    /// <summary>Verifies synthetic response size and status bounds and unique policy identifiers.</summary>
    [Fact]
    public void SyntheticResponsesAndPolicyIdentifiersAreBounded()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WebNetworkInterceptionRule(
            "status",
            "*",
            WebNetworkInterceptionAction.Fulfill,
            responseStatus: 199));
        Assert.Throws<ArgumentException>(() => new WebNetworkInterceptionRule(
            "body",
            "*",
            WebNetworkInterceptionAction.Fulfill,
            responseBody: new string('x', (1024 * 1024) + 1)));

        WebNetworkInterceptionRule duplicate = new("duplicate", "*", WebNetworkInterceptionAction.Allow);
        Assert.Throws<ArgumentException>(() => new WebNetworkInterceptionPolicy([duplicate, duplicate]));
    }
}
