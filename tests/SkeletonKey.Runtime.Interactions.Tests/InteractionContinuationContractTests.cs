using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Interaction;

namespace SkeletonKey.Runtime.Interactions.Tests;

/// <summary>
/// Covers in-memory interaction continuation contracts.
/// </summary>
public sealed class InteractionContinuationContractTests
{
    /// <summary>
    /// Verifies continuation values are cloned defensively.
    /// </summary>
    [Fact]
    public void ContinuationClonesValue()
    {
        JsonObject value = new() { ["accepted"] = true };
        WorkflowInteractionContinuation continuation = new("interaction:1", WorkflowInteractionResponseStatus.Submitted, value: value);

        value["accepted"] = false;

        Assert.True(continuation.Value!["accepted"]!.GetValue<bool>());
    }

    /// <summary>
    /// Verifies pending interaction preserves the continuation identifier and timeout boundary.
    /// </summary>
    [Fact]
    public void PendingInteractionStoresContinuationMetadata()
    {
        WorkflowInteractionRequest request = new("request", "execution", "invocation", "workflow", "node", WorkflowInteractionKind.Confirmation, "Continue?");
        DateTimeOffset created = new(2026, 7, 19, 0, 0, 0, TimeSpan.Zero);
        PendingWorkflowInteraction pending = new("continue", request, created, created.AddMinutes(1));

        Assert.Equal("continue", pending.ContinuationId);
        Assert.Equal(created.AddMinutes(1), pending.ExpiresAt);
    }
}
