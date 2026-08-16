using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;

namespace SkeletonKey.Abstractions.Tests.Execution;

/// <summary>
/// Covers host-neutral iteration context contracts.
/// </summary>
public sealed class WorkflowIterationContextTests
{
    /// <summary>
    /// Verifies iteration context identity and counters are preserved.
    /// </summary>
    [Fact]
    public void PreservesIdentityAndCounters()
    {
        WorkflowIterationContext context = new("process-contacts", 0, 1, count: 3);

        Assert.Equal("process-contacts", context.IterationId);
        Assert.Equal(0, context.Index);
        Assert.Equal(1, context.Number);
        Assert.Equal(3, context.Count);
    }

    /// <summary>
    /// Verifies explicit JSON null item presence is distinguishable from absence.
    /// </summary>
    [Fact]
    public void DistinguishesAbsentItemFromExplicitNull()
    {
        WorkflowIterationContext absent = new("repeat", 0, 1);
        WorkflowIterationContext explicitNull = new("foreach", 0, 1, hasItem: true);

        Assert.False(absent.HasItem);
        Assert.True(explicitNull.HasItem);
        Assert.Null(explicitNull.Item);
    }

    /// <summary>
    /// Verifies item JSON is defensively cloned.
    /// </summary>
    [Fact]
    public void DefensivelyClonesItem()
    {
        JsonObject item = new()
        {
            ["name"] = "Ada",
        };

        WorkflowIterationContext context = new("foreach", 0, 1, item, hasItem: true, count: 1);
        item["name"] = "Grace";
        context.Item!["name"] = "Katherine";

        Assert.Equal("Ada", context.Item!["name"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies count can be absent for future streaming iteration contexts.
    /// </summary>
    [Fact]
    public void SupportsAbsentCount()
    {
        WorkflowIterationContext context = new("streaming", 4, 5, JsonValue.Create("item"), hasItem: true);

        Assert.Null(context.Count);
    }
}
