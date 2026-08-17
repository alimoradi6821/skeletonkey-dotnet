using System.Collections.ObjectModel;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Execution;

namespace SkeletonKey.Runtime;

/// <summary>Represents a versioned, immutable workflow execution checkpoint.</summary>
public sealed class WorkflowExecutionCheckpoint
{
    /// <summary>The checkpoint contract version implemented by this runtime.</summary>
    public const string CurrentFormatVersion = "0.2";

    /// <summary>The previous checkpoint contract version accepted for safe migration.</summary>
    public const string LegacyFormatVersion = "0.1";

    private readonly IReadOnlyList<WorkflowCheckpointStep> _steps;
    private readonly IReadOnlyDictionary<string, int> _nodeActivationOrdinals;
    private readonly IReadOnlyList<NodeExecutionResult> _nodeResults;
    private readonly IReadOnlyList<NodeExecutionStateSnapshot> _nodeSnapshots;

    /// <summary>Initializes an immutable execution checkpoint.</summary>
    public WorkflowExecutionCheckpoint(
        string formatVersion,
        string executionId,
        string workflowId,
        string workflowSpecVersion,
        string planId,
        string requestFingerprint,
        long revision,
        DateTimeOffset savedAtUtc,
        bool isTerminal,
        IReadOnlyList<WorkflowCheckpointStep>? steps = null,
        IReadOnlyDictionary<string, int>? nodeActivationOrdinals = null,
        int executedAttempts = 0,
        int runtimeActivations = 0,
        int invocations = 1,
        long eventSequence = 0,
        long recordsEmitted = 0,
        long elapsedDurationMilliseconds = 0,
        WorkflowExecutionStatus terminalStatus = WorkflowExecutionStatus.Succeeded,
        WorkflowOutcome? outcome = null,
        WorkflowError? error = null,
        WorkflowExecutionResult? terminalResult = null,
        IReadOnlyList<NodeExecutionResult>? nodeResults = null,
        IReadOnlyList<NodeExecutionStateSnapshot>? nodeSnapshots = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formatVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowSpecVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestFingerprint);
        if (revision < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(revision));
        }

        if (savedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Checkpoint timestamps must use UTC offset zero.", nameof(savedAtUtc));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(executedAttempts);
        ArgumentOutOfRangeException.ThrowIfNegative(runtimeActivations);
        ArgumentOutOfRangeException.ThrowIfLessThan(invocations, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(eventSequence);
        ArgumentOutOfRangeException.ThrowIfNegative(recordsEmitted);
        ArgumentOutOfRangeException.ThrowIfNegative(elapsedDurationMilliseconds);

        if (isTerminal != (terminalResult is not null))
        {
            throw new ArgumentException("A terminal checkpoint must contain exactly one terminal result.", nameof(terminalResult));
        }

        FormatVersion = formatVersion;
        ExecutionId = executionId;
        WorkflowId = workflowId;
        WorkflowSpecVersion = workflowSpecVersion;
        PlanId = planId;
        RequestFingerprint = requestFingerprint;
        Revision = revision;
        SavedAtUtc = savedAtUtc;
        IsTerminal = isTerminal;
        _steps = Array.AsReadOnly([.. (steps ?? Array.AsReadOnly(Array.Empty<WorkflowCheckpointStep>()))]);
        _nodeActivationOrdinals = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(nodeActivationOrdinals ?? new Dictionary<string, int>(), StringComparer.Ordinal));
        ExecutedAttempts = executedAttempts;
        RuntimeActivations = runtimeActivations;
        Invocations = invocations;
        EventSequence = eventSequence;
        RecordsEmitted = recordsEmitted;
        ElapsedDurationMilliseconds = elapsedDurationMilliseconds;
        TerminalStatus = terminalStatus;
        Outcome = outcome;
        Error = error;
        TerminalResult = terminalResult;
        _nodeResults = Array.AsReadOnly([.. (nodeResults ?? Array.AsReadOnly(Array.Empty<NodeExecutionResult>()))]);
        _nodeSnapshots = Array.AsReadOnly([.. (nodeSnapshots ?? Array.AsReadOnly(Array.Empty<NodeExecutionStateSnapshot>()))]);
    }

    /// <summary>Gets the checkpoint format version.</summary>
    public string FormatVersion { get; }

    /// <summary>Gets the root execution identifier.</summary>
    public string ExecutionId { get; }

    /// <summary>Gets the workflow identifier.</summary>
    public string WorkflowId { get; }

    /// <summary>Gets the workflow language version.</summary>
    public string WorkflowSpecVersion { get; }

    /// <summary>Gets the caller-supplied execution plan identifier.</summary>
    public string PlanId { get; }

    /// <summary>Gets the SHA-256 fingerprint of execution inputs and variable overrides.</summary>
    public string RequestFingerprint { get; }

    /// <summary>Gets the monotonically increasing persisted revision.</summary>
    public long Revision { get; }

    /// <summary>Gets the host clock timestamp at which the checkpoint was captured.</summary>
    public DateTimeOffset SavedAtUtc { get; }

    /// <summary>Gets whether this checkpoint contains a final execution result.</summary>
    public bool IsTerminal { get; }

    /// <summary>Gets scheduler step states in plan order.</summary>
    public IReadOnlyList<WorkflowCheckpointStep> Steps => new ReadOnlyCollection<WorkflowCheckpointStep>([.. _steps]);

    /// <summary>Gets the last activation ordinal keyed by node ID.</summary>
    public IReadOnlyDictionary<string, int> NodeActivationOrdinals => new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(_nodeActivationOrdinals, StringComparer.Ordinal));

    /// <summary>Gets the number of attempted node executions.</summary>
    public int ExecutedAttempts { get; }

    /// <summary>Gets the number of runtime activations.</summary>
    public int RuntimeActivations { get; }

    /// <summary>Gets the invocation count.</summary>
    public int Invocations { get; }

    /// <summary>Gets the last emitted runtime event sequence.</summary>
    public long EventSequence { get; }

    /// <summary>Gets the count of streamed records emitted before the checkpoint.</summary>
    public long RecordsEmitted { get; }

    /// <summary>Gets accumulated execution duration before the checkpoint.</summary>
    public long ElapsedDurationMilliseconds { get; }

    /// <summary>Gets the current terminal status accumulator.</summary>
    public WorkflowExecutionStatus TerminalStatus { get; }

    /// <summary>Gets the current business outcome accumulator.</summary>
    public WorkflowOutcome? Outcome { get; }

    /// <summary>Gets the current technical error accumulator.</summary>
    public WorkflowError? Error { get; }

    /// <summary>Gets the final execution result for a terminal checkpoint.</summary>
    public WorkflowExecutionResult? TerminalResult { get; }

    /// <summary>Gets terminal node results in deterministic execution order.</summary>
    public IReadOnlyList<NodeExecutionResult> NodeResults => new ReadOnlyCollection<NodeExecutionResult>([.. _nodeResults]);

    /// <summary>Gets terminal node lifecycle snapshots in deterministic execution order.</summary>
    public IReadOnlyList<NodeExecutionStateSnapshot> NodeSnapshots => new ReadOnlyCollection<NodeExecutionStateSnapshot>([.. _nodeSnapshots]);

    /// <summary>Returns whether the runtime can load and migrate the supplied checkpoint format.</summary>
    public static bool IsSupportedFormatVersion(string formatVersion)
    {
        return string.Equals(formatVersion, CurrentFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, LegacyFormatVersion, StringComparison.Ordinal);
    }
}
