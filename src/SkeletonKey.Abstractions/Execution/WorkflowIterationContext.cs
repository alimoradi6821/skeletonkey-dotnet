using System.Text.Json.Nodes;

namespace SkeletonKey.Abstractions.Execution;

/// <summary>
/// Represents an immutable host-neutral iteration context for a future workflow runtime.
/// </summary>
/// <remarks>
/// JSON item values are defensively cloned. <see cref="HasItem" /> distinguishes an absent item from an
/// explicit JSON null item. <see cref="Index" /> is zero-based and <see cref="Number" /> is one-based.
/// </remarks>
public sealed class WorkflowIterationContext
{
    private readonly JsonNode? _item;

    /// <summary>
    /// Initializes a new iteration context contract.
    /// </summary>
    /// <param name="iterationId">The explicit loop node identifier for this iteration context.</param>
    /// <param name="index">The zero-based iteration index.</param>
    /// <param name="number">The one-based iteration number.</param>
    /// <param name="item">The optional item value for foreach iterations.</param>
    /// <param name="hasItem">Whether an item is present, including explicit JSON null.</param>
    /// <param name="count">The optional known iteration count.</param>
    public WorkflowIterationContext(
        string iterationId,
        long index,
        long number,
        JsonNode? item = null,
        bool hasItem = false,
        long? count = null)
    {
        IterationId = iterationId;
        Index = index;
        Number = number;
        _item = item?.DeepClone();
        HasItem = hasItem;
        Count = count;
    }

    /// <summary>
    /// Gets the explicit loop node identifier for this iteration context.
    /// </summary>
    public string IterationId { get; }

    /// <summary>
    /// Gets the zero-based iteration index.
    /// </summary>
    public long Index { get; }

    /// <summary>
    /// Gets the one-based iteration number.
    /// </summary>
    public long Number { get; }

    /// <summary>
    /// Gets a defensive copy of the optional item value for foreach iterations.
    /// </summary>
    public JsonNode? Item => _item?.DeepClone();

    /// <summary>
    /// Gets a value indicating whether an item is present, including explicit JSON null.
    /// </summary>
    public bool HasItem { get; }

    /// <summary>
    /// Gets the optional known iteration count.
    /// </summary>
    public long? Count { get; }
}
