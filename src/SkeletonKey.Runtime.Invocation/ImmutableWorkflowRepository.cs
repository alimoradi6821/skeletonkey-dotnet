using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.References;

namespace SkeletonKey.Runtime.Invocation;

/// <summary>
/// Provides an immutable in-memory workflow repository keyed by workflow identifier and optional exact version.
/// </summary>
public sealed class ImmutableWorkflowRepository : IWorkflowRepository
{
    private readonly IReadOnlyDictionary<string, WorkflowDocument> _workflows;

    /// <summary>
    /// Initializes an immutable workflow repository.
    /// </summary>
    public ImmutableWorkflowRepository(IReadOnlyDictionary<string, WorkflowDocument> workflows)
    {
        ArgumentNullException.ThrowIfNull(workflows);
        _workflows = workflows.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
    }

    /// <summary>
    /// Creates a repository keyed by each workflow document identifier.
    /// </summary>
    public static ImmutableWorkflowRepository FromDocuments(params WorkflowDocument[] workflows)
    {
        return new ImmutableWorkflowRepository(workflows.ToDictionary(static workflow => workflow.Id, StringComparer.Ordinal));
    }

    /// <inheritdoc />
    public ValueTask<WorkflowRepositoryLookupResult> LookupAsync(WorkflowReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        cancellationToken.ThrowIfCancellationRequested();
        string key = reference.Version is null ? reference.Id : reference.Id + "@" + reference.Version;
        if (_workflows.TryGetValue(key, out WorkflowDocument? versioned))
        {
            return ValueTask.FromResult(WorkflowRepositoryLookupResult.Success(versioned));
        }

        return ValueTask.FromResult(WorkflowRepositoryLookupResult.NotFound("Workflow reference was not found."));
    }
}
