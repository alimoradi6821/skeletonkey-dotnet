using SkeletonKey.Workflow.References;

namespace SkeletonKey.Runtime.Invocation;

/// <summary>
/// Resolves child workflow references for runtime invocation.
/// </summary>
/// <remarks>
/// Implementations are supplied explicitly by the host. The contract does not define filesystem lookup, network access, package registries,
/// mutable plugin discovery, assembly scanning, or dependency injection.
/// </remarks>
public interface IWorkflowRepository
{
    /// <summary>Resolves an exact workflow reference.</summary>
    public ValueTask<WorkflowRepositoryLookupResult> LookupAsync(
        WorkflowReference reference,
        CancellationToken cancellationToken = default);
}
