namespace SkeletonKey.Abstractions.Interaction;

/// <summary>
/// Describes the host-neutral kind of human interaction requested by a workflow.
/// </summary>
public enum WorkflowInteractionKind
{
    /// <summary>Requests a yes or no decision.</summary>
    Confirmation,

    /// <summary>Requests non-secret text input.</summary>
    Text,

    /// <summary>Requests sensitive text input that hosts must treat as secret data.</summary>
    Secret,

    /// <summary>Requests one selected option.</summary>
    Choice,

    /// <summary>Requests zero or more selected options.</summary>
    MultipleChoice,

    /// <summary>Requests that a human perform an external action and acknowledge completion.</summary>
    ManualAction,
}
