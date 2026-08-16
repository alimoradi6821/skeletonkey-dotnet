using System.Text.Json.Nodes;
using SkeletonKey.Serialization.Json.Tests.Support;
using SkeletonKey.Workflow.Documents;

namespace SkeletonKey.Serialization.Json.Tests.RoundTrip;

/// <summary>
/// Covers workflow JSON serialization behavior.
/// </summary>
public sealed class RoundTripTests
{
    private readonly WorkflowJsonSerializer _serializer = new();

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void RoundTripsMinimalExample()
    {
        string first = _serializer.Serialize(WorkflowJsonTestData.CreateMinimalWorkflow());
        string second = _serializer.Serialize(_serializer.Deserialize(first));

        Assert.Equal(first, second);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void RoundTripsNestedParameters()
    {
        WorkflowDocument workflow = _serializer.Deserialize(WorkflowJsonTestData.CreateComplexWorkflowJson());

        Assert.Equal(2.5, workflow.Nodes[0].Parameters["nested"]!["numbers"]![1]!.GetValue<double>());
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void RoundTripsArraysObjectsBooleansNumbersStringsAndNulls()
    {
        string json = _serializer.Serialize(WorkflowJsonTestData.CreateComplexWorkflow());
        WorkflowDocument workflow = _serializer.Deserialize(json);

        Assert.Equal("hello", workflow.Variables["text"]!.GetValue<string>());
        Assert.Equal(3, workflow.Variables["count"]!.GetValue<int>());
        Assert.True(workflow.Variables["enabled"]!.GetValue<bool>());
        Assert.Null(workflow.Variables["nothing"]);
        Assert.Equal("two", workflow.Variables["items"]![1]!.GetValue<string>());
        Assert.Equal(1.5, workflow.Variables["obj"]!["nested"]!["value"]!.GetValue<double>());
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void RoundTripDoesNotExposeMutableJsonReferences()
    {
        WorkflowDocument workflow = _serializer.Deserialize(WorkflowJsonTestData.CreateComplexWorkflowJson());
        JsonObject parameters = workflow.Nodes[0].Parameters;
        parameters["message"] = "changed";

        Assert.Contains("\"message\": \"hello\"", _serializer.Serialize(workflow), StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void RepeatedSerializationRemainsStableAfterReturnedCloneMutation()
    {
        WorkflowDocument workflow = _serializer.Deserialize(WorkflowJsonTestData.CreateComplexWorkflowJson());
        string before = _serializer.Serialize(workflow);

        workflow.Nodes[0].Parameters["message"] = "changed";
        workflow.Variables["obj"]!["nested"]!["value"] = 99;

        string after = _serializer.Serialize(workflow);
        Assert.Equal(before, after);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void ReserializingSameWorkflowProducesEquivalentJson()
    {
        WorkflowDocument workflow = _serializer.Deserialize(WorkflowJsonTestData.CreateComplexWorkflowJson());

        Assert.Equal(_serializer.Serialize(workflow), _serializer.Serialize(workflow));
    }
}


