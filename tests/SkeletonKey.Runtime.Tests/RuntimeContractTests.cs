using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Events;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Runtime;
using SkeletonKey.Workflow.Documents;

namespace SkeletonKey.Runtime.Tests;

/// <summary>
/// Covers runtime request, options, state-store, and event contracts.
/// </summary>
public sealed class RuntimeContractTests
{
    /// <summary>
    /// Verifies execution requests clone JSON inputs and variables and use a no-op sink when omitted.
    /// </summary>
    [Fact]
    public void WorkflowExecutionRequestClonesInputsVariablesAndSuppliesNoOpSink()
    {
        JsonObject input = new() { ["value"] = 1 };
        JsonObject variable = new() { ["value"] = 2 };
        WorkflowExecutionRequest request = new(
            new WorkflowDocument(id: "workflow", name: "Workflow"),
            "execution",
            "plan",
            new Dictionary<string, JsonNode?> { ["input"] = input },
            new Dictionary<string, JsonNode?> { ["variable"] = variable });

        input["value"] = 10;
        variable["value"] = 20;

        Assert.Equal(1, request.Inputs["input"]!["value"]!.GetValue<int>());
        Assert.Equal(2, request.Variables["variable"]!["value"]!.GetValue<int>());
        Assert.NotNull(request.EventSink);
    }

    /// <summary>
    /// Verifies runtime options expose deterministic safe defaults and reject invalid limits.
    /// </summary>
    [Fact]
    public void RuntimeOptionsExposeSafeDefaultsAndValidateLimits()
    {
        WorkflowRuntimeOptions options = new();

        Assert.Equal(10_000, options.MaximumExecutedNodeAttempts);
        Assert.True(options.StopOnFirstUnhandledFailure);
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowRuntimeOptions(maximumExecutedNodeAttempts: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowRuntimeOptions(maximumReadySteps: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowRuntimeOptions(maximumStoredNodeResults: 0));
    }

    /// <summary>
    /// Verifies execution, invocation, and node snapshots are created in Created state.
    /// </summary>
    [Fact]
    public void StateStoreCreatesExecutionInvocationAndNodeSnapshots()
    {
        InMemoryExecutionStateStore store = new();
        DateTimeOffset now = new(2026, 7, 19, 1, 0, 0, TimeSpan.Zero);
        WorkflowExecutionStateSnapshot execution = store.CreateExecution("execution", "workflow", "plan", now);
        WorkflowInvocationStateSnapshot invocation = store.CreateInvocation("execution", "invocation", null, "workflow", now);
        NodeExecutionIdentity identity = new("execution", "invocation", null, "workflow", "node", new WorkflowNodeDefinitionKey("demo.node", 1), "plan", "step", 1);
        NodeExecutionStateSnapshot node = store.CreateNode(identity, "node-execution", now);

        Assert.Equal(ExecutionLifecycleState.Created, execution.State);
        Assert.Equal(ExecutionLifecycleState.Created, invocation.State);
        Assert.Equal(ExecutionLifecycleState.Created, node.State);
        Assert.Equal(1, execution.Revision);
    }

    /// <summary>
    /// Verifies legal lifecycle transitions increment revisions and preserve timestamps.
    /// </summary>
    [Fact]
    public void StateStoreAppliesLegalTransitionsAndIncrementsRevisions()
    {
        InMemoryExecutionStateStore store = new();
        DateTimeOffset created = new(2026, 7, 19, 1, 0, 0, TimeSpan.Zero);
        DateTimeOffset ready = created.AddSeconds(1);
        DateTimeOffset running = created.AddSeconds(2);
        store.CreateExecution("execution", "workflow", "plan", created);

        store.TransitionExecution("execution", ExecutionLifecycleState.Ready, ready);
        WorkflowExecutionStateSnapshot snapshot = store.TransitionExecution("execution", ExecutionLifecycleState.Running, running);

        Assert.Equal(3, snapshot.Revision);
        Assert.Equal(created, snapshot.CreatedAt);
        Assert.Equal(running, snapshot.StartedAt);
    }

    /// <summary>
    /// Verifies invalid transitions and transitions after completion are rejected.
    /// </summary>
    [Fact]
    public void StateStoreRejectsInvalidTransitions()
    {
        InMemoryExecutionStateStore store = new();
        DateTimeOffset now = new(2026, 7, 19, 1, 0, 0, TimeSpan.Zero);
        store.CreateExecution("execution", "workflow", "plan", now);

        Assert.Throws<InvalidOperationException>(() => store.TransitionExecution("execution", ExecutionLifecycleState.Completed, now));
        store.TransitionExecution("execution", ExecutionLifecycleState.Ready, now);
        store.TransitionExecution("execution", ExecutionLifecycleState.Running, now);
        store.TransitionExecution("execution", ExecutionLifecycleState.Completed, now);
        Assert.Throws<InvalidOperationException>(() => store.TransitionExecution("execution", ExecutionLifecycleState.Running, now));
    }

    /// <summary>
    /// Verifies state-store snapshots remain immutable to callers.
    /// </summary>
    [Fact]
    public void StateStoreReturnsImmutableSnapshots()
    {
        InMemoryExecutionStateStore store = new();
        DateTimeOffset now = new(2026, 7, 19, 1, 0, 0, TimeSpan.Zero);
        store.CreateExecution("execution", "workflow", "plan", now);

        WorkflowExecutionStateSnapshot first = store.GetExecution("execution");
        store.TransitionExecution("execution", ExecutionLifecycleState.Ready, now.AddSeconds(1));
        WorkflowExecutionStateSnapshot second = store.GetExecution("execution");

        Assert.Equal(ExecutionLifecycleState.Created, first.State);
        Assert.Equal(ExecutionLifecycleState.Ready, second.State);
    }

    /// <summary>
    /// Verifies the in-memory state store can create independent executions concurrently.
    /// </summary>
    [Fact]
    public void StateStoreIsThreadSafeForIndependentExecutions()
    {
        InMemoryExecutionStateStore store = new();
        DateTimeOffset now = new(2026, 7, 19, 1, 0, 0, TimeSpan.Zero);

        Parallel.For(0, 100, index =>
        {
            string executionId = "execution-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            store.CreateExecution(executionId, "workflow", "plan", now);
            store.TransitionExecution(executionId, ExecutionLifecycleState.Ready, now);
        });

        Assert.Equal(ExecutionLifecycleState.Ready, store.GetExecution("execution-42").State);
    }

    /// <summary>
    /// Verifies runtime events enforce one-based sequences and defensive JSON cloning.
    /// </summary>
    [Fact]
    public void RuntimeWorkflowEventPreservesSequenceAndClonesData()
    {
        JsonObject data = new() { ["value"] = 1 };
        RuntimeWorkflowEvent workflowEvent = new(
            "event",
            1,
            "execution",
            "workflow",
            "invocation",
            null,
            new DateTimeOffset(2026, 7, 19, 1, 0, 0, TimeSpan.Zero),
            RuntimeWorkflowEventKind.ExecutionStarted,
            "started",
            data: data);

        data["value"] = 2;

        Assert.Equal(1, workflowEvent.Sequence);
        Assert.Equal(1, workflowEvent.Data!["value"]!.GetValue<int>());
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeWorkflowEvent("bad", 0, "execution", "workflow", "invocation", null, DateTimeOffset.UtcNow, RuntimeWorkflowEventKind.ExecutionStarted));
    }

    private sealed class Sink : IWorkflowEventSink
    {
        public ValueTask PublishAsync(WorkflowEvent workflowEvent, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }
    }
}
