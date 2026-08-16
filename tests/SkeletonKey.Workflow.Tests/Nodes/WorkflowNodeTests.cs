using System.Text.Json.Nodes;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Policies;

namespace SkeletonKey.Workflow.Tests.Nodes;

/// <summary>
/// Covers workflow node document model behavior.
/// </summary>
public sealed class WorkflowNodeTests
{
    /// <summary>
    /// Verifies omitted parameters become an empty JSON object.
    /// </summary>
    [Fact]
    public void CreatesEmptyParameterObject_WhenParametersAreOmitted()
    {
        WorkflowNode node = new("start", "core.start", 1);

        Assert.Empty(node.Parameters);
    }

    /// <summary>
    /// Verifies supplied parameters are cloned during construction.
    /// </summary>
    [Fact]
    public void DefensivelyClonesSuppliedParameters()
    {
        JsonObject parameters = new()
        {
            ["message"] = "hello",
        };

        WorkflowNode node = new("log", "core.log", 1, parameters: parameters);

        Assert.NotSame(parameters, node.Parameters);
        Assert.Equal("hello", node.Parameters["message"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies source parameter mutation does not affect the node.
    /// </summary>
    [Fact]
    public void ExternalMutationOfSourceParameters_DoesNotChangeNode()
    {
        JsonObject parameters = new()
        {
            ["message"] = "hello",
        };

        WorkflowNode node = new("log", "core.log", 1, parameters: parameters);

        parameters["message"] = "changed";

        Assert.Equal("hello", node.Parameters["message"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies returned parameter mutation does not affect the node.
    /// </summary>
    [Fact]
    public void ExternalMutationOfReturnedParameters_DoesNotChangeNode()
    {
        WorkflowNode node = new(
            "log",
            "core.log",
            1,
            parameters: new JsonObject
            {
                ["message"] = "hello",
            });

        JsonObject returnedParameters = node.Parameters;
        returnedParameters["message"] = "changed";

        Assert.Equal("hello", node.Parameters["message"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies execution policy declarations are preserved.
    /// </summary>
    [Fact]
    public void PreservesExecutionPolicyDeclarations()
    {
        WorkflowExecutionPolicy policy = new(timeout: "PT30S", onError: WorkflowOnError.Continue);

        WorkflowNode node = new("log", "core.log", 1, policy: policy);

        Assert.Same(policy, node.Policy);
    }

    /// <summary>
    /// Verifies workflow node properties do not model runtime execution state.
    /// </summary>
    [Fact]
    public void DoesNotContainRuntimeExecutionState()
    {
        string[] runtimePropertyNames =
        [
            "Status",
            "Result",
            "Error",
            "StartedAt",
            "FinishedAt",
            "Attempt",
        ];

        string[] publicPropertyNames = typeof(WorkflowNode)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        foreach (string runtimePropertyName in runtimePropertyNames)
        {
            Assert.DoesNotContain(runtimePropertyName, publicPropertyNames);
        }
    }
}
