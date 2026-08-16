using SkeletonKey.Serialization.Json.Tests.Support;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Inputs;
using SkeletonKey.Workflow.Policies;

namespace SkeletonKey.Serialization.Json.Tests.Basic;

/// <summary>
/// Covers workflow JSON serialization behavior.
/// </summary>
public sealed class BasicDeserializationTests
{
    private readonly WorkflowJsonSerializer _serializer = new();

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void DeserializesMinimalValidWorkflow()
    {
        WorkflowDocument workflow = _serializer.Deserialize(WorkflowJsonTestData.MinimalJson);

        Assert.Equal("minimal", workflow.Id);
        Assert.Equal("Minimal workflow", workflow.Name);
        Assert.Equal(2, workflow.Nodes.Count);
        Assert.Single(workflow.Connections);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void AppliesDefaultsForOmittedOptionalCollections()
    {
        WorkflowDocument workflow = _serializer.Deserialize("""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "minimal",
              "name": "Minimal workflow",
              "nodes": [],
              "connections": []
            }
            """);

        Assert.Empty(workflow.Inputs);
        Assert.Empty(workflow.Variables);
        Assert.Null(workflow.Designer);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void AppliesDefaultNodeParameters()
    {
        WorkflowDocument workflow = _serializer.Deserialize("""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "minimal",
              "name": "Minimal workflow",
              "nodes": [{ "id": "start", "type": "core.start", "typeVersion": 1 }],
              "connections": []
            }
            """);

        Assert.Empty(workflow.Nodes[0].Parameters);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void AppliesDefaultDisabledState()
    {
        WorkflowDocument workflow = _serializer.Deserialize("""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "minimal",
              "name": "Minimal workflow",
              "nodes": [{ "id": "start", "type": "core.start", "typeVersion": 1 }],
              "connections": []
            }
            """);

        Assert.False(workflow.Nodes[0].Disabled);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void AppliesDefaultExecutionPolicyValues()
    {
        WorkflowDocument workflow = _serializer.Deserialize("""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "minimal",
              "name": "Minimal workflow",
              "nodes": [{ "id": "start", "type": "core.start", "typeVersion": 1, "policy": {} }],
              "connections": []
            }
            """);

        Assert.Equal(WorkflowOnError.Fail, workflow.Nodes[0].Policy!.OnError);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void AppliesDefaultRetryPolicyValues()
    {
        WorkflowDocument workflow = _serializer.Deserialize("""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "minimal",
              "name": "Minimal workflow",
              "nodes": [{ "id": "start", "type": "core.start", "typeVersion": 1, "policy": { "retry": {} } }],
              "connections": []
            }
            """);

        Assert.Equal(1, workflow.Nodes[0].Policy!.Retry!.MaxAttempts);
        Assert.Equal(1.0, workflow.Nodes[0].Policy!.Retry!.Backoff);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void PreservesArbitraryNodeParameters()
    {
        WorkflowDocument workflow = _serializer.Deserialize(WorkflowJsonTestData.CreateComplexWorkflowJson());

        Assert.Equal("hello", workflow.Nodes[0].Parameters["message"]!.GetValue<string>());
        Assert.True(workflow.Nodes[0].Parameters["nested"]!["flag"]!.GetValue<bool>());
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void PreservesArbitraryWorkflowVariables()
    {
        WorkflowDocument workflow = _serializer.Deserialize(WorkflowJsonTestData.CreateComplexWorkflowJson());

        Assert.Equal("hello", workflow.Variables["text"]!.GetValue<string>());
        Assert.Equal(3, workflow.Variables["count"]!.GetValue<int>());
        Assert.True(workflow.Variables["enabled"]!.GetValue<bool>());
        Assert.Equal(1.5, workflow.Variables["obj"]!["nested"]!["value"]!.GetValue<double>());
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void PreservesNullWorkflowVariableValues()
    {
        WorkflowDocument workflow = _serializer.Deserialize(WorkflowJsonTestData.CreateComplexWorkflowJson());

        Assert.True(workflow.Variables.ContainsKey("nothing"));
        Assert.Null(workflow.Variables["nothing"]);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void PreservesInputDefaultJsonValues()
    {
        WorkflowDocument workflow = _serializer.Deserialize(WorkflowJsonTestData.CreateComplexWorkflowJson());

        Assert.Equal(WorkflowInputType.Object, workflow.Inputs["customer"].Type);
        Assert.Equal("Ada", workflow.Inputs["customer"].Default!["name"]!.GetValue<string>());
        Assert.True(workflow.Inputs["optional"].HasDefault);
        Assert.Null(workflow.Inputs["optional"].Default);
    }
}

