using System.Collections;
using System.Text.Json.Nodes;
using SkeletonKey.Workflow.Bindings;
using SkeletonKey.Workflow.Invocation;
using SkeletonKey.Workflow.References;

namespace SkeletonKey.Workflow.Tests.Invocation;

/// <summary>
/// Covers workflow reference, binding, and invocation stream contracts.
/// </summary>
public sealed class WorkflowInvocationContractTests
{
    /// <summary>
    /// Verifies workflow references preserve ID and optional version.
    /// </summary>
    [Fact]
    public void WorkflowReferencePreservesIdAndOptionalVersion()
    {
        WorkflowReference pinned = new("bale-check-account", "1.0.0");
        WorkflowReference unpinned = new("bale-check-account");

        Assert.Equal("bale-check-account", pinned.Id);
        Assert.Equal("1.0.0", pinned.Version);
        Assert.Null(unpinned.Version);
    }

    /// <summary>
    /// Verifies workflow bindings default the path to the root pointer.
    /// </summary>
    [Fact]
    public void WorkflowBindingDefaultsPathToRoot()
    {
        WorkflowBinding binding = new(WorkflowBindingSource.Input, name: "account");

        Assert.Equal(string.Empty, binding.Path);
    }

    /// <summary>
    /// Verifies workflow bindings default missing behavior to error.
    /// </summary>
    [Fact]
    public void WorkflowBindingDefaultsOnMissingToError()
    {
        WorkflowBinding binding = new(WorkflowBindingSource.Input, name: "account");

        Assert.Equal(WorkflowBindingMissingBehavior.Error, binding.OnMissing);
    }

    /// <summary>
    /// Verifies workflow bindings preserve an omitted default.
    /// </summary>
    [Fact]
    public void WorkflowBindingPreservesOmittedDefault()
    {
        WorkflowBinding binding = new(WorkflowBindingSource.Input, name: "account");

        Assert.False(binding.HasDefault);
        Assert.Null(binding.Default);
    }

    /// <summary>
    /// Verifies workflow bindings preserve an explicit JSON null default.
    /// </summary>
    [Fact]
    public void WorkflowBindingPreservesExplicitNullDefault()
    {
        WorkflowBinding binding = new(WorkflowBindingSource.Input, name: "account", onMissing: WorkflowBindingMissingBehavior.Default, hasDefault: true);

        Assert.True(binding.HasDefault);
        Assert.Null(binding.Default);
    }

    /// <summary>
    /// Verifies workflow bindings defensively clone default values.
    /// </summary>
    [Fact]
    public void WorkflowBindingDefensivelyClonesDefault()
    {
        JsonObject defaultValue = new()
        {
            ["value"] = 1,
        };

        WorkflowBinding binding = new(WorkflowBindingSource.Input, name: "account", defaultValue: defaultValue, hasDefault: true);
        defaultValue["value"] = 2;
        binding.Default!["value"] = 3;

        Assert.Equal(1, binding.Default!["value"]!.GetValue<int>());
    }

    /// <summary>
    /// Verifies invocation stream policies default mappings to empty.
    /// </summary>
    [Fact]
    public void InvocationStreamPolicyDefaultsMappingsToEmpty()
    {
        WorkflowInvocationStreamPolicy policy = new();

        Assert.Equal(WorkflowInvocationStreamMode.Forward, policy.Mode);
        Assert.Empty(policy.Mappings);
    }

    /// <summary>
    /// Verifies invocation stream policies defensively copy mappings.
    /// </summary>
    [Fact]
    public void InvocationStreamPolicyDefensivelyCopiesMappings()
    {
        Dictionary<string, string> mappings = new()
        {
            ["child"] = "parent",
        };

        WorkflowInvocationStreamPolicy policy = new(WorkflowInvocationStreamMode.Map, mappings);
        mappings["other"] = "parent";

        Assert.Single(policy.Mappings);
    }

    /// <summary>
    /// Verifies invocation stream policies do not expose mutable mappings.
    /// </summary>
    [Fact]
    public void InvocationStreamPolicyExposesImmutableMappings()
    {
        WorkflowInvocationStreamPolicy policy = new(
            WorkflowInvocationStreamMode.Map,
            new Dictionary<string, string>
            {
                ["child"] = "parent",
            });

        Assert.Throws<NotSupportedException>(() => ((IDictionary)policy.Mappings).Add("other", "parent"));
    }
}
