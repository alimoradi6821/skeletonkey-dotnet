namespace SkeletonKey.Evaluation;

/// <summary>
/// Defines deterministic safety limits for workflow value evaluation and materialization.
/// </summary>
/// <remarks>
/// Defaults are host-independent. Hosts may supply stricter limits. No time, memory, clock, or machine-specific limits are used.
/// </remarks>
public sealed class WorkflowValueProcessingLimits
{
    /// <summary>
    /// Initializes a new limits contract.
    /// </summary>
    /// <param name="maximumMaterializationDepth">The maximum recursive materialization depth.</param>
    /// <param name="maximumExpressionOperations">The maximum expression evaluation operation count.</param>
    /// <param name="maximumResultDepth">The maximum JSON result depth.</param>
    /// <param name="maximumCollectionItems">The maximum items allowed in any produced array or object.</param>
    /// <param name="maximumStringLength">The maximum length allowed for any produced string.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any limit is not positive.</exception>
    public WorkflowValueProcessingLimits(
        int maximumMaterializationDepth = 64,
        int maximumExpressionOperations = 4096,
        int maximumResultDepth = 128,
        int maximumCollectionItems = 10000,
        int maximumStringLength = 1048576)
    {
        ThrowIfNonPositive(maximumMaterializationDepth, nameof(maximumMaterializationDepth));
        ThrowIfNonPositive(maximumExpressionOperations, nameof(maximumExpressionOperations));
        ThrowIfNonPositive(maximumResultDepth, nameof(maximumResultDepth));
        ThrowIfNonPositive(maximumCollectionItems, nameof(maximumCollectionItems));
        ThrowIfNonPositive(maximumStringLength, nameof(maximumStringLength));
        MaximumMaterializationDepth = maximumMaterializationDepth;
        MaximumExpressionOperations = maximumExpressionOperations;
        MaximumResultDepth = maximumResultDepth;
        MaximumCollectionItems = maximumCollectionItems;
        MaximumStringLength = maximumStringLength;
    }

    /// <summary>
    /// Gets the safe default limits.
    /// </summary>
    public static WorkflowValueProcessingLimits Default { get; } = new();

    /// <summary>
    /// Gets the maximum recursive materialization depth.
    /// </summary>
    public int MaximumMaterializationDepth { get; }

    /// <summary>
    /// Gets the maximum expression evaluation operation count.
    /// </summary>
    public int MaximumExpressionOperations { get; }

    /// <summary>
    /// Gets the maximum JSON result depth.
    /// </summary>
    public int MaximumResultDepth { get; }

    /// <summary>
    /// Gets the maximum items allowed in any produced array or object.
    /// </summary>
    public int MaximumCollectionItems { get; }

    /// <summary>
    /// Gets the maximum length allowed for any produced string.
    /// </summary>
    public int MaximumStringLength { get; }

    private static void ThrowIfNonPositive(int value, string name)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Workflow value processing limits must be positive.");
        }
    }
}
