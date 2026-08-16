namespace SkeletonKey.Workflow.Resources;

/// <summary>
/// Identifies a resource declared by the current workflow document without resolving it.
/// </summary>
public sealed class WorkflowResourceReference
{
    /// <summary>
    /// Initializes a workflow resource reference.
    /// </summary>
    /// <param name="name">The declared workflow resource name.</param>
    public WorkflowResourceReference(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Gets the declared workflow resource name.
    /// </summary>
    public string Name { get; }
}
