using System.Text.Json.Nodes;
using SkeletonKey.Validation.Tests.Support;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Validation.Tests.Rules;

/// <summary>
/// Covers workflow resource, locator, and human interaction semantic validation.
/// </summary>
public sealed class ResourceLocatorInteractionValidationTests
{
    /// <summary>
    /// Verifies valid resource declarations and browser constraints are accepted.
    /// </summary>
    [Fact]
    public void AcceptsValidResourceDeclarationAndBrowserConstraints()
    {
        WorkflowValidationResult result = Validate(resources: new Dictionary<string, WorkflowResourceDefinition>
        {
            ["browser"] = new WorkflowResourceDefinition(
                StandardWorkflowResourceKinds.WebBrowser,
                capabilities: [StandardWorkflowResourceCapabilities.WebPersistentProfile],
                constraints: new JsonObject { ["engine"] = "chromium", ["profile"] = "persistent", ["visibility"] = "headful" }),
        });

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies invalid resource declarations are rejected with specific codes.
    /// </summary>
    [Fact]
    public void RejectsInvalidResourceDeclaration()
    {
        WorkflowValidationResult result = Validate(resources: new Dictionary<string, WorkflowResourceDefinition>
        {
            ["bad name"] = new WorkflowResourceDefinition("browser", capabilities: ["bad_capability", "web.frames", "web.frames"], constraints: new JsonObject { ["engine"] = "bad" }),
        });

        AssertCodes(result,
            WorkflowValidationCodes.InvalidWorkflowResourceName,
            WorkflowValidationCodes.InvalidWorkflowResourceKind,
            WorkflowValidationCodes.InvalidResourceCapabilityId,
            WorkflowValidationCodes.DuplicateResourceCapability);
    }

    /// <summary>
    /// Verifies invalid standard browser constraints are rejected.
    /// </summary>
    [Fact]
    public void RejectsInvalidStandardBrowserConstraints()
    {
        WorkflowValidationResult result = Validate(resources: new Dictionary<string, WorkflowResourceDefinition>
        {
            ["browser"] = new WorkflowResourceDefinition(StandardWorkflowResourceKinds.WebBrowser, constraints: new JsonObject { ["engine"] = "internet-explorer" }),
        });

        AssertCodes(result, WorkflowValidationCodes.InvalidStandardResourceConstraints);
    }

    /// <summary>
    /// Verifies resource references and invocation mappings are accepted.
    /// </summary>
    [Fact]
    public void AcceptsResourceReferenceAndInvocationResourceMapping()
    {
        WorkflowValidationResult result = Validate(node: new WorkflowNode("invoke", "workflow.invoke", 1, parameters: new JsonObject
        {
            ["workflow"] = new JsonObject { ["id"] = "child" },
            ["resources"] = new JsonObject { ["browser"] = Resource("browser") },
        }));

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies unknown resource references and mapped parent resources are rejected.
    /// </summary>
    [Fact]
    public void RejectsUnknownResourceReferences()
    {
        WorkflowValidationResult result = Validate(node: new WorkflowNode("invoke", "workflow.invoke", 1, parameters: new JsonObject
        {
            ["workflow"] = new JsonObject { ["id"] = "child" },
            ["resources"] = new JsonObject { ["browser"] = Resource("missing") },
        }));

        AssertCodes(result, WorkflowValidationCodes.UnknownWorkflowResourceReference);
    }

    /// <summary>
    /// Verifies invalid invocation resource mapping syntax is rejected.
    /// </summary>
    [Fact]
    public void RejectsInvalidInvocationResourceMapping()
    {
        WorkflowValidationResult result = Validate(node: new WorkflowNode("invoke", "workflow.invoke", 1, parameters: new JsonObject
        {
            ["workflow"] = new JsonObject { ["id"] = "child" },
            ["resources"] = new JsonObject { ["bad name"] = new JsonObject { ["name"] = "browser" } },
        }));

        AssertCodes(result, WorkflowValidationCodes.InvalidInvocationResourceMappingName, WorkflowValidationCodes.InvalidInvocationResourceMappingValue);
    }

    /// <summary>
    /// Verifies locator references are accepted and invalid syntax is rejected.
    /// </summary>
    [Fact]
    public void ValidatesLocatorReferences()
    {
        WorkflowValidationResult valid = Validate(node: new WorkflowNode("start", "core.start", 1, parameters: new JsonObject
        {
            ["target"] = Locator("catalog", "1.0.0", "save"),
        }));
        WorkflowValidationResult invalid = Validate(node: new WorkflowNode("start", "core.start", 1, parameters: new JsonObject
        {
            ["target"] = Locator("bad.catalog", "latest", "bad.id"),
        }));

        Assert.True(valid.IsValid);
        AssertCodes(invalid, WorkflowValidationCodes.InvalidLocatorCatalogId, WorkflowValidationCodes.InvalidLocatorId, WorkflowValidationCodes.InvalidLocatorVersion);
    }

    /// <summary>
    /// Verifies all interaction kinds are accepted when statically valid.
    /// </summary>
    [Theory]
    [InlineData("confirmation")]
    [InlineData("text")]
    [InlineData("secret")]
    [InlineData("choice")]
    [InlineData("multiple-choice")]
    [InlineData("manual-action")]
    public void AcceptsValidInteractionKinds(string kind)
    {
        WorkflowValidationResult result = Validate(node: Interaction(kind));

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies invalid interaction options are rejected.
    /// </summary>
    [Fact]
    public void RejectsInvalidInteractionOptions()
    {
        WorkflowValidationResult withoutOptions = Validate(node: Interaction("choice", options: []));
        WorkflowValidationResult optionsOnText = Validate(node: Interaction("text", options: [Option("a"), Option("a")]));

        AssertCodes(withoutOptions, WorkflowValidationCodes.InvalidInteractionOptions);
        AssertCodes(optionsOnText, WorkflowValidationCodes.InvalidInteractionOptions);
    }

    /// <summary>
    /// Verifies duplicate option IDs are rejected.
    /// </summary>
    [Fact]
    public void RejectsDuplicateInteractionOptionIds()
    {
        WorkflowValidationResult result = Validate(node: Interaction("choice", options: [Option("a"), Option("a")]));

        AssertCodes(result, WorkflowValidationCodes.DuplicateInteractionOptionId);
    }

    /// <summary>
    /// Verifies invalid defaults are rejected by interaction kind.
    /// </summary>
    [Fact]
    public void RejectsInvalidInteractionDefaults()
    {
        AssertCodes(Validate(node: Interaction("confirmation", defaultValue: "yes")), WorkflowValidationCodes.InvalidInteractionDefault);
        AssertCodes(Validate(node: Interaction("choice", options: [Option("a")], defaultValue: "missing")), WorkflowValidationCodes.InvalidInteractionDefault);
        AssertCodes(Validate(node: Interaction("multiple-choice", options: [Option("a")], defaultValue: new JsonArray("a", "a"))), WorkflowValidationCodes.InvalidInteractionDefault);
        AssertCodes(Validate(node: Interaction("manual-action", defaultValue: true)), WorkflowValidationCodes.InvalidInteractionDefault);
        AssertCodes(Validate(node: Interaction("secret", defaultValue: "secret")), WorkflowValidationCodes.SecretInteractionContainsProhibitedDefault);
    }

    /// <summary>
    /// Verifies invalid interaction timeout and version are rejected.
    /// </summary>
    [Fact]
    public void RejectsInvalidInteractionTimeoutAndVersion()
    {
        WorkflowValidationResult invalidTimeout = Validate(node: Interaction("confirmation", timeout: "soon"));
        WorkflowValidationResult invalidVersion = Validate(node: new WorkflowNode("request", "interaction.request", 2, parameters: new JsonObject { ["kind"] = "confirmation", ["prompt"] = "Continue?" }));

        AssertCodes(invalidTimeout, WorkflowValidationCodes.InvalidInteractionTimeout);
        AssertCodes(invalidVersion, WorkflowValidationCodes.UnsupportedInteractionNodeVersion);
    }

    /// <summary>
    /// Verifies invalid interaction ports are rejected.
    /// </summary>
    [Fact]
    public void RejectsInvalidInteractionPorts()
    {
        WorkflowDocument workflow = new(
            id: "workflow",
            name: "Workflow",
            resources: Resources(),
            nodes:
            [
                new WorkflowNode("start", "core.start", 1),
                Interaction("confirmation"),
                new WorkflowNode("end", "core.end", 1),
            ],
            connections:
            [
                new WorkflowConnection(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint("request", "wrong")),
                new WorkflowConnection(new WorkflowEndpoint("request", "wrong"), new WorkflowEndpoint("end", "main")),
            ]);

        AssertCodes(ValidationTestData.Validate(workflow), WorkflowValidationCodes.InvalidInteractionPort);
    }

    /// <summary>
    /// Verifies validation does not mutate resources or interaction parameters and remains deterministic.
    /// </summary>
    [Fact]
    public void ValidationDoesNotMutateResourcesOrInteractionParametersAndIsDeterministic()
    {
        WorkflowDocument workflow = Workflow(node: Interaction("choice", options: [Option("a")], defaultValue: "missing"));
        string before = workflow.Nodes[0].Parameters.ToJsonString();

        string[] first = [.. ValidationTestData.Validate(workflow).Issues.Select(static issue => $"{issue.Code}:{issue.Path}")];
        string[] second = [.. ValidationTestData.Validate(workflow).Issues.Select(static issue => $"{issue.Code}:{issue.Path}")];

        Assert.Equal(first, second);
        Assert.Equal(before, workflow.Nodes[0].Parameters.ToJsonString());
    }

    private static WorkflowValidationResult Validate(
        IReadOnlyDictionary<string, WorkflowResourceDefinition>? resources = null,
        WorkflowNode? node = null)
    {
        return ValidationTestData.Validate(Workflow(resources, node));
    }

    private static WorkflowDocument Workflow(
        IReadOnlyDictionary<string, WorkflowResourceDefinition>? resources = null,
        WorkflowNode? node = null)
    {
        WorkflowNode actualNode = node ?? new WorkflowNode("start", "core.start", 1);
        bool isStart = actualNode.Id == "start";
        return new WorkflowDocument(
            id: "workflow",
            name: "Workflow",
            resources: resources ?? Resources(),
            nodes: isStart ? [actualNode] : [new WorkflowNode("start", "core.start", 1), actualNode],
            connections: isStart
                ? []
                : [new WorkflowConnection(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint(actualNode.Id, "main"))]);
    }

    private static IReadOnlyDictionary<string, WorkflowResourceDefinition> Resources()
    {
        return new Dictionary<string, WorkflowResourceDefinition>
        {
            ["browser"] = new WorkflowResourceDefinition(StandardWorkflowResourceKinds.WebBrowser),
        };
    }

    private static WorkflowNode Interaction(
        string kind,
        JsonArray? options = null,
        JsonNode? defaultValue = null,
        string? timeout = "PT1M")
    {
        JsonObject parameters = new()
        {
            ["kind"] = kind,
            ["prompt"] = "Continue?",
            ["required"] = true,
        };

        if (kind is "choice" or "multiple-choice")
        {
            parameters["options"] = options ?? new JsonArray(Option("a"));
        }
        else if (options is not null)
        {
            parameters["options"] = options;
        }

        if (defaultValue is not null || kind == "text")
        {
            parameters["default"] = defaultValue;
        }

        if (timeout is not null)
        {
            parameters["timeout"] = timeout;
        }

        return new WorkflowNode("request", "interaction.request", 1, parameters: parameters);
    }

    private static JsonObject Option(string id)
    {
        return new JsonObject { ["id"] = id, ["label"] = id.ToUpperInvariant() };
    }

    private static JsonObject Resource(string name)
    {
        return new JsonObject { ["$resource"] = new JsonObject { ["name"] = name } };
    }

    private static JsonObject Locator(string catalog, string version, string id)
    {
        return new JsonObject { ["$locator"] = new JsonObject { ["catalog"] = catalog, ["version"] = version, ["id"] = id } };
    }

    private static void AssertCodes(WorkflowValidationResult result, params string[] codes)
    {
        foreach (string code in codes)
        {
            Assert.Contains(result.Issues, issue => issue.Code == code);
        }
    }
}
