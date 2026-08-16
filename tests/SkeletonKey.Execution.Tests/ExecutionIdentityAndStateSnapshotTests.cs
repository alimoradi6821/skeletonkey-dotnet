using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Catalog;

namespace SkeletonKey.Execution.Tests;

/// <summary>
/// Verifies runtime identity and immutable state snapshot contracts.
/// </summary>
public sealed class ExecutionIdentityAndStateSnapshotTests
{
    /// <summary>
    /// Verifies node identity preserves every execution scope and exact definition identity.
    /// </summary>
    [Fact]
    public void NodeExecutionIdentityPreservesAllExecutionScopes()
    {
        WorkflowNodeDefinitionKey definition = new("Core.Log", 2);
        NodeExecutionIdentity identity = new(
            " Execution ",
            "Invocation-A",
            null,
            "Workflow-A",
            "Node-A",
            definition,
            "Plan-A",
            "Step-A",
            3);

        Assert.Equal(" Execution ", identity.ExecutionId);
        Assert.Equal("Invocation-A", identity.InvocationId);
        Assert.Null(identity.ParentInvocationId);
        Assert.Equal("Workflow-A", identity.WorkflowId);
        Assert.Equal("Node-A", identity.NodeId);
        Assert.Equal(definition, identity.Definition);
        Assert.Equal("Plan-A", identity.PlanId);
        Assert.Equal("Step-A", identity.StepId);
        Assert.Equal(3, identity.Attempt);
    }

    /// <summary>
    /// Verifies identity equality is ordinal, exact, and case-sensitive.
    /// </summary>
    [Fact]
    public void NodeExecutionIdentityIsExactAndCaseSensitive()
    {
        NodeExecutionIdentity lower = Identity(nodeId: "node", definition: new WorkflowNodeDefinitionKey("core.log", 1));
        NodeExecutionIdentity upper = Identity(nodeId: "Node", definition: new WorkflowNodeDefinitionKey("Core.Log", 1));

        Assert.NotEqual(lower, upper);
    }

    /// <summary>
    /// Verifies attempts are one-based.
    /// </summary>
    [Fact]
    public void NodeExecutionIdentityRejectsAttemptBelowOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Identity(attempt: 0));
    }

    /// <summary>
    /// Verifies workflow snapshots defensively copy active invocation IDs and preserve result/timestamps.
    /// </summary>
    [Fact]
    public void WorkflowSnapshotDefensivelyCopiesActiveInvocationIds()
    {
        List<string> active = ["root"];
        var created = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var result = new WorkflowExecutionResult("execution", "workflow", "root", null, WorkflowExecutionStatus.Succeeded, outputs: new Dictionary<string, JsonNode?> { ["value"] = JsonValue.Create(1) });
        WorkflowExecutionStateSnapshot snapshot = new("execution", "workflow", "plan", ExecutionLifecycleState.Completed, 7, created, created, created.AddSeconds(1), created.AddSeconds(2), active, result);

        active.Add("late");
        Assert.Equal(["root"], snapshot.ActiveInvocationIds);
        Assert.Equal(7, snapshot.Revision);
        Assert.Equal(created, snapshot.CreatedAt);
        Assert.Equal(created.AddSeconds(2), snapshot.CompletedAt);
        WorkflowExecutionResult snapshotResult = Assert.IsType<WorkflowExecutionResult>(snapshot.Result);
        Assert.Same(result, snapshotResult);
        Assert.Equal(1, snapshotResult.Outputs["value"]!.GetValue<int>());
    }

    /// <summary>
    /// Verifies invocation snapshots defensively copy active node execution IDs.
    /// </summary>
    [Fact]
    public void InvocationSnapshotDefensivelyCopiesActiveNodeIds()
    {
        List<string> active = ["node-execution"];
        WorkflowInvocationStateSnapshot snapshot = new("execution", "child", "root", "workflow", ExecutionLifecycleState.Running, 2, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null, active);

        active[0] = "mutated";
        Assert.Equal(["node-execution"], snapshot.ActiveNodeExecutionIds);
        Assert.Equal("root", snapshot.ParentInvocationId);
    }

    /// <summary>
    /// Verifies node snapshots preserve identity and terminal results.
    /// </summary>
    [Fact]
    public void NodeSnapshotPreservesExecutionIdentityAndResult()
    {
        NodeExecutionIdentity identity = Identity();
        NodeExecutionResult result = new("execution", "workflow", "invocation", "node", "core.log", NodeExecutionStatus.Succeeded, 1);
        NodeExecutionStateSnapshot snapshot = new(identity, "node-execution", ExecutionLifecycleState.Completed, 4, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, result);

        Assert.Same(identity, snapshot.Identity);
        Assert.Equal("node-execution", snapshot.NodeExecutionId);
        Assert.Same(result, snapshot.Result);
    }

    /// <summary>
    /// Verifies state transition observations preserve runtime-supplied data without mutation behavior.
    /// </summary>
    [Fact]
    public void StateTransitionPreservesRuntimeObservation()
    {
        ExecutionStateTransition transition = new(ExecutionScopeKind.Node, "execution", "invocation", "node-execution", ExecutionLifecycleState.Running, ExecutionLifecycleState.Completed, 9, DateTimeOffset.UnixEpoch, "done", "Completed");

        Assert.Equal(ExecutionScopeKind.Node, transition.Scope);
        Assert.Equal(ExecutionLifecycleState.Running, transition.Previous);
        Assert.Equal(ExecutionLifecycleState.Completed, transition.Current);
        Assert.Equal(9, transition.Revision);
        Assert.Equal("done", transition.ReasonCode);
    }

    private static NodeExecutionIdentity Identity(
        string nodeId = "node",
        WorkflowNodeDefinitionKey definition = default,
        int attempt = 1)
    {
        WorkflowNodeDefinitionKey actualDefinition = definition.Equals(default) ? new WorkflowNodeDefinitionKey("core.log", 1) : definition;
        return new NodeExecutionIdentity("execution", "invocation", "parent", "workflow", nodeId, actualDefinition, "plan", "step", attempt);
    }
}
