using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Events;
using SkeletonKey.Abstractions.Execution;

namespace SkeletonKey.Abstractions.Tests.Execution;

/// <summary>
/// Covers host-neutral execution result and event contracts.
/// </summary>
public sealed class ExecutionContractTests
{
    /// <summary>
    /// Verifies workflow outcomes defensively clone JSON data supplied by callers.
    /// </summary>
    [Fact]
    public void WorkflowOutcomeDefensivelyClonesData()
    {
        JsonObject data = new()
        {
            ["value"] = 1,
        };

        WorkflowOutcome outcome = new(WorkflowOutcomeKind.Success, "done", data: data);
        data["value"] = 2;

        Assert.Equal(1, outcome.Data!["value"]!.GetValue<int>());
    }

    /// <summary>
    /// Verifies returned workflow outcome data cannot mutate internal state.
    /// </summary>
    [Fact]
    public void ReturnedWorkflowOutcomeDataCannotMutateInternalState()
    {
        WorkflowOutcome outcome = new(
            WorkflowOutcomeKind.Success,
            "done",
            data: new JsonObject
            {
                ["value"] = 1,
            });

        outcome.Data!["value"] = 2;

        Assert.Equal(1, outcome.Data!["value"]!.GetValue<int>());
    }

    /// <summary>
    /// Verifies workflow errors defensively clone JSON details supplied by callers.
    /// </summary>
    [Fact]
    public void WorkflowErrorDefensivelyClonesDetails()
    {
        JsonObject details = new()
        {
            ["attempt"] = 1,
        };

        WorkflowError error = new("failed", "Failed.", details: details);
        details["attempt"] = 2;

        Assert.Equal(1, error.Details!["attempt"]!.GetValue<int>());
    }

    /// <summary>
    /// Verifies workflow execution results default final outputs to an empty dictionary.
    /// </summary>
    [Fact]
    public void WorkflowExecutionResultDefaultsOutputsToEmpty()
    {
        WorkflowExecutionResult result = new("execution", "workflow", "root", null, WorkflowExecutionStatus.Succeeded);

        Assert.Empty(result.Outputs);
    }

    /// <summary>
    /// Verifies workflow execution results defensively copy final output values.
    /// </summary>
    [Fact]
    public void WorkflowExecutionResultDefensivelyCopiesOutputs()
    {
        JsonObject value = new()
        {
            ["count"] = 1,
        };
        Dictionary<string, JsonNode?> outputs = new()
        {
            ["result"] = value,
        };

        WorkflowExecutionResult result = new("execution", "workflow", "root", null, WorkflowExecutionStatus.Succeeded, outputs: outputs);
        value["count"] = 2;
        outputs["other"] = true;

        Assert.Single(result.Outputs);
        Assert.Equal(1, result.Outputs["result"]!["count"]!.GetValue<int>());
    }

    /// <summary>
    /// Verifies returned workflow output values cannot mutate internal state.
    /// </summary>
    [Fact]
    public void ReturnedWorkflowResultOutputsCannotMutateInternalState()
    {
        WorkflowExecutionResult result = new(
            "execution",
            "workflow",
            "root",
            null,
            WorkflowExecutionStatus.Succeeded,
            outputs: new Dictionary<string, JsonNode?>
            {
                ["result"] = new JsonObject
                {
                    ["count"] = 1,
                },
            });

        result.Outputs["result"]!["count"] = 2;

        Assert.Equal(1, result.Outputs["result"]!["count"]!.GetValue<int>());
    }

    /// <summary>
    /// Verifies node execution results defensively copy output port values.
    /// </summary>
    [Fact]
    public void NodeExecutionResultDefensivelyCopiesOutputPorts()
    {
        JsonObject value = new()
        {
            ["count"] = 1,
        };

        NodeExecutionResult result = new(
            "execution",
            "workflow",
            "root",
            "node",
            "core.log",
            NodeExecutionStatus.Succeeded,
            1,
            outputs: new Dictionary<string, JsonNode?>
            {
                ["main"] = value,
            });
        value["count"] = 2;
        result.Outputs["main"]!["count"] = 3;

        Assert.Equal(1, result.Outputs["main"]!["count"]!.GetValue<int>());
    }

    /// <summary>
    /// Verifies workflow output events defensively clone JSON payloads.
    /// </summary>
    [Fact]
    public void WorkflowOutputEventDefensivelyClonesPayload()
    {
        JsonObject payload = new()
        {
            ["record"] = 1,
        };

        WorkflowOutputEvent workflowEvent = new("event", "execution", "workflow", "root", null, DateTimeOffset.UnixEpoch, "records", "records", 0, payload);
        payload["record"] = 2;
        workflowEvent.Payload!["record"] = 3;

        Assert.Equal(1, workflowEvent.Payload!["record"]!.GetValue<int>());
    }

    /// <summary>
    /// Verifies workflow log events defensively clone JSON data.
    /// </summary>
    [Fact]
    public void WorkflowLogEventDefensivelyClonesData()
    {
        JsonObject data = new()
        {
            ["line"] = 1,
        };

        WorkflowLogEvent workflowEvent = new("event", "execution", "workflow", "root", null, DateTimeOffset.UnixEpoch, WorkflowLogLevel.Information, "message", data: data);
        data["line"] = 2;
        workflowEvent.Data!["line"] = 3;

        Assert.Equal(1, workflowEvent.Data!["line"]!.GetValue<int>());
    }

    /// <summary>
    /// Verifies workflow progress events defensively clone JSON data.
    /// </summary>
    [Fact]
    public void WorkflowProgressEventDefensivelyClonesData()
    {
        JsonObject data = new()
        {
            ["step"] = 1,
        };

        WorkflowProgressEvent workflowEvent = new("event", "execution", "workflow", "root", null, DateTimeOffset.UnixEpoch, 1, data: data);
        data["step"] = 2;
        workflowEvent.Data!["step"] = 3;

        Assert.Equal(1, workflowEvent.Data!["step"]!.GetValue<int>());
    }

    /// <summary>
    /// Verifies execution status and business outcome remain separate concepts.
    /// </summary>
    [Fact]
    public void ExecutionStatusAndBusinessOutcomeRemainSeparateConcepts()
    {
        WorkflowExecutionResult result = new(
            "execution",
            "workflow",
            "root",
            null,
            WorkflowExecutionStatus.Succeeded,
            outcome: new WorkflowOutcome(WorkflowOutcomeKind.Skipped, "business-skipped"));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Status);
        Assert.Equal(WorkflowOutcomeKind.Skipped, result.Outcome!.Kind);
    }

    /// <summary>
    /// Verifies required-action outcomes can coexist with a successful technical execution.
    /// </summary>
    [Fact]
    public void RequiresActionCanCoexistWithSucceededExecutionStatus()
    {
        WorkflowExecutionResult result = new(
            "execution",
            "workflow",
            "root",
            null,
            WorkflowExecutionStatus.Succeeded,
            outcome: new WorkflowOutcome(WorkflowOutcomeKind.RequiresAction, "approval-required"));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Status);
        Assert.Equal(WorkflowOutcomeKind.RequiresAction, result.Outcome!.Kind);
    }

    /// <summary>
    /// Verifies streamed records are not required in final workflow outputs.
    /// </summary>
    [Fact]
    public void StreamedRecordsAreNotRequiredInFinalOutputs()
    {
        WorkflowExecutionResult result = new("execution", "workflow", "root", null, WorkflowExecutionStatus.Succeeded);
        WorkflowOutputEvent streamedRecord = new("event", "execution", "workflow", "root", null, DateTimeOffset.UnixEpoch, "records", "records", 0, new JsonObject());

        Assert.Empty(result.Outputs);
        Assert.Equal("records", streamedRecord.Channel);
    }

    /// <summary>
    /// Verifies workflow results carry execution and invocation identities.
    /// </summary>
    [Fact]
    public void WorkflowResultCarriesExecutionAndInvocationIdentities()
    {
        WorkflowExecutionResult result = new("execution", "workflow", "root", null, WorkflowExecutionStatus.Succeeded);

        Assert.Equal("execution", result.ExecutionId);
        Assert.Equal("workflow", result.WorkflowId);
        Assert.Equal("root", result.InvocationId);
    }

    /// <summary>
    /// Verifies root workflow results allow a null parent invocation identifier.
    /// </summary>
    [Fact]
    public void RootWorkflowResultAllowsNullParentInvocationId()
    {
        WorkflowExecutionResult result = new("execution", "workflow", "root", null, WorkflowExecutionStatus.Succeeded);

        Assert.Null(result.ParentInvocationId);
    }

    /// <summary>
    /// Verifies child workflow results allow a parent invocation identifier.
    /// </summary>
    [Fact]
    public void ChildWorkflowResultAllowsParentInvocationId()
    {
        WorkflowExecutionResult result = new("execution", "child", "child-invocation", "root", WorkflowExecutionStatus.Succeeded);

        Assert.Equal("child-invocation", result.InvocationId);
        Assert.Equal("root", result.ParentInvocationId);
    }

    /// <summary>
    /// Verifies workflow events carry workflow invocation identity.
    /// </summary>
    [Fact]
    public void WorkflowEventCarriesWorkflowInvocationIdentity()
    {
        WorkflowLogEvent workflowEvent = new("event", "execution", "workflow", "root", null, DateTimeOffset.UnixEpoch, WorkflowLogLevel.Information, "message");

        Assert.Equal("root", workflowEvent.InvocationId);
    }

    /// <summary>
    /// Verifies child workflow events share the root execution identifier.
    /// </summary>
    [Fact]
    public void ChildWorkflowEventsShareRootExecutionId()
    {
        WorkflowOutputEvent workflowEvent = new("event", "execution", "child", "child-invocation", "root", DateTimeOffset.UnixEpoch, "records", "records", 0);

        Assert.Equal("execution", workflowEvent.ExecutionId);
        Assert.Equal("child-invocation", workflowEvent.InvocationId);
        Assert.Equal("root", workflowEvent.ParentInvocationId);
    }

    /// <summary>
    /// Verifies node results carry execution, invocation, and workflow identifiers.
    /// </summary>
    [Fact]
    public void NodeResultCarriesExecutionInvocationAndWorkflowIds()
    {
        NodeExecutionResult result = new("execution", "workflow", "root", "node", "core.log", NodeExecutionStatus.Succeeded, 1);

        Assert.Equal("execution", result.ExecutionId);
        Assert.Equal("workflow", result.WorkflowId);
        Assert.Equal("root", result.InvocationId);
    }

    /// <summary>
    /// Verifies identity additions preserve immutable result behavior.
    /// </summary>
    [Fact]
    public void IdentityAdditionsPreserveImmutableResultBehavior()
    {
        JsonObject output = new()
        {
            ["value"] = 1,
        };
        WorkflowExecutionResult result = new(
            "execution",
            "workflow",
            "root",
            null,
            WorkflowExecutionStatus.Succeeded,
            outputs: new Dictionary<string, JsonNode?>
            {
                ["result"] = output,
            });

        output["value"] = 2;
        result.Outputs["result"]!["value"] = 3;

        Assert.Equal(1, result.Outputs["result"]!["value"]!.GetValue<int>());
    }
}
