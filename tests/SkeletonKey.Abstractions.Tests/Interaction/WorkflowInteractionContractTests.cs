using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Interaction;

namespace SkeletonKey.Abstractions.Tests.Interaction;

/// <summary>
/// Covers host-neutral human interaction contracts.
/// </summary>
public sealed class WorkflowInteractionContractTests
{
    /// <summary>
    /// Verifies option values are defensively cloned.
    /// </summary>
    [Fact]
    public void InteractionOptionDefensivelyClonesValue()
    {
        JsonObject value = new() { ["id"] = 1 };
        WorkflowInteractionOption option = new("yes", "Yes", value: value);
        value["id"] = 2;

        JsonObject returned = option.Value!.AsObject();
        returned["id"] = 3;

        Assert.Equal(1, option.Value!["id"]!.GetValue<int>());
    }

    /// <summary>
    /// Verifies request identity and option ordering are preserved.
    /// </summary>
    [Fact]
    public void InteractionRequestPreservesIdentityAndOptionOrder()
    {
        WorkflowInteractionRequest request = new(
            "request",
            "execution",
            "invocation",
            "workflow",
            "node",
            WorkflowInteractionKind.Choice,
            "Choose",
            options: [new WorkflowInteractionOption("a", "A"), new WorkflowInteractionOption("b", "B")]);

        Assert.Equal("execution", request.ExecutionId);
        Assert.Equal("invocation", request.InvocationId);
        Assert.Equal(["a", "b"], [.. request.Options.Select(static option => option.Id)]);
    }

    /// <summary>
    /// Verifies request defaults distinguish omission from explicit JSON null.
    /// </summary>
    [Fact]
    public void InteractionRequestDistinguishesOmittedDefaultFromNullDefault()
    {
        WorkflowInteractionRequest omitted = Request(hasDefault: false, defaultValue: null);
        WorkflowInteractionRequest explicitNull = Request(hasDefault: true, defaultValue: null);

        Assert.False(omitted.HasDefault);
        Assert.True(explicitNull.HasDefault);
        Assert.Null(explicitNull.Default);
    }

    /// <summary>
    /// Verifies request default and metadata JSON are defensively cloned.
    /// </summary>
    [Fact]
    public void InteractionRequestDefensivelyClonesDefaultAndMetadata()
    {
        JsonObject defaultValue = new() { ["name"] = "initial" };
        JsonObject metadata = new() { ["source"] = "host" };
        WorkflowInteractionRequest request = Request(hasDefault: true, defaultValue: defaultValue, metadata: metadata);
        defaultValue["name"] = "changed";
        metadata["source"] = "changed";

        request.Default!.AsObject()["name"] = "mutated";
        request.Metadata!["source"] = "mutated";

        Assert.Equal("initial", request.Default!["name"]!.GetValue<string>());
        Assert.Equal("host", request.Metadata!["source"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies response values distinguish omission from explicit JSON null.
    /// </summary>
    [Fact]
    public void InteractionResponseDistinguishesMissingValueFromExplicitNull()
    {
        WorkflowInteractionResponse missing = new("request", WorkflowInteractionResponseStatus.Cancelled, false, null, DateTimeOffset.UnixEpoch);
        WorkflowInteractionResponse explicitNull = new("request", WorkflowInteractionResponseStatus.Submitted, true, null, DateTimeOffset.UnixEpoch);

        Assert.False(missing.HasValue);
        Assert.True(explicitNull.HasValue);
        Assert.Null(explicitNull.Value);
    }

    /// <summary>
    /// Verifies response values are defensively cloned.
    /// </summary>
    [Fact]
    public void InteractionResponseDefensivelyClonesValue()
    {
        JsonObject value = new() { ["answer"] = "yes" };
        WorkflowInteractionResponse response = new("request", WorkflowInteractionResponseStatus.Submitted, true, value, DateTimeOffset.UnixEpoch);
        value["answer"] = "no";

        response.Value!.AsObject()["answer"] = "mutated";

        Assert.Equal("yes", response.Value!["answer"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies secret interaction kind remains distinct.
    /// </summary>
    [Fact]
    public void SecretInteractionKindRemainsDistinct()
    {
        Assert.NotEqual(WorkflowInteractionKind.Text, WorkflowInteractionKind.Secret);
    }

    private static WorkflowInteractionRequest Request(bool hasDefault, JsonNode? defaultValue, JsonObject? metadata = null)
    {
        return new WorkflowInteractionRequest(
            "request",
            "execution",
            "invocation",
            "workflow",
            "node",
            WorkflowInteractionKind.Text,
            "Prompt",
            hasDefault: hasDefault,
            defaultValue: defaultValue,
            metadata: metadata);
    }
}
