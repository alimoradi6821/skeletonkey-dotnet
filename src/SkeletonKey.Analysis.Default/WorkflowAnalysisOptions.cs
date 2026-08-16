namespace SkeletonKey.Analysis.Default;

/// <summary>
/// Defines immutable deterministic options for <see cref="DefaultWorkflowAnalyzer" />.
/// </summary>
/// <remarks>
/// Options never change workflow semantics, do not contain callbacks, and are safe to share across threads.
/// </remarks>
public sealed class WorkflowAnalysisOptions
{
    /// <summary>
    /// Initializes analysis options.
    /// </summary>
    /// <param name="validateParameterSchemas">Whether bounded catalog parameter-contract checks are performed.</param>
    /// <param name="treatDeprecatedNodesAsErrors">Whether deprecated catalog definitions produce errors instead of warnings.</param>
    /// <param name="requireResourceCapabilities">Whether required resource capabilities must be declared by workflow resources.</param>
    /// <param name="maximumIssues">The positive maximum number of diagnostics to retain.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maximumIssues" /> is less than one.</exception>
    public WorkflowAnalysisOptions(
        bool validateParameterSchemas = true,
        bool treatDeprecatedNodesAsErrors = false,
        bool requireResourceCapabilities = true,
        int maximumIssues = 1024)
    {
        if (maximumIssues < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumIssues), "Maximum issue count must be positive.");
        }

        ValidateParameterSchemas = validateParameterSchemas;
        TreatDeprecatedNodesAsErrors = treatDeprecatedNodesAsErrors;
        RequireResourceCapabilities = requireResourceCapabilities;
        MaximumIssues = maximumIssues;
    }

    /// <summary>Gets the deterministic default options.</summary>
    public static WorkflowAnalysisOptions Default { get; } = new();

    /// <summary>Gets whether bounded catalog parameter-contract checks are performed.</summary>
    public bool ValidateParameterSchemas { get; }

    /// <summary>Gets whether deprecated catalog definitions produce errors instead of warnings.</summary>
    public bool TreatDeprecatedNodesAsErrors { get; }

    /// <summary>Gets whether required resource capabilities must be declared by workflow resources.</summary>
    public bool RequireResourceCapabilities { get; }

    /// <summary>Gets the positive maximum number of diagnostics to retain.</summary>
    public int MaximumIssues { get; }
}
