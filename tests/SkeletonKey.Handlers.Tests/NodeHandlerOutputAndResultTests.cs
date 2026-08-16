using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Execution;

namespace SkeletonKey.Handlers.Tests;

/// <summary>
/// Verifies node handler output and result contracts.
/// </summary>
public sealed class NodeHandlerOutputAndResultTests
{
    /// <summary>
    /// Verifies control outputs preserve order and reject duplicates.
    /// </summary>
    [Fact]
    public void ControlOutputsPreserveOrderAndRejectDuplicates()
    {
        NodeHandlerOutputs outputs = new(["true", "done"]);

        Assert.Equal(["true", "done"], outputs.ActivatedControlOutputs);
        Assert.Throws<ArgumentException>(() => new NodeHandlerOutputs(["done", "done"]));
    }

    /// <summary>
    /// Verifies data outputs are immutable and preserve JSON value order.
    /// </summary>
    [Fact]
    public void DataOutputsAreImmutable()
    {
        JsonObject payload = new() { ["value"] = 1 };
        Dictionary<string, NodePortValueSet> data = new(StringComparer.Ordinal)
        {
            ["result"] = new([payload]),
        };

        NodeHandlerOutputs outputs = new(dataOutputs: data);
        payload["value"] = 99;
        data["late"] = new();

        Assert.Equal(["result"], outputs.DataOutputs.Keys);
        Assert.Equal(1, outputs.DataOutputs["result"].Values[0]!["value"]!.GetValue<int>());
    }

    /// <summary>
    /// Verifies success results preserve outputs and omit runtime identity.
    /// </summary>
    [Fact]
    public void SuccessResultPreservesOutputsAndContainsNoRuntimeIdentity()
    {
        NodeHandlerOutputs outputs = new(["completed"]);
        var result = NodeHandlerResult.Success(outputs);

        Assert.Equal(NodeHandlerCompletionStatus.Succeeded, result.Status);
        Assert.Same(outputs, result.Outputs);
        Assert.Null(result.Error);
        Assert.DoesNotContain(typeof(NodeHandlerResult).GetProperties(), static property => property.Name.Contains("ExecutionId", StringComparison.Ordinal));
        Assert.DoesNotContain(typeof(NodeHandlerResult).GetProperties(), static property => property.Name.Contains("Timestamp", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies failure results require and preserve a structured workflow error.
    /// </summary>
    [Fact]
    public void FailureResultPreservesStructuredError()
    {
        WorkflowError error = new("expected.failure", "Expected failure.");
        var result = NodeHandlerResult.Failure(error);

        Assert.Equal(NodeHandlerCompletionStatus.Failed, result.Status);
        Assert.Same(error, result.Error);
        Assert.Throws<ArgumentException>(() => new NodeHandlerResult(NodeHandlerCompletionStatus.Failed));
    }

    /// <summary>
    /// Verifies cancellation remains distinct from failure and may omit an error.
    /// </summary>
    [Fact]
    public void CancelledResultRemainsDistinct()
    {
        var result = NodeHandlerResult.Cancelled();

        Assert.Equal(NodeHandlerCompletionStatus.Cancelled, result.Status);
        Assert.Null(result.Error);
    }

    /// <summary>
    /// Verifies metadata is defensively cloned.
    /// </summary>
    [Fact]
    public void MetadataIsDefensivelyCloned()
    {
        JsonObject metadata = new() { ["attempt"] = 1 };
        var result = NodeHandlerResult.Success(metadata: metadata);

        metadata["attempt"] = 2;
        Assert.Equal(1, result.Metadata!["attempt"]!.GetValue<int>());

        JsonObject returned = result.Metadata!;
        returned["attempt"] = 3;
        Assert.Equal(1, result.Metadata!["attempt"]!.GetValue<int>());
    }
}
