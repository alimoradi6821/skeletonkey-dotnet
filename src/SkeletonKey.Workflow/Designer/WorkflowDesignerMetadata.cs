using System.Collections.ObjectModel;

namespace SkeletonKey.Workflow.Designer;

/// <summary>
/// Contains optional visual metadata for workflow authoring surfaces.
/// </summary>
public sealed class WorkflowDesignerMetadata
{
    private static readonly IReadOnlyDictionary<string, WorkflowNodePosition> _emptyPositions = new ReadOnlyDictionary<string, WorkflowNodePosition>(new Dictionary<string, WorkflowNodePosition>());
    private static readonly IReadOnlyDictionary<string, WorkflowNodeSize> _emptySizes = new ReadOnlyDictionary<string, WorkflowNodeSize>(new Dictionary<string, WorkflowNodeSize>());

    /// <summary>
    /// Initializes a new designer metadata declaration.
    /// </summary>
    /// <param name="positions">Optional node positions keyed by node identifier.</param>
    /// <param name="sizes">Optional node sizes keyed by node identifier.</param>
    public WorkflowDesignerMetadata(
        IReadOnlyDictionary<string, WorkflowNodePosition>? positions = null,
        IReadOnlyDictionary<string, WorkflowNodeSize>? sizes = null)
    {
        Positions = positions is null
            ? _emptyPositions
            : new ReadOnlyDictionary<string, WorkflowNodePosition>(new Dictionary<string, WorkflowNodePosition>(positions));

        Sizes = sizes is null
            ? _emptySizes
            : new ReadOnlyDictionary<string, WorkflowNodeSize>(new Dictionary<string, WorkflowNodeSize>(sizes));
    }

    /// <summary>
    /// Gets visual node positions keyed by node identifier.
    /// </summary>
    public IReadOnlyDictionary<string, WorkflowNodePosition> Positions { get; }

    /// <summary>
    /// Gets visual node sizes keyed by node identifier.
    /// </summary>
    public IReadOnlyDictionary<string, WorkflowNodeSize> Sizes { get; }
}

