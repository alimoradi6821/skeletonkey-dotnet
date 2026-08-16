using SkeletonKey.Runtime.Invocation;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.References;

namespace SkeletonKey.Runtime.Invocation.Tests;

/// <summary>
/// Covers host-neutral workflow repository lookup contracts.
/// </summary>
public sealed class WorkflowRepositoryTests
{
    /// <summary>
    /// Verifies immutable repositories resolve exact workflow identifiers.
    /// </summary>
    [Fact]
    public async Task ResolvesWorkflowById()
    {
        WorkflowDocument workflow = new(id: "child", name: "Child");
        var repository = ImmutableWorkflowRepository.FromDocuments(workflow);

        WorkflowRepositoryLookupResult result = await repository.LookupAsync(new WorkflowReference("child"));

        Assert.True(result.Found);
        Assert.Same(workflow, result.Workflow);
    }

    /// <summary>
    /// Verifies missing references are represented as data, not exceptions.
    /// </summary>
    [Fact]
    public async Task ReturnsNotFoundForMissingWorkflow()
    {
        ImmutableWorkflowRepository repository = new(new Dictionary<string, WorkflowDocument>(StringComparer.Ordinal));

        WorkflowRepositoryLookupResult result = await repository.LookupAsync(new WorkflowReference("missing"));

        Assert.False(result.Found);
        Assert.Null(result.Workflow);
    }
}
