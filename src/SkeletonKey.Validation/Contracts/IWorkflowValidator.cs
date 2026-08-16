using SkeletonKey.Workflow.Documents;

namespace SkeletonKey.Validation;

/// <summary>
/// Validates workflow documents after parsing or programmatic construction.
/// </summary>
/// <remarks>
/// Implementations distinguish semantic validation from JSON parsing, must not mutate the supplied workflow,
/// and are expected to be safe for concurrent use.
/// </remarks>
public interface IWorkflowValidator
{
    /// <summary>
    /// Validates a workflow document and returns deterministic issues for invalid content.
    /// </summary>
    /// <param name="workflow">The workflow document to validate.</param>
    /// <returns>A semantic validation result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="workflow" /> is <see langword="null" />.</exception>
    public WorkflowValidationResult Validate(WorkflowDocument workflow);
}
