using System.Collections.ObjectModel;
using SkeletonKey.Abstractions.Execution;

namespace SkeletonKey.Runtime;

/// <summary>Represents one persisted deterministic scheduler step.</summary>
public sealed class WorkflowCheckpointStep
{
    private readonly IReadOnlyList<string> _activatedControlInputs;
    private readonly IReadOnlyList<WorkflowCheckpointPortValue> _outputs;

    /// <summary>Initializes a checkpoint step.</summary>
    public WorkflowCheckpointStep(
        string stepId,
        string nodeId,
        string nodeType,
        WorkflowStepRuntimeStatus status,
        bool entryActivated,
        IReadOnlyList<string>? activatedControlInputs = null,
        IReadOnlyList<WorkflowCheckpointPortValue>? outputs = null,
        int attempt = 0,
        NodeExecutionStatus? resultStatus = null,
        WorkflowError? error = null,
        int retryAttempt = 0,
        DateTimeOffset? retryNotBeforeUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeType);
        if (attempt < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attempt));
        }

        if (retryAttempt < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retryAttempt));
        }

        if (retryNotBeforeUtc is not null && retryAttempt < 1)
        {
            throw new ArgumentException("A retry not-before timestamp requires at least one completed attempt.", nameof(retryNotBeforeUtc));
        }

        if (retryNotBeforeUtc is not null && retryNotBeforeUtc.Value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Retry timestamps must use UTC offset zero.", nameof(retryNotBeforeUtc));
        }

        StepId = stepId;
        NodeId = nodeId;
        NodeType = nodeType;
        Status = status;
        EntryActivated = entryActivated;
        _activatedControlInputs = Array.AsReadOnly([.. (activatedControlInputs ?? Array.AsReadOnly(Array.Empty<string>()))]);
        _outputs = Array.AsReadOnly([.. (outputs ?? Array.AsReadOnly(Array.Empty<WorkflowCheckpointPortValue>()))]);
        Attempt = attempt;
        ResultStatus = resultStatus;
        Error = error;
        RetryAttempt = retryAttempt;
        RetryNotBeforeUtc = retryNotBeforeUtc;
    }

    /// <summary>Gets the planned step identifier.</summary>
    public string StepId { get; }

    /// <summary>Gets the workflow node identifier.</summary>
    public string NodeId { get; }

    /// <summary>Gets the workflow node type.</summary>
    public string NodeType { get; }

    /// <summary>Gets the scheduler status captured at the checkpoint boundary.</summary>
    public WorkflowStepRuntimeStatus Status { get; }

    /// <summary>Gets whether the entry activation reached this step.</summary>
    public bool EntryActivated { get; }

    /// <summary>Gets activated control input ports in ordinal order.</summary>
    public IReadOnlyList<string> ActivatedControlInputs => new ReadOnlyCollection<string>([.. _activatedControlInputs]);

    /// <summary>Gets persisted output port values.</summary>
    public IReadOnlyList<WorkflowCheckpointPortValue> Outputs => new ReadOnlyCollection<WorkflowCheckpointPortValue>([.. _outputs]);

    /// <summary>Gets the most recent node activation ordinal, or zero when never activated.</summary>
    public int Attempt { get; }

    /// <summary>Gets the terminal node result status when available.</summary>
    public NodeExecutionStatus? ResultStatus { get; }

    /// <summary>Gets the optional terminal node error.</summary>
    public WorkflowError? Error { get; }

    /// <summary>Gets the number of policy-controlled handler attempts already started for the current scheduler activation.</summary>
    public int RetryAttempt { get; }

    /// <summary>Gets the earliest UTC time at which the next policy-controlled attempt may start.</summary>
    public DateTimeOffset? RetryNotBeforeUtc { get; }
}
