namespace SkeletonKey.Planning.Default;

/// <summary>
/// Defines immutable deterministic options for <see cref="DefaultWorkflowExecutionPlanner" />.
/// </summary>
/// <remarks>
/// Planning options contain no callbacks, time-based values, randomness, handlers, or service locators.
/// </remarks>
public sealed class ExecutionPlanningOptions
{
    /// <summary>
    /// Initializes execution-planning options.
    /// </summary>
    /// <param name="allowWarnings">Whether warnings are allowed when producing a ready plan.</param>
    /// <param name="allowDeprecatedNodes">Whether deprecated-node warnings are allowed when producing a ready plan.</param>
    /// <param name="requireCompleteResourceBindings">Whether unresolved required resource uses block planning.</param>
    /// <param name="maximumSteps">The positive maximum number of plan steps.</param>
    /// <param name="maximumDependencies">The positive maximum number of plan dependencies.</param>
    /// <param name="maximumIssues">The positive maximum number of planning issues.</param>
    public ExecutionPlanningOptions(
        bool allowWarnings = true,
        bool allowDeprecatedNodes = true,
        bool requireCompleteResourceBindings = true,
        int maximumSteps = 10000,
        int maximumDependencies = 50000,
        int maximumIssues = 1024)
    {
        if (maximumSteps < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumSteps), "Maximum step count must be positive.");
        }

        if (maximumDependencies < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDependencies), "Maximum dependency count must be positive.");
        }

        if (maximumIssues < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumIssues), "Maximum issue count must be positive.");
        }

        AllowWarnings = allowWarnings;
        AllowDeprecatedNodes = allowDeprecatedNodes;
        RequireCompleteResourceBindings = requireCompleteResourceBindings;
        MaximumSteps = maximumSteps;
        MaximumDependencies = maximumDependencies;
        MaximumIssues = maximumIssues;
    }

    /// <summary>Gets deterministic default planning options.</summary>
    public static ExecutionPlanningOptions Default { get; } = new();

    /// <summary>Gets whether warnings are allowed when producing a ready plan.</summary>
    public bool AllowWarnings { get; }

    /// <summary>Gets whether deprecated-node warnings are allowed when producing a ready plan.</summary>
    public bool AllowDeprecatedNodes { get; }

    /// <summary>Gets whether unresolved required resource uses block planning.</summary>
    public bool RequireCompleteResourceBindings { get; }

    /// <summary>Gets the positive maximum number of plan steps.</summary>
    public int MaximumSteps { get; }

    /// <summary>Gets the positive maximum number of plan dependencies.</summary>
    public int MaximumDependencies { get; }

    /// <summary>Gets the positive maximum number of planning issues.</summary>
    public int MaximumIssues { get; }
}
