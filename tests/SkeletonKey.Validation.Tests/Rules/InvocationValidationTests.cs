using System.Text.Json.Nodes;
using SkeletonKey.Validation.Tests.Support;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Inputs;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Outputs;

namespace SkeletonKey.Validation.Tests.Rules;

/// <summary>
/// Covers workflow invocation and structured binding semantic validation.
/// </summary>
public sealed class InvocationValidationTests
{
    /// <summary>
    /// Verifies valid workflow invocation declarations are accepted.
    /// </summary>
    [Fact]
    public void AcceptsValidWorkflowInvocation()
    {
        AssertValid(Workflow(Parameters()));
    }

    /// <summary>
    /// Verifies invocation references may omit pinned versions.
    /// </summary>
    [Fact]
    public void AcceptsInvocationWithoutPinnedVersion()
    {
        JsonObject parameters = Parameters();
        parameters["workflow"]!.AsObject().Remove("version");

        AssertValid(Workflow(parameters));
    }

    /// <summary>
    /// Verifies exact Semantic Version 2.0 references are accepted.
    /// </summary>
    [Theory]
    [InlineData("1.0.0")]
    [InlineData("0.1.0")]
    [InlineData("2.3.4-alpha.1")]
    [InlineData("1.2.0+build.7")]
    [InlineData("1.2.0-beta.2+build.9")]
    public void AcceptsValidExactSemanticVersion(string version)
    {
        JsonObject parameters = Parameters();
        parameters["workflow"]!["version"] = version;

        AssertValid(Workflow(parameters));
    }

    /// <summary>
    /// Verifies invalid workflow reference IDs are rejected.
    /// </summary>
    [Fact]
    public void RejectsInvalidWorkflowReferenceId()
    {
        JsonObject parameters = Parameters();
        parameters["workflow"]!["id"] = "bad.id";

        AssertHasCode(Workflow(parameters), WorkflowValidationCodes.InvalidReferencedWorkflowId);
    }

    /// <summary>
    /// Verifies invalid workflow reference versions are rejected.
    /// </summary>
    [Theory]
    [InlineData("latest")]
    [InlineData("*")]
    [InlineData("^1.0.0")]
    [InlineData(">=1.0.0")]
    [InlineData("1")]
    [InlineData("1.2")]
    [InlineData("v1.0.0")]
    public void RejectsInvalidWorkflowReferenceVersion(string version)
    {
        JsonObject parameters = Parameters();
        parameters["workflow"]!["version"] = version;

        AssertHasCode(Workflow(parameters), WorkflowValidationCodes.InvalidReferencedWorkflowVersion);
    }

    /// <summary>
    /// Verifies invalid invocation input names are rejected.
    /// </summary>
    [Fact]
    public void RejectsInvalidInvocationInputName()
    {
        JsonObject parameters = Parameters();
        parameters["inputs"]!.AsObject()["bad/name"] = "value";

        AssertHasCode(Workflow(parameters), WorkflowValidationCodes.InvalidInvocationInputName);
    }

    /// <summary>
    /// Verifies bindings to declared inputs are accepted.
    /// </summary>
    [Fact]
    public void AcceptsBindingToDeclaredInput()
    {
        JsonObject parameters = Parameters();
        parameters["inputs"]!["account"] = Binding("input", "account");

        AssertValid(Workflow(parameters));
    }

    /// <summary>
    /// Verifies bindings to unknown inputs are rejected.
    /// </summary>
    [Fact]
    public void RejectsBindingToUnknownInput()
    {
        JsonObject parameters = Parameters();
        parameters["inputs"]!["account"] = Binding("input", "missing");

        AssertHasCode(Workflow(parameters), WorkflowValidationCodes.UnknownWorkflowInputBinding);
    }

    /// <summary>
    /// Verifies bindings to declared variables are accepted.
    /// </summary>
    [Fact]
    public void AcceptsBindingToDeclaredVariable()
    {
        JsonObject parameters = Parameters();
        parameters["inputs"]!["message"] = Binding("variable", "message");

        AssertValid(Workflow(parameters));
    }

    /// <summary>
    /// Verifies bindings to unknown variables are rejected.
    /// </summary>
    [Fact]
    public void RejectsBindingToUnknownVariable()
    {
        JsonObject parameters = Parameters();
        parameters["inputs"]!["message"] = Binding("variable", "missing");

        AssertHasCode(Workflow(parameters), WorkflowValidationCodes.UnknownWorkflowVariableBinding);
    }

    /// <summary>
    /// Verifies bindings to existing nodes are accepted.
    /// </summary>
    [Fact]
    public void AcceptsBindingToExistingNode()
    {
        JsonObject parameters = Parameters();
        parameters["inputs"]!["result"] = NodeBinding("previous", "result", "");

        AssertValid(Workflow(parameters));
    }

    /// <summary>
    /// Verifies bindings to unknown nodes are rejected.
    /// </summary>
    [Fact]
    public void RejectsBindingToUnknownNode()
    {
        JsonObject parameters = Parameters();
        parameters["inputs"]!["result"] = NodeBinding("missing", "result", "");

        AssertHasCode(Workflow(parameters), WorkflowValidationCodes.UnknownNodeBinding);
    }

    /// <summary>
    /// Verifies self-referencing node bindings are rejected.
    /// </summary>
    [Fact]
    public void RejectsSelfReferencingNodeBinding()
    {
        JsonObject parameters = Parameters();
        parameters["inputs"]!["result"] = NodeBinding("invoke", "result", "");

        AssertHasCode(Workflow(parameters), WorkflowValidationCodes.SelfReferencingNodeBinding);
    }

    /// <summary>
    /// Verifies invalid binding ports are rejected.
    /// </summary>
    [Fact]
    public void RejectsInvalidBindingPort()
    {
        JsonObject parameters = Parameters();
        parameters["inputs"]!["result"] = NodeBinding("previous", "bad port", "");

        AssertHasCode(Workflow(parameters), WorkflowValidationCodes.InvalidNodeBindingPort);
    }

    /// <summary>
    /// Verifies root JSON Pointer binding paths are accepted.
    /// </summary>
    [Fact]
    public void AcceptsRootJsonPointer()
    {
        JsonObject parameters = Parameters();
        parameters["inputs"]!["account"] = Binding("input", "account", "");

        AssertValid(Workflow(parameters));
    }

    /// <summary>
    /// Verifies escaped JSON Pointer binding paths are accepted.
    /// </summary>
    [Fact]
    public void AcceptsEscapedJsonPointer()
    {
        JsonObject parameters = Parameters();
        parameters["inputs"]!["account"] = Binding("input", "account", "/a~1b/tilde~0value");

        AssertValid(Workflow(parameters));
    }

    /// <summary>
    /// Verifies invalid JSON Pointer binding paths are rejected.
    /// </summary>
    [Fact]
    public void RejectsInvalidJsonPointer()
    {
        JsonObject parameters = Parameters();
        parameters["inputs"]!["account"] = Binding("input", "account", "/bad~2path");

        AssertHasCode(Workflow(parameters), WorkflowValidationCodes.InvalidBindingJsonPointer);
    }

    /// <summary>
    /// Verifies URI fragment pointer syntax is rejected.
    /// </summary>
    [Fact]
    public void RejectsUriFragmentPointer()
    {
        JsonObject parameters = Parameters();
        parameters["inputs"]!["account"] = Binding("input", "account", "#/name");

        AssertHasCode(Workflow(parameters), WorkflowValidationCodes.InvalidBindingJsonPointer);
    }

    /// <summary>
    /// Verifies array append pointer tokens are rejected.
    /// </summary>
    [Fact]
    public void RejectsArrayAppendPointer()
    {
        JsonObject parameters = Parameters();
        parameters["inputs"]!["account"] = Binding("input", "account", "/items/-");

        AssertHasCode(Workflow(parameters), WorkflowValidationCodes.InvalidBindingJsonPointer);
    }

    /// <summary>
    /// Verifies onMissing error is accepted.
    /// </summary>
    [Fact]
    public void AcceptsOnMissingError()
    {
        JsonObject parameters = Parameters();
        parameters["inputs"]!["account"] = Binding("input", "account", onMissing: "error");

        AssertValid(Workflow(parameters));
    }

    /// <summary>
    /// Verifies onMissing null is accepted.
    /// </summary>
    [Fact]
    public void AcceptsOnMissingNull()
    {
        JsonObject parameters = Parameters();
        parameters["inputs"]!["account"] = Binding("input", "account", onMissing: "null");

        AssertValid(Workflow(parameters));
    }

    /// <summary>
    /// Verifies onMissing default with explicit null is accepted.
    /// </summary>
    [Fact]
    public void AcceptsOnMissingDefaultWithExplicitNull()
    {
        JsonObject binding = Binding("input", "account", onMissing: "default");
        binding["$binding"]!["default"] = null;
        JsonObject parameters = Parameters();
        parameters["inputs"]!["account"] = binding;

        AssertValid(Workflow(parameters));
    }

    /// <summary>
    /// Verifies defaults without onMissing default are rejected.
    /// </summary>
    [Fact]
    public void RejectsDefaultWithoutOnMissingDefault()
    {
        JsonObject binding = Binding("input", "account");
        binding["$binding"]!["default"] = "fallback";
        JsonObject parameters = Parameters();
        parameters["inputs"]!["account"] = binding;

        AssertHasCode(Workflow(parameters), WorkflowValidationCodes.InvalidBindingMissingValueConfiguration);
    }

    /// <summary>
    /// Verifies onMissing default without default is rejected.
    /// </summary>
    [Fact]
    public void RejectsOnMissingDefaultWithoutDefault()
    {
        JsonObject parameters = Parameters();
        parameters["inputs"]!["account"] = Binding("input", "account", onMissing: "default");

        AssertHasCode(Workflow(parameters), WorkflowValidationCodes.InvalidBindingMissingValueConfiguration);
    }

    /// <summary>
    /// Verifies valid forward stream policies are accepted.
    /// </summary>
    [Fact]
    public void AcceptsValidForwardStreamPolicy()
    {
        JsonObject parameters = Parameters();
        parameters["streams"] = new JsonObject
        {
            ["mode"] = "forward",
        };

        AssertValid(Workflow(parameters));
    }

    /// <summary>
    /// Verifies valid suppress stream policies are accepted.
    /// </summary>
    [Fact]
    public void AcceptsValidSuppressStreamPolicy()
    {
        JsonObject parameters = Parameters();
        parameters["streams"] = new JsonObject
        {
            ["mode"] = "suppress",
        };

        AssertValid(Workflow(parameters));
    }

    /// <summary>
    /// Verifies valid map stream policies are accepted.
    /// </summary>
    [Fact]
    public void AcceptsValidMapStreamPolicy()
    {
        AssertValid(Workflow(Parameters()));
    }

    /// <summary>
    /// Verifies forward stream policies with mappings are rejected.
    /// </summary>
    [Fact]
    public void RejectsForwardPolicyWithMappings()
    {
        JsonObject parameters = Parameters();
        parameters["streams"]!["mode"] = "forward";

        AssertHasCode(Workflow(parameters), WorkflowValidationCodes.InvalidInvocationStreamPolicy);
    }

    /// <summary>
    /// Verifies suppress stream policies with mappings are rejected.
    /// </summary>
    [Fact]
    public void RejectsSuppressPolicyWithMappings()
    {
        JsonObject parameters = Parameters();
        parameters["streams"]!["mode"] = "suppress";

        AssertHasCode(Workflow(parameters), WorkflowValidationCodes.InvalidInvocationStreamPolicy);
    }

    /// <summary>
    /// Verifies map stream policies without mappings are rejected.
    /// </summary>
    [Fact]
    public void RejectsMapPolicyWithoutMappings()
    {
        JsonObject parameters = Parameters();
        parameters["streams"] = new JsonObject
        {
            ["mode"] = "map",
        };

        AssertHasCode(Workflow(parameters), WorkflowValidationCodes.InvalidInvocationStreamPolicy);
    }

    /// <summary>
    /// Verifies invalid mapped channels are rejected.
    /// </summary>
    [Fact]
    public void RejectsInvalidMappedChannel()
    {
        JsonObject parameters = Parameters();
        parameters["streams"]!["mappings"]!.AsObject()["Bad"] = "parent.results";

        AssertHasCode(Workflow(parameters), WorkflowValidationCodes.InvalidInvocationStreamChannel);
    }

    /// <summary>
    /// Verifies undeclared parent stream targets are rejected.
    /// </summary>
    [Fact]
    public void RejectsUndeclaredParentStreamTarget()
    {
        JsonObject parameters = Parameters();
        parameters["streams"]!["mappings"]!.AsObject()["child.results"] = "missing.results";

        AssertHasCode(Workflow(parameters), WorkflowValidationCodes.UndeclaredParentStreamChannel);
    }

    /// <summary>
    /// Verifies unsupported workflow.invoke versions are rejected.
    /// </summary>
    [Fact]
    public void RejectsUnsupportedWorkflowInvokeVersion()
    {
        AssertHasCode(Workflow(Parameters(), invokeTypeVersion: 2), WorkflowValidationCodes.UnsupportedInvocationNodeVersion);
    }

    /// <summary>
    /// Verifies referenced workflow existence is not validated in this phase.
    /// </summary>
    [Fact]
    public void DoesNotValidateReferencedWorkflowExistence()
    {
        JsonObject parameters = Parameters();
        parameters["workflow"]!["id"] = "missing-workflow";

        AssertValid(Workflow(parameters));
    }

    /// <summary>
    /// Verifies child input compatibility is not validated in this phase.
    /// </summary>
    [Fact]
    public void DoesNotValidateChildInputCompatibility()
    {
        JsonObject parameters = Parameters();
        parameters["inputs"]!.AsObject()["unknownChildInput"] = 1;

        AssertValid(Workflow(parameters));
    }

    /// <summary>
    /// Verifies validation does not mutate parameter JSON.
    /// </summary>
    [Fact]
    public void ValidationDoesNotMutateParameterJson()
    {
        JsonObject parameters = Parameters();
        WorkflowDocument workflow = Workflow(parameters);
        string before = workflow.Nodes[2].Parameters.ToJsonString();

        _ = ValidationTestData.Validate(workflow);

        Assert.Equal(before, workflow.Nodes[2].Parameters.ToJsonString());
    }

    /// <summary>
    /// Verifies invocation issue ordering is deterministic.
    /// </summary>
    [Fact]
    public void IssueOrderingRemainsDeterministic()
    {
        JsonObject parameters = Parameters();
        parameters["workflow"]!["id"] = "bad.id";
        parameters["inputs"]!["account"] = Binding("input", "missing");
        parameters["streams"]!["mappings"]!.AsObject()["child.results"] = "missing.results";

        WorkflowValidationResult result = ValidationTestData.Validate(Workflow(parameters));

        Assert.Equal(
            [
                WorkflowValidationCodes.InvalidReferencedWorkflowId,
                WorkflowValidationCodes.UnknownWorkflowInputBinding,
                WorkflowValidationCodes.UndeclaredParentStreamChannel,
            ],
            [.. result.Issues.Where(static issue => issue.Code.StartsWith("SKW29", StringComparison.Ordinal)).Select(static issue => issue.Code)]);
    }

    private static WorkflowDocument Workflow(JsonObject parameters, int invokeTypeVersion = 1)
    {
        return new WorkflowDocument(
            id: "parent",
            name: "Parent",
            inputs: new Dictionary<string, WorkflowInputDefinition>
            {
                ["account"] = new WorkflowInputDefinition(WorkflowInputType.Object),
            },
            variables: new Dictionary<string, JsonNode?>
            {
                ["message"] = "hello",
            },
            nodes:
            [
                new WorkflowNode("start", "core.start", 1),
                new WorkflowNode("previous", "core.log", 1),
                new WorkflowNode("invoke", "workflow.invoke", invokeTypeVersion, parameters: parameters),
            ],
            connections:
            [
                new WorkflowConnection(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint("previous", "main")),
                new WorkflowConnection(new WorkflowEndpoint("previous", "main"), new WorkflowEndpoint("invoke", "main")),
            ],
            outputs: new Dictionary<string, WorkflowOutputDefinition>
            {
                ["parentEvents"] = new WorkflowOutputDefinition(WorkflowOutputMode.Stream, channel: "parent.results"),
            });
    }

    private static JsonObject Parameters()
    {
        return new JsonObject
        {
            ["workflow"] = new JsonObject
            {
                ["id"] = "child-workflow",
                ["version"] = "1.0.0",
            },
            ["inputs"] = new JsonObject
            {
                ["account"] = Binding("input", "account"),
            },
            ["streams"] = new JsonObject
            {
                ["mode"] = "map",
                ["mappings"] = new JsonObject
                {
                    ["child.results"] = "parent.results",
                },
            },
        };
    }

    private static JsonObject Binding(string source, string name, string? path = null, string? onMissing = null)
    {
        JsonObject binding = new()
        {
            ["source"] = source,
            ["name"] = name,
        };

        if (path is not null)
        {
            binding["path"] = path;
        }

        if (onMissing is not null)
        {
            binding["onMissing"] = onMissing;
        }

        return new JsonObject
        {
            ["$binding"] = binding,
        };
    }

    private static JsonObject NodeBinding(string node, string port, string path)
    {
        return new JsonObject
        {
            ["$binding"] = new JsonObject
            {
                ["source"] = "node",
                ["node"] = node,
                ["port"] = port,
                ["path"] = path,
            },
        };
    }

    private static void AssertValid(WorkflowDocument workflow)
    {
        WorkflowValidationResult result = ValidationTestData.Validate(workflow);

        Assert.True(result.IsValid);
        Assert.Empty(result.Warnings);
    }

    private static void AssertHasCode(WorkflowDocument workflow, string code)
    {
        WorkflowValidationResult result = ValidationTestData.Validate(workflow);

        Assert.Contains(result.Issues, issue => issue.Code == code);
    }
}
