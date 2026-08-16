using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Events;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Abstractions.Interaction;
using SkeletonKey.Analysis;
using SkeletonKey.Analysis.Default;
using SkeletonKey.BuiltIns;
using SkeletonKey.BuiltIns.Runtime;
using SkeletonKey.Catalog;
using SkeletonKey.Evaluation;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;
using SkeletonKey.Locators;
using SkeletonKey.Locators.Runtime;
using SkeletonKey.Materialization;
using SkeletonKey.Planning;
using SkeletonKey.Planning.Default;
using SkeletonKey.Resources;
using SkeletonKey.Runtime;
using SkeletonKey.Runtime.Interactions;
using SkeletonKey.Runtime.Invocation;
using SkeletonKey.Runtime.Resources;
using SkeletonKey.Validation;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Invocation;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Outputs;
using SkeletonKey.Workflow.References;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Runtime.Default;

/// <summary>
/// Executes validated, analyzed, and planned workflows with deterministic sequential dependency scheduling.
/// </summary>
/// <remarks>
/// The runtime consumes an execution plan instead of reinterpreting raw graph semantics. It owns state transitions, event sequencing, exact handler
/// resolution, materialized parameter preparation, control and data propagation, cancellation normalization, and final result aggregation. It does not
/// implement persistence, retry execution, loops, subworkflow invocation, resource resolution, locator resolution, browser automation, dependency injection,
/// plugin discovery, or assembly scanning.
/// </remarks>
public sealed class DefaultWorkflowRuntime : IWorkflowRuntime
{
    private static readonly IReadOnlyDictionary<string, WorkflowNode> _emptyNodeMap = new ReadOnlyDictionary<string, WorkflowNode>(new Dictionary<string, WorkflowNode>(StringComparer.Ordinal));

    private readonly IWorkflowValidator _validator;
    private readonly IWorkflowAnalyzer _analyzer;
    private readonly IWorkflowExecutionPlanner _planner;
    private readonly IWorkflowNodeDefinitionCatalog _catalog;
    private readonly INodeHandlerResolver _handlerResolver;
    private readonly NodeParameterMaterializer _parameterMaterializer;
    private readonly IWorkflowClock _clock;
    private readonly WorkflowRuntimeOptions _options;
    private readonly IWorkflowRepository? _workflowRepository;
    private readonly IReadOnlyDictionary<string, IWorkflowRuntimeResourceProvider> _resourceProviders;
    private readonly ILocatorPlanResolver? _locatorResolver;

    /// <summary>
    /// Initializes a default workflow runtime with built-in validation, analysis, planning, materialization, clock, and essential handlers.
    /// </summary>
    public DefaultWorkflowRuntime()
        : this(
            new WorkflowSemanticValidator(),
            new DefaultWorkflowAnalyzer(),
            new DefaultWorkflowExecutionPlanner(),
            BuiltInWorkflowNodeCatalog.Catalog,
            BuiltInRuntimeHandlers.CreateResolver(),
            new NodeParameterMaterializer(),
            new SystemWorkflowClock(),
            new WorkflowRuntimeOptions())
    {
    }

    /// <summary>
    /// Initializes a directly constructible default workflow runtime.
    /// </summary>
    /// <param name="validator">Semantic validator used before analysis.</param>
    /// <param name="analyzer">Catalog-aware analyzer used before planning.</param>
    /// <param name="planner">Execution planner used before runtime state initialization.</param>
    /// <param name="catalog">Exact node definition catalog used for analysis and output validation.</param>
    /// <param name="handlerResolver">Immutable exact node handler resolver.</param>
    /// <param name="parameterMaterializer">Node parameter materializer used before handler execution.</param>
    /// <param name="clock">Runtime clock for timestamps and metrics.</param>
    /// <param name="options">Runtime limits and behavior flags.</param>
    /// <param name="workflowRepository">Optional host-supplied immutable child workflow repository.</param>
    /// <param name="resourceProviders">Optional explicit runtime resource providers keyed by kind.</param>
    /// <param name="locatorResolver">Optional explicit locator plan resolver used for `$locator` wrapper preparation.</param>
    public DefaultWorkflowRuntime(
        IWorkflowValidator validator,
        IWorkflowAnalyzer analyzer,
        IWorkflowExecutionPlanner planner,
        IWorkflowNodeDefinitionCatalog catalog,
        INodeHandlerResolver handlerResolver,
        NodeParameterMaterializer? parameterMaterializer = null,
        IWorkflowClock? clock = null,
        WorkflowRuntimeOptions? options = null,
        IWorkflowRepository? workflowRepository = null,
        IReadOnlyList<IWorkflowRuntimeResourceProvider>? resourceProviders = null,
        ILocatorPlanResolver? locatorResolver = null)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _handlerResolver = handlerResolver ?? throw new ArgumentNullException(nameof(handlerResolver));
        _parameterMaterializer = parameterMaterializer ?? new NodeParameterMaterializer();
        _clock = clock ?? new SystemWorkflowClock();
        _options = options ?? new WorkflowRuntimeOptions();
        _workflowRepository = workflowRepository;
        _resourceProviders = (resourceProviders ?? Array.AsReadOnly(Array.Empty<IWorkflowRuntimeResourceProvider>()))
            .ToDictionary(static provider => provider.Kind, StringComparer.Ordinal);
        _locatorResolver = locatorResolver;
    }

    /// <inheritdoc />
    public ValueTask<IWorkflowExecutionSession> StartAsync(WorkflowExecutionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string invocationId = $"invocation:{request.ExecutionId}:root";
        if (cancellationToken.IsCancellationRequested)
        {
            WorkflowExecutionResult cancelled = new(
                request.ExecutionId,
                request.Workflow.Id,
                invocationId,
                null,
                WorkflowExecutionStatus.Cancelled,
                error: new WorkflowError(WorkflowRuntimeErrorCodes.ExecutionCancelled, "Execution was cancelled."));
            return ValueTask.FromResult<IWorkflowExecutionSession>(new CompletedWorkflowExecutionSession(request.ExecutionId, new WorkflowRuntimeResult(cancelled)));
        }

        WorkflowValidationResult validation = _validator.Validate(request.Workflow);
        if (!validation.IsValid)
        {
            return ValueTask.FromResult<IWorkflowExecutionSession>(new CompletedWorkflowExecutionSession(request.ExecutionId, FailureBeforeState(request, invocationId, WorkflowRuntimeErrorCodes.SemanticValidationFailed, "Semantic validation failed.")));
        }

        WorkflowAnalysisResult analysis = _analyzer.Analyze(request.Workflow, _catalog);
        if (!analysis.CanPlanExecution)
        {
            return ValueTask.FromResult<IWorkflowExecutionSession>(new CompletedWorkflowExecutionSession(request.ExecutionId, FailureBeforeState(request, invocationId, WorkflowRuntimeErrorCodes.CatalogAnalysisFailed, "Catalog-aware analysis failed.")));
        }

        WorkflowExecutionPlanResult planning = _planner.Plan(request.Workflow, analysis);
        if (!planning.IsReady || planning.Plan is null)
        {
            return ValueTask.FromResult<IWorkflowExecutionSession>(new CompletedWorkflowExecutionSession(request.ExecutionId, FailureBeforeState(request, invocationId, WorkflowRuntimeErrorCodes.PlanningFailed, "Execution planning failed.")));
        }

        DefaultWorkflowExecutionSession session = new(request.ExecutionId, _clock);
        ExecutionSession execution = new(request, invocationId, planning.Plan, analysis, _validator, _analyzer, _planner, _catalog, _clock, _options, _workflowRepository, _resourceProviders, _locatorResolver, session);
        session.Start(execution.ExecuteAsync(_handlerResolver, _parameterMaterializer, cancellationToken));
        return ValueTask.FromResult<IWorkflowExecutionSession>(session);
    }

    /// <inheritdoc />
    public async ValueTask<WorkflowRuntimeResult> ExecuteAsync(WorkflowExecutionRequest request, CancellationToken cancellationToken = default)
    {
        await using IWorkflowExecutionSession session = await StartAsync(request, cancellationToken).ConfigureAwait(false);
        return await session.WaitForCompletionAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private static WorkflowRuntimeResult FailureBeforeState(WorkflowExecutionRequest request, string invocationId, string code, string message)
    {
        WorkflowExecutionResult result = new(
            request.ExecutionId,
            request.Workflow.Id,
            invocationId,
            null,
            WorkflowExecutionStatus.Failed,
            error: new WorkflowError(code, message));
        return new WorkflowRuntimeResult(result);
    }

    private sealed class CompletedWorkflowExecutionSession : IWorkflowExecutionSession
    {
        private readonly WorkflowRuntimeResult _result;

        public CompletedWorkflowExecutionSession(string executionId, WorkflowRuntimeResult result)
        {
            ExecutionId = executionId;
            _result = result;
        }

        public string ExecutionId { get; }

        public ValueTask<IReadOnlyList<PendingWorkflowInteraction>> GetPendingInteractionsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IReadOnlyList<PendingWorkflowInteraction>>(Array.AsReadOnly(Array.Empty<PendingWorkflowInteraction>()));
        }

        public ValueTask<WorkflowInteractionContinuationResult> ContinueAsync(WorkflowInteractionContinuation continuation, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(continuation);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(WorkflowInteractionContinuationResult.Reject(WorkflowInteractionContinuationErrorCodes.UnknownContinuation, "The execution session has no pending continuations."));
        }

        public ValueTask CancelAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }

        public ValueTask<WorkflowRuntimeResult> WaitForCompletionAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(_result);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class DefaultWorkflowExecutionSession : IWorkflowExecutionSession
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, PendingInteractionWaiter> _pending = new(StringComparer.Ordinal);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly IWorkflowClock _clock;
        private Task<WorkflowRuntimeResult>? _execution;

        public DefaultWorkflowExecutionSession(string executionId, IWorkflowClock clock)
        {
            ExecutionId = executionId;
            _clock = clock;
        }

        public string ExecutionId { get; }

        public void Start(ValueTask<WorkflowRuntimeResult> execution)
        {
            _execution = execution.AsTask();
        }

        public ValueTask<WorkflowInteractionResponse> SuspendAsync(WorkflowInteractionRequest request, DateTimeOffset? expiresAt, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string continuationId = request.RequestId;
            TaskCompletionSource<WorkflowInteractionResponse> source = new(TaskCreationOptions.RunContinuationsAsynchronously);
            PendingInteractionWaiter pending = new(new PendingWorkflowInteraction(continuationId, request, _clock.UtcNow, expiresAt), source, () => _clock.UtcNow);
            lock (_gate)
            {
                _pending.Add(continuationId, pending);
            }

            return new ValueTask<WorkflowInteractionResponse>(source.Task);
        }

        public ValueTask<IReadOnlyList<PendingWorkflowInteraction>> GetPendingInteractionsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                return ValueTask.FromResult<IReadOnlyList<PendingWorkflowInteraction>>(Array.AsReadOnly([.. _pending.Values.Select(static item => item.Pending)]));
            }
        }

        public ValueTask<WorkflowInteractionContinuationResult> ContinueAsync(WorkflowInteractionContinuation continuation, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(continuation);
            cancellationToken.ThrowIfCancellationRequested();
            PendingInteractionWaiter waiter;
            lock (_gate)
            {
                if (!_pending.Remove(continuation.ContinuationId, out waiter!))
                {
                    return ValueTask.FromResult(WorkflowInteractionContinuationResult.Reject(WorkflowInteractionContinuationErrorCodes.UnknownContinuation, "The continuation identifier is not pending."));
                }
            }

            DateTimeOffset now = _clock.UtcNow;
            if (waiter.Pending.ExpiresAt is not null && now > waiter.Pending.ExpiresAt.Value)
            {
                waiter.Source.TrySetResult(new WorkflowInteractionResponse(waiter.Pending.Request.RequestId, WorkflowInteractionResponseStatus.TimedOut, hasValue: false, null, now));
                return ValueTask.FromResult(WorkflowInteractionContinuationResult.Reject(WorkflowInteractionContinuationErrorCodes.ContinuationTimedOut, "The interaction continuation timed out."));
            }

            waiter.Source.TrySetResult(new WorkflowInteractionResponse(waiter.Pending.Request.RequestId, continuation.Status, continuation.HasValue, continuation.Value, now));
            return ValueTask.FromResult(WorkflowInteractionContinuationResult.Accept());
        }

        public ValueTask CancelAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _cancellation.Cancel();
            PendingInteractionWaiter[] pending;
            lock (_gate)
            {
                pending = [.. _pending.Values];
                _pending.Clear();
            }

            DateTimeOffset now = _clock.UtcNow;
            foreach (PendingInteractionWaiter waiter in pending)
            {
                waiter.Source.TrySetResult(new WorkflowInteractionResponse(waiter.Pending.Request.RequestId, WorkflowInteractionResponseStatus.Cancelled, hasValue: false, null, now));
            }

            return ValueTask.CompletedTask;
        }

        public async ValueTask<WorkflowRuntimeResult> WaitForCompletionAsync(CancellationToken cancellationToken = default)
        {
            if (_execution is null)
            {
                throw new InvalidOperationException("The execution session has not been started.");
            }

            return await _execution.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        public ValueTask DisposeAsync()
        {
            _cancellation.Dispose();
            return ValueTask.CompletedTask;
        }

        private sealed class PendingInteractionWaiter(
            PendingWorkflowInteraction pending,
            TaskCompletionSource<WorkflowInteractionResponse> source,
            Func<DateTimeOffset> cancelledAt)
        {
            public PendingWorkflowInteraction Pending { get; } = pending;

            public TaskCompletionSource<WorkflowInteractionResponse> Source { get; } = source;

            public Func<DateTimeOffset> CancelledAt { get; } = cancelledAt;
        }
    }

    private sealed class ExecutionSession
    {
        private readonly WorkflowExecutionRequest _request;
        private readonly string _invocationId;
        private readonly WorkflowExecutionPlan _plan;
        private readonly WorkflowAnalysisResult _analysis;
        private readonly IWorkflowValidator _validator;
        private readonly IWorkflowAnalyzer _analyzer;
        private readonly IWorkflowExecutionPlanner _planner;
        private readonly IWorkflowNodeDefinitionCatalog _catalog;
        private readonly IWorkflowClock _clock;
        private readonly WorkflowRuntimeOptions _options;
        private readonly IWorkflowRepository? _workflowRepository;
        private readonly IReadOnlyDictionary<string, IWorkflowRuntimeResourceProvider> _resourceProviders;
        private readonly ILocatorPlanResolver? _locatorResolver;
        private readonly DefaultWorkflowExecutionSession? _ownerSession;
        private readonly Dictionary<string, IWorkflowRuntimeResourceInstance> _resourcesByName = new(StringComparer.Ordinal);
        private readonly InMemoryExecutionStateStore _stateStore = new();
        private readonly Dictionary<string, StepState> _steps;
        private readonly Dictionary<string, WorkflowExecutionPlanStep> _stepsById;
        private readonly Dictionary<string, int> _stepOrder;
        private readonly IReadOnlyDictionary<string, WorkflowNode> _nodesById;
        private readonly Dictionary<string, WorkflowNodeAnalysis> _analysisByNode;
        private readonly Dictionary<string, NodePortValueMap> _completedNodeOutputs = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _nodeActivationOrdinals = new(StringComparer.Ordinal);
        private readonly List<NodeExecutionResult> _nodeResults = [];
        private readonly List<NodeExecutionStateSnapshot> _nodeSnapshots = [];
        private readonly EventCoordinator _events;
        private readonly Stopwatch _stopwatch = new();
        private WorkflowOutcome? _terminalOutcome;
        private WorkflowError? _terminalError;
        private WorkflowExecutionStatus _terminalStatus = WorkflowExecutionStatus.Succeeded;
        private int _executedAttempts;
        private int _activations;
        private int _invocations = 1;

        public ExecutionSession(
            WorkflowExecutionRequest request,
            string invocationId,
            WorkflowExecutionPlan plan,
            WorkflowAnalysisResult analysis,
            IWorkflowValidator validator,
            IWorkflowAnalyzer analyzer,
            IWorkflowExecutionPlanner planner,
            IWorkflowNodeDefinitionCatalog catalog,
            IWorkflowClock clock,
            WorkflowRuntimeOptions options,
            IWorkflowRepository? workflowRepository,
            IReadOnlyDictionary<string, IWorkflowRuntimeResourceProvider> resourceProviders,
            ILocatorPlanResolver? locatorResolver,
            DefaultWorkflowExecutionSession? ownerSession)
        {
            _request = request;
            _invocationId = invocationId;
            _plan = plan;
            _analysis = analysis;
            _validator = validator;
            _analyzer = analyzer;
            _planner = planner;
            _catalog = catalog;
            _clock = clock;
            _options = options;
            _workflowRepository = workflowRepository;
            _resourceProviders = resourceProviders;
            _locatorResolver = locatorResolver;
            _ownerSession = ownerSession;
            _events = new EventCoordinator(request, invocationId, clock);
            _stepsById = plan.Steps.ToDictionary(static step => step.StepId, StringComparer.Ordinal);
            _stepOrder = plan.Steps.Select(static (step, index) => new { step.StepId, index }).ToDictionary(static item => item.StepId, static item => item.index, StringComparer.Ordinal);
            _steps = plan.Steps.ToDictionary(static step => step.StepId, static step => new StepState(step), StringComparer.Ordinal);
            _nodesById = request.Workflow.Nodes.Count == 0
                ? _emptyNodeMap
                : new ReadOnlyDictionary<string, WorkflowNode>(request.Workflow.Nodes.ToDictionary(static node => node.Id, StringComparer.Ordinal));
            _analysisByNode = analysis.Nodes.ToDictionary(static node => node.NodeId, StringComparer.Ordinal);
        }

        public async ValueTask<WorkflowRuntimeResult> ExecuteAsync(INodeHandlerResolver handlerResolver, NodeParameterMaterializer parameterMaterializer, CancellationToken cancellationToken)
        {
            WorkflowExecutionStateSnapshot execution = _stateStore.CreateExecution(_request.ExecutionId, _request.Workflow.Id, _request.PlanId, _clock.UtcNow);
            WorkflowInvocationStateSnapshot invocation = _stateStore.CreateInvocation(_request.ExecutionId, _invocationId, null, _request.Workflow.Id, _clock.UtcNow);

            await EmitAsync(RuntimeWorkflowEventKind.ExecutionCreated, "Execution created.", cancellationToken: cancellationToken).ConfigureAwait(false);
            _stateStore.TransitionExecution(_request.ExecutionId, ExecutionLifecycleState.Ready, _clock.UtcNow);
            _stateStore.TransitionInvocation(_invocationId, ExecutionLifecycleState.Ready, _clock.UtcNow);
            await EmitAsync(RuntimeWorkflowEventKind.ExecutionReady, "Execution ready.", cancellationToken: cancellationToken).ConfigureAwait(false);
            _stateStore.TransitionExecution(_request.ExecutionId, ExecutionLifecycleState.Running, _clock.UtcNow);
            _stateStore.TransitionInvocation(_invocationId, ExecutionLifecycleState.Running, _clock.UtcNow);
            _stopwatch.Start();
            await EmitAsync(RuntimeWorkflowEventKind.ExecutionStarted, "Execution started.", cancellationToken: cancellationToken).ConfigureAwait(false);

            try
            {
                ActivateEntry();

                while (_terminalError is null && !IsTerminalReturnCompleted())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    UpdateReadySteps();

                    StepState? ready = _steps.Values
                        .Where(static step => step.Status == WorkflowStepRuntimeStatus.Ready)
                        .OrderBy(step => _stepOrder[step.Step.StepId])
                        .FirstOrDefault();

                    if (ready is null)
                    {
                        if (_steps.Values.Any(static step => step.Status == WorkflowStepRuntimeStatus.Pending))
                        {
                            Fail(WorkflowRuntimeErrorCodes.ExecutionNoProgress, "Execution reached a no-progress state with pending steps.");
                        }

                        break;
                    }

                    await ExecuteStepAsync(ready, handlerResolver, parameterMaterializer, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _terminalStatus = WorkflowExecutionStatus.Cancelled;
                _terminalError = new WorkflowError(WorkflowRuntimeErrorCodes.ExecutionCancelled, "Execution was cancelled.");
                await MarkCancellationAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await DisposeRuntimeResourcesAsync().ConfigureAwait(false);
            }

            SkipUnreachedSteps();
            WorkflowExecutionResult result = CreateWorkflowResult();
            execution = _stateStore.TransitionExecution(_request.ExecutionId, ExecutionLifecycleState.Completed, _clock.UtcNow, result);
            invocation = _stateStore.TransitionInvocation(_invocationId, ExecutionLifecycleState.Completed, _clock.UtcNow, result);
            RuntimeWorkflowEventKind finalKind = result.Status switch
            {
                WorkflowExecutionStatus.Cancelled => RuntimeWorkflowEventKind.ExecutionCancelled,
                WorkflowExecutionStatus.Failed => RuntimeWorkflowEventKind.ExecutionFailed,
                _ => RuntimeWorkflowEventKind.ExecutionCompleted,
            };
            await EmitAsync(finalKind, $"Execution {result.Status}.", cancellationToken: CancellationToken.None).ConfigureAwait(false);

            return new WorkflowRuntimeResult(result, execution, invocation, _nodeResults, _nodeSnapshots);
        }

        private async ValueTask DisposeRuntimeResourcesAsync()
        {
            List<Exception> errors = [];
            foreach (IWorkflowRuntimeResourceInstance resource in _resourcesByName.Values.Reverse())
            {
                try
                {
                    await resource.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            _resourcesByName.Clear();
            if (errors.Count > 0 && _terminalError is null)
            {
                _terminalStatus = WorkflowExecutionStatus.Failed;
                _terminalError = new WorkflowError(WorkflowRuntimeErrorCodes.RuntimeResourceProviderInvalid, "Runtime resource cleanup failed.");
            }
        }

        private void ActivateEntry()
        {
            if (_plan.EntryStepIds.Count != 1 || !_steps.TryGetValue(_plan.EntryStepIds[0], out StepState? entry))
            {
                Fail(WorkflowRuntimeErrorCodes.PlanningFailed, "Execution plan must contain exactly one entry step.");
                return;
            }

            entry.ActivatedControlInputs.Add("entry");
            entry.EntryActivated = true;
        }

        private void UpdateReadySteps()
        {
            foreach (StepState step in _steps.Values.Where(static step => step.Status == WorkflowStepRuntimeStatus.Pending))
            {
                if (IsReady(step))
                {
                    if (_steps.Values.Count(static item => item.Status == WorkflowStepRuntimeStatus.Ready) >= _options.MaximumReadySteps)
                    {
                        Fail(WorkflowRuntimeErrorCodes.ExecutionLimitExceeded, "Ready-step limit was exceeded.", step.Step.NodeId);
                        return;
                    }

                    step.Status = WorkflowStepRuntimeStatus.Ready;
                }
            }
        }

        private bool IsReady(StepState step)
        {
            IReadOnlyList<WorkflowExecutionPlanDependency> dependencies = step.Step.DependsOn;
            IReadOnlyList<WorkflowExecutionPlanDependency> controlDependencies = dependencies.Where(static dependency => dependency.Kind == WorkflowExecutionPlanDependencyKind.Control).ToArray();
            if (step.EntryActivated)
            {
                return true;
            }

            if (controlDependencies.Count == 0 && step.Step.Kind is WorkflowExecutionPlanStepKind.Control or WorkflowExecutionPlanStepKind.Action or WorkflowExecutionPlanStepKind.Interaction or WorkflowExecutionPlanStepKind.Terminal)
            {
                return false;
            }

            if (controlDependencies.Count > 0 && step.ActivatedControlInputs.Count == 0)
            {
                return false;
            }

            foreach (WorkflowExecutionPlanDependency dependency in dependencies.Where(static dependency => dependency.Kind == WorkflowExecutionPlanDependencyKind.Data))
            {
                if (!_stepsById.ContainsKey(dependency.StepId) || !_steps[dependency.StepId].IsTerminalSuccess)
                {
                    return false;
                }

                if (dependency.SourcePort is not null)
                {
                    if (!_completedNodeOutputs.TryGetValue(_stepsById[dependency.StepId].NodeId, out NodePortValueMap? dependencyOutputs))
                    {
                        return false;
                    }

                    if (!dependencyOutputs.Values.ContainsKey(dependency.SourcePort))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private async ValueTask ExecuteStepAsync(StepState step, INodeHandlerResolver handlerResolver, NodeParameterMaterializer parameterMaterializer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++_executedAttempts > _options.MaximumExecutedNodeAttempts)
            {
                Fail(WorkflowRuntimeErrorCodes.ExecutionLimitExceeded, "Executed node attempt limit was exceeded.", step.Step.NodeId);
                return;
            }

            if (++_activations > _options.MaximumRuntimeActivations)
            {
                Fail(WorkflowRuntimeErrorCodes.ExecutionLimitExceeded, "Runtime activation limit was exceeded.", step.Step.NodeId);
                return;
            }

            step.Status = WorkflowStepRuntimeStatus.Running;
            WorkflowExecutionPlanStep planStep = step.Step;
            WorkflowNode node = _nodesById[planStep.NodeId];
            int attempt = NextActivationOrdinal(node.Id);
            NodeExecutionIdentity identity = new(_request.ExecutionId, _invocationId, null, _request.Workflow.Id, node.Id, planStep.DefinitionKey, _request.PlanId, planStep.StepId, attempt);
            string nodeExecutionId = $"node-execution:{_request.ExecutionId}:{node.Id}:{attempt.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            _stateStore.CreateNode(identity, nodeExecutionId, _clock.UtcNow);
            _stateStore.TransitionNode(nodeExecutionId, ExecutionLifecycleState.Ready, _clock.UtcNow);
            await EmitAsync(RuntimeWorkflowEventKind.NodeReady, "Node ready.", node.Id, cancellationToken).ConfigureAwait(false);
            _stateStore.TransitionNode(nodeExecutionId, ExecutionLifecycleState.Running, _clock.UtcNow);
            await EmitAsync(RuntimeWorkflowEventKind.NodeStarted, "Node started.", node.Id, cancellationToken).ConfigureAwait(false);

            if (planStep.Kind == WorkflowExecutionPlanStepKind.Loop)
            {
                await ExecuteLoopStepAsync(step, nodeExecutionId, identity, handlerResolver, parameterMaterializer, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (planStep.Kind == WorkflowExecutionPlanStepKind.Invocation)
            {
                await ExecuteInvocationStepAsync(step, nodeExecutionId, identity, handlerResolver, parameterMaterializer, cancellationToken).ConfigureAwait(false);
                return;
            }

            WorkflowValueResolutionContext valueContext = new(_request.Inputs, MergeVariables(), _completedNodeOutputs, new Dictionary<string, WorkflowIterationContext>(StringComparer.Ordinal));
            PreparedNodeResult prepared = await PrepareNodeParametersAsync(step.Step, node, parameterMaterializer, valueContext, cancellationToken).ConfigureAwait(false);
            if (!prepared.IsSuccess || prepared.Parameters is null)
            {
                CompleteFailedStep(step, nodeExecutionId, identity, prepared.Error ?? new WorkflowError(WorkflowRuntimeErrorCodes.ParameterMaterializationFailed, "Parameter preparation failed.", node.Id), cancellationToken);
                return;
            }

            if (!handlerResolver.TryResolve(planStep.DefinitionKey, out INodeHandler? handler) || handler is null)
            {
                if (step.Step.Kind == WorkflowExecutionPlanStepKind.Interaction && string.Equals(node.Type, "interaction.request", StringComparison.Ordinal) && _ownerSession is not null)
                {
                    await ExecuteSuspendedInteractionAsync(step, nodeExecutionId, identity, prepared.Parameters.MaterializedParameters, cancellationToken).ConfigureAwait(false);
                    return;
                }

                CompleteFailedStep(step, nodeExecutionId, identity, WorkflowRuntimeErrorCodes.MissingNodeHandler, "Exact node handler was not found.", cancellationToken);
                return;
            }

            if (!handler.Definition.Equals(planStep.DefinitionKey))
            {
                CompleteFailedStep(step, nodeExecutionId, identity, WorkflowRuntimeErrorCodes.HandlerIdentityMismatch, "Resolved handler definition did not match the planned node definition.", cancellationToken);
                return;
            }

            NodeExecutionRequest nodeRequest = new(identity, prepared.Parameters.MaterializedParameters, step.ActivatedControlInputs.ToArray(), BuildDataInputs(step), new Dictionary<string, WorkflowIterationContext>(StringComparer.Ordinal));
            INodeResourceAccessor resourceAccessor = await PrepareResourceAccessorAsync(step.Step, prepared.Parameters.ResourceBindings, cancellationToken).ConfigureAwait(false);
            if (_terminalError is not null)
            {
                CompleteFailedStep(step, nodeExecutionId, identity, _terminalError, cancellationToken);
                return;
            }

            DefaultNodeExecutionContext context = new(identity, new RuntimeNodeExecutionEventWriter(_events, node.Id), resourceAccessor, new RuntimeNodeLocatorAccessor(prepared.Parameters.LocatorBindings));

            NodeHandlerResult handlerResult;
            try
            {
                handlerResult = await handler.ExecuteAsync(nodeRequest, context, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                CompleteCancelledStep(step, nodeExecutionId, identity);
                throw;
            }
            catch (Exception)
            {
                CompleteFailedStep(step, nodeExecutionId, identity, WorkflowRuntimeErrorCodes.HandlerUnexpectedException, "Node handler threw an unexpected exception.", cancellationToken);
                return;
            }

            if (handlerResult.Status == NodeHandlerCompletionStatus.Cancelled)
            {
                CompleteCancelledStep(step, nodeExecutionId, identity);
                _terminalStatus = WorkflowExecutionStatus.Cancelled;
                _terminalError = new WorkflowError(WorkflowRuntimeErrorCodes.ExecutionCancelled, "Execution was cancelled.");
                return;
            }

            if (handlerResult.Status == NodeHandlerCompletionStatus.Failed)
            {
                CompleteFailedStep(step, nodeExecutionId, identity, handlerResult.Error ?? new WorkflowError(WorkflowRuntimeErrorCodes.HandlerUnexpectedException, "Node handler failed.", node.Id), cancellationToken);
                return;
            }

            WorkflowError? contractError = ValidateOutputs(step, handlerResult.Outputs);
            if (contractError is not null)
            {
                CompleteFailedStep(step, nodeExecutionId, identity, contractError, cancellationToken);
                return;
            }

            PropagateOutputs(step, handlerResult.Outputs);
            NodeExecutionResult result = new(_request.ExecutionId, _request.Workflow.Id, _invocationId, node.Id, node.Type, NodeExecutionStatus.Succeeded, attempt, ProjectOutputs(handlerResult.Outputs.DataOutputs));
            step.Result = result;
            step.Outputs = new NodePortValueMap(handlerResult.Outputs.DataOutputs);
            step.Status = WorkflowStepRuntimeStatus.Succeeded;
            _completedNodeOutputs[node.Id] = step.Outputs;
            StoreNodeResult(nodeExecutionId, result);
            _stateStore.TransitionNode(nodeExecutionId, ExecutionLifecycleState.Completed, _clock.UtcNow, result);
            _nodeSnapshots.Add(_stateStore.GetNode(nodeExecutionId));
            await EmitAsync(RuntimeWorkflowEventKind.NodeCompleted, "Node completed.", node.Id, cancellationToken).ConfigureAwait(false);

            if (node.Type == "core.return")
            {
                _terminalOutcome = ExtractOutcome(handlerResult.Metadata);
            }
        }

        private async ValueTask ExecuteSuspendedInteractionAsync(
            StepState step,
            string nodeExecutionId,
            NodeExecutionIdentity identity,
            JsonObject parameters,
            CancellationToken cancellationToken)
        {
            Debug.Assert(_ownerSession is not null, "Suspended interactions require a session owner.");
            WorkflowNode node = _nodesById[step.Step.NodeId];
            WorkflowInteractionRequest interactionRequest;
            try
            {
                interactionRequest = BuildInteractionRequest(identity, parameters);
            }
            catch (InvalidOperationException exception)
            {
                CompleteFailedStep(step, nodeExecutionId, identity, WorkflowRuntimeErrorCodes.ParameterMaterializationFailed, exception.Message, cancellationToken);
                return;
            }

            DateTimeOffset? expiresAt = interactionRequest.Timeout is null ? null : _clock.UtcNow.Add(interactionRequest.Timeout.Value);
            _stateStore.TransitionExecution(_request.ExecutionId, ExecutionLifecycleState.Suspended, _clock.UtcNow);
            _stateStore.TransitionInvocation(_invocationId, ExecutionLifecycleState.Suspended, _clock.UtcNow);
            await EmitAsync(RuntimeWorkflowEventKind.ExecutionSuspended, "Execution suspended.", node.Id, cancellationToken).ConfigureAwait(false);

            WorkflowInteractionResponse response = await _ownerSession.SuspendAsync(interactionRequest, expiresAt, cancellationToken).ConfigureAwait(false);

            _stateStore.TransitionExecution(_request.ExecutionId, ExecutionLifecycleState.Running, _clock.UtcNow);
            _stateStore.TransitionInvocation(_invocationId, ExecutionLifecycleState.Running, _clock.UtcNow);
            await EmitAsync(RuntimeWorkflowEventKind.ExecutionResumed, "Execution resumed.", node.Id, cancellationToken).ConfigureAwait(false);
            if (response.Status == WorkflowInteractionResponseStatus.Cancelled)
            {
                CompleteCancelledStep(step, nodeExecutionId, identity);
                _terminalStatus = WorkflowExecutionStatus.Cancelled;
                _terminalError = new WorkflowError(WorkflowRuntimeErrorCodes.ExecutionCancelled, "Execution was cancelled while suspended.", node.Id);
                return;
            }

            JsonObject resultObject = new()
            {
                ["requestId"] = response.RequestId,
                ["status"] = response.Status.ToString(),
                ["hasValue"] = response.HasValue,
            };
            if (response.HasValue)
            {
                resultObject["value"] = response.Value;
            }

            NodeHandlerOutputs outputs = new(["result"], new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal)
            {
                ["result"] = new([resultObject]),
            });
            PropagateOutputs(step, outputs);
            NodeExecutionResult result = new(_request.ExecutionId, _request.Workflow.Id, _invocationId, node.Id, node.Type, NodeExecutionStatus.Succeeded, identity.Attempt, ProjectOutputs(outputs.DataOutputs));
            step.Result = result;
            step.Outputs = new NodePortValueMap(outputs.DataOutputs);
            step.Status = WorkflowStepRuntimeStatus.Succeeded;
            _completedNodeOutputs[node.Id] = step.Outputs;
            StoreNodeResult(nodeExecutionId, result);
            _stateStore.TransitionNode(nodeExecutionId, ExecutionLifecycleState.Completed, _clock.UtcNow, result);
            _nodeSnapshots.Add(_stateStore.GetNode(nodeExecutionId));
            await EmitAsync(RuntimeWorkflowEventKind.NodeCompleted, "Node completed.", node.Id, cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask ExecuteLoopStepAsync(
            StepState step,
            string nodeExecutionId,
            NodeExecutionIdentity identity,
            INodeHandlerResolver handlerResolver,
            NodeParameterMaterializer parameterMaterializer,
            CancellationToken cancellationToken)
        {
            WorkflowNode node = _nodesById[step.Step.NodeId];
            IReadOnlyDictionary<string, WorkflowIterationContext> emptyIterations = new Dictionary<string, WorkflowIterationContext>(StringComparer.Ordinal);
            WorkflowValueResolutionContext valueContext = new(_request.Inputs, MergeVariables(), _completedNodeOutputs, emptyIterations);
            WorkflowValueResult materialized = parameterMaterializer.MaterializeParameters(node.Parameters, valueContext);
            if (!materialized.IsSuccess)
            {
                CompleteFailedStep(step, nodeExecutionId, identity, WorkflowRuntimeErrorCodes.ParameterMaterializationFailed, materialized.Error?.Message ?? "Parameter materialization failed.", cancellationToken);
                return;
            }

            if (materialized.Value is not JsonObject parameters)
            {
                CompleteFailedStep(step, nodeExecutionId, identity, WorkflowRuntimeErrorCodes.InvalidLoopParameters, "Loop parameters must materialize to an object.", cancellationToken);
                return;
            }

            IReadOnlyList<WorkflowIterationContext> iterations = BuildLoopIterations(node, parameters, identity);
            if (_terminalError is not null)
            {
                CompleteFailedStep(step, nodeExecutionId, identity, _terminalError, cancellationToken);
                return;
            }

            foreach (WorkflowIterationContext iteration in iterations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LoopSignal signal = await ExecuteLoopBodyAsync(step.Step, iteration, handlerResolver, parameterMaterializer, cancellationToken).ConfigureAwait(false);
                if (signal == LoopSignal.Break)
                {
                    break;
                }

                if (signal == LoopSignal.Terminal)
                {
                    break;
                }

                if (signal == LoopSignal.NoProgress)
                {
                    CompleteFailedStep(step, nodeExecutionId, identity, WorkflowRuntimeErrorCodes.LoopNoProgress, "Loop body reached a no-progress state.", cancellationToken);
                    return;
                }

                if (_terminalError is not null || IsTerminalReturnCompleted())
                {
                    break;
                }
            }

            NodeHandlerOutputs outputs = new(["completed"]);
            PropagateOutputs(step, outputs);
            NodeExecutionResult result = new(_request.ExecutionId, _request.Workflow.Id, _invocationId, node.Id, node.Type, NodeExecutionStatus.Succeeded, identity.Attempt);
            step.Result = result;
            step.Outputs = new NodePortValueMap();
            step.Status = WorkflowStepRuntimeStatus.Succeeded;
            StoreNodeResult(nodeExecutionId, result);
            _stateStore.TransitionNode(nodeExecutionId, ExecutionLifecycleState.Completed, _clock.UtcNow, result);
            _nodeSnapshots.Add(_stateStore.GetNode(nodeExecutionId));
            await EmitAsync(RuntimeWorkflowEventKind.NodeCompleted, "Node completed.", node.Id, cancellationToken).ConfigureAwait(false);
        }

        private IReadOnlyList<WorkflowIterationContext> BuildLoopIterations(WorkflowNode node, JsonObject parameters, NodeExecutionIdentity identity)
        {
            List<WorkflowIterationContext> iterations = [];
            switch (node.Type)
            {
                case "flow.foreach":
                    if (parameters["items"] is not JsonArray items)
                    {
                        Fail(WorkflowRuntimeErrorCodes.InvalidLoopParameters, "flow.foreach requires an array items parameter.", node.Id);
                        return iterations;
                    }

                    if (items.Count > _options.MaximumLoopIterationsPerNode)
                    {
                        Fail(WorkflowRuntimeErrorCodes.ExecutionLimitExceeded, "Loop iteration limit was exceeded.", node.Id);
                        return iterations;
                    }

                    for (int index = 0; index < items.Count; index++)
                    {
                        iterations.Add(new WorkflowIterationContext(identity.NodeId, index, index + 1, items[index]?.DeepClone(), hasItem: true, items.Count));
                    }

                    return iterations;

                case "flow.repeat":
                    if (!TryGetLong(parameters["count"], out long count) || count < 0)
                    {
                        Fail(WorkflowRuntimeErrorCodes.InvalidLoopParameters, "flow.repeat requires a non-negative integer count parameter.", node.Id);
                        return iterations;
                    }

                    if (count > _options.MaximumLoopIterationsPerNode)
                    {
                        Fail(WorkflowRuntimeErrorCodes.ExecutionLimitExceeded, "Loop iteration limit was exceeded.", node.Id);
                        return iterations;
                    }

                    for (long index = 0; index < count; index++)
                    {
                        iterations.Add(new WorkflowIterationContext(identity.NodeId, index, index + 1, count: count));
                    }

                    return iterations;

                case "flow.while":
                    if (parameters["condition"]?.GetValueKind() != JsonValueKind.True)
                    {
                        return iterations;
                    }

                    for (long index = 0; index < _options.MaximumLoopIterationsPerNode; index++)
                    {
                        iterations.Add(new WorkflowIterationContext(identity.NodeId, index, index + 1));
                    }

                    return iterations;

                default:
                    Fail(WorkflowRuntimeErrorCodes.InvalidLoopParameters, "The loop node type is not supported.", node.Id);
                    return iterations;
            }
        }

        private async ValueTask<LoopSignal> ExecuteLoopBodyAsync(
            WorkflowExecutionPlanStep loopStep,
            WorkflowIterationContext iteration,
            INodeHandlerResolver handlerResolver,
            NodeParameterMaterializer parameterMaterializer,
            CancellationToken cancellationToken)
        {
            Dictionary<string, WorkflowIterationContext> iterations = new(StringComparer.Ordinal)
            {
                [loopStep.NodeId] = iteration,
            };
            Queue<(string StepId, string? TargetPort)> queue = new();
            foreach (WorkflowExecutionPlanDependency dependency in _plan.Dependencies.Where(dependency => dependency.Kind == WorkflowExecutionPlanDependencyKind.Control && dependency.StepId == loopStep.StepId && string.Equals(dependency.SourcePort, "body", StringComparison.Ordinal)))
            {
                if (dependency.TargetStepId is not null)
                {
                    queue.Enqueue((dependency.TargetStepId, dependency.TargetPort));
                }
            }

            if (queue.Count == 0)
            {
                return LoopSignal.Continue;
            }

            int localProgress = 0;
            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                (string stepId, string? targetPort) = queue.Dequeue();
                if (string.Equals(stepId, loopStep.StepId, StringComparison.Ordinal))
                {
                    return string.Equals(targetPort, "break", StringComparison.Ordinal) ? LoopSignal.Break : LoopSignal.Continue;
                }

                if (!_stepsById.TryGetValue(stepId, out WorkflowExecutionPlanStep? bodyStep))
                {
                    continue;
                }

                NodeHandlerOutputs? outputs = await ExecuteRepeatedStepAsync(bodyStep, targetPort ?? "main", iterations, handlerResolver, parameterMaterializer, cancellationToken).ConfigureAwait(false);
                localProgress++;
                if (outputs is null)
                {
                    return _terminalError is null ? LoopSignal.NoProgress : LoopSignal.Terminal;
                }

                if (bodyStep.Terminal || IsTerminalReturnCompleted())
                {
                    return LoopSignal.Terminal;
                }

                foreach (string control in outputs.ActivatedControlOutputs)
                {
                    foreach (WorkflowExecutionPlanDependency dependency in _plan.Dependencies.Where(dependency => dependency.Kind == WorkflowExecutionPlanDependencyKind.Control && dependency.StepId == bodyStep.StepId && string.Equals(dependency.SourcePort, control, StringComparison.Ordinal)))
                    {
                        if (dependency.TargetStepId is null)
                        {
                            continue;
                        }

                        if (string.Equals(dependency.TargetStepId, loopStep.StepId, StringComparison.Ordinal))
                        {
                            return string.Equals(dependency.TargetPort, "break", StringComparison.Ordinal) ? LoopSignal.Break : LoopSignal.Continue;
                        }

                        queue.Enqueue((dependency.TargetStepId, dependency.TargetPort));
                    }
                }
            }

            return localProgress == 0 ? LoopSignal.NoProgress : LoopSignal.Continue;
        }

        private async ValueTask<NodeHandlerOutputs?> ExecuteRepeatedStepAsync(
            WorkflowExecutionPlanStep planStep,
            string activatedControlInput,
            IReadOnlyDictionary<string, WorkflowIterationContext> iterations,
            INodeHandlerResolver handlerResolver,
            NodeParameterMaterializer parameterMaterializer,
            CancellationToken cancellationToken)
        {
            if (++_executedAttempts > _options.MaximumExecutedNodeAttempts || ++_activations > _options.MaximumRuntimeActivations)
            {
                Fail(WorkflowRuntimeErrorCodes.ExecutionLimitExceeded, "Runtime execution limit was exceeded.", planStep.NodeId);
                return null;
            }

            WorkflowNode node = _nodesById[planStep.NodeId];
            int attempt = NextActivationOrdinal(node.Id);
            NodeExecutionIdentity identity = new(_request.ExecutionId, _invocationId, null, _request.Workflow.Id, node.Id, planStep.DefinitionKey, _request.PlanId, planStep.StepId, attempt);
            string nodeExecutionId = $"node-execution:{_request.ExecutionId}:{node.Id}:{attempt.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            _stateStore.CreateNode(identity, nodeExecutionId, _clock.UtcNow);
            _stateStore.TransitionNode(nodeExecutionId, ExecutionLifecycleState.Ready, _clock.UtcNow);
            await EmitAsync(RuntimeWorkflowEventKind.NodeReady, "Node ready.", node.Id, cancellationToken).ConfigureAwait(false);
            _stateStore.TransitionNode(nodeExecutionId, ExecutionLifecycleState.Running, _clock.UtcNow);
            await EmitAsync(RuntimeWorkflowEventKind.NodeStarted, "Node started.", node.Id, cancellationToken).ConfigureAwait(false);

            WorkflowValueResolutionContext valueContext = new(_request.Inputs, MergeVariables(), _completedNodeOutputs, iterations);
            PreparedNodeResult prepared = await PrepareNodeParametersAsync(planStep, node, parameterMaterializer, valueContext, cancellationToken).ConfigureAwait(false);
            if (!prepared.IsSuccess || prepared.Parameters is null)
            {
                CompleteActivationFailure(planStep, nodeExecutionId, identity, prepared.Error ?? new WorkflowError(WorkflowRuntimeErrorCodes.ParameterMaterializationFailed, "Parameter preparation failed.", node.Id), cancellationToken);
                return null;
            }

            if (!handlerResolver.TryResolve(planStep.DefinitionKey, out INodeHandler? handler) || handler is null)
            {
                CompleteActivationFailure(planStep, nodeExecutionId, identity, WorkflowRuntimeErrorCodes.MissingNodeHandler, "Exact node handler was not found.", cancellationToken);
                return null;
            }

            INodeResourceAccessor resourceAccessor = await PrepareResourceAccessorAsync(planStep, prepared.Parameters.ResourceBindings, cancellationToken).ConfigureAwait(false);
            if (_terminalError is not null)
            {
                CompleteActivationFailure(planStep, nodeExecutionId, identity, _terminalError, cancellationToken);
                return null;
            }

            NodeExecutionRequest nodeRequest = new(identity, prepared.Parameters.MaterializedParameters, [activatedControlInput], BuildDataInputs(planStep), iterations);
            DefaultNodeExecutionContext context = new(identity, new RuntimeNodeExecutionEventWriter(_events, node.Id), resourceAccessor, new RuntimeNodeLocatorAccessor(prepared.Parameters.LocatorBindings));
            NodeHandlerResult handlerResult;
            try
            {
                handlerResult = await handler.ExecuteAsync(nodeRequest, context, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                CompleteActivationCancelled(planStep, nodeExecutionId, identity);
                throw;
            }
            catch (Exception)
            {
                CompleteActivationFailure(planStep, nodeExecutionId, identity, WorkflowRuntimeErrorCodes.HandlerUnexpectedException, "Node handler threw an unexpected exception.", cancellationToken);
                return null;
            }

            if (handlerResult.Status == NodeHandlerCompletionStatus.Cancelled)
            {
                CompleteActivationCancelled(planStep, nodeExecutionId, identity);
                _terminalStatus = WorkflowExecutionStatus.Cancelled;
                _terminalError = new WorkflowError(WorkflowRuntimeErrorCodes.ExecutionCancelled, "Execution was cancelled.");
                return null;
            }

            if (handlerResult.Status == NodeHandlerCompletionStatus.Failed)
            {
                CompleteActivationFailure(planStep, nodeExecutionId, identity, handlerResult.Error ?? new WorkflowError(WorkflowRuntimeErrorCodes.HandlerUnexpectedException, "Node handler failed.", node.Id), cancellationToken);
                return null;
            }

            if (_steps.TryGetValue(planStep.StepId, out StepState? state))
            {
                WorkflowError? contractError = ValidateOutputs(state, handlerResult.Outputs);
                if (contractError is not null)
                {
                    CompleteActivationFailure(planStep, nodeExecutionId, identity, contractError, cancellationToken);
                    return null;
                }

                state.Status = WorkflowStepRuntimeStatus.Succeeded;
                state.Outputs = new NodePortValueMap(handlerResult.Outputs.DataOutputs);
                state.Result = new NodeExecutionResult(_request.ExecutionId, _request.Workflow.Id, _invocationId, node.Id, node.Type, NodeExecutionStatus.Succeeded, attempt, ProjectOutputs(handlerResult.Outputs.DataOutputs));
            }

            NodeExecutionResult result = new(_request.ExecutionId, _request.Workflow.Id, _invocationId, node.Id, node.Type, NodeExecutionStatus.Succeeded, attempt, ProjectOutputs(handlerResult.Outputs.DataOutputs));
            _completedNodeOutputs[node.Id] = new NodePortValueMap(handlerResult.Outputs.DataOutputs);
            StoreNodeResult(nodeExecutionId, result);
            _stateStore.TransitionNode(nodeExecutionId, ExecutionLifecycleState.Completed, _clock.UtcNow, result);
            _nodeSnapshots.Add(_stateStore.GetNode(nodeExecutionId));
            await EmitAsync(RuntimeWorkflowEventKind.NodeCompleted, "Node completed.", node.Id, cancellationToken).ConfigureAwait(false);
            if (node.Type == "core.return")
            {
                _terminalOutcome = ExtractOutcome(handlerResult.Metadata);
            }

            return handlerResult.Outputs;
        }

        private async ValueTask ExecuteInvocationStepAsync(
            StepState step,
            string nodeExecutionId,
            NodeExecutionIdentity identity,
            INodeHandlerResolver handlerResolver,
            NodeParameterMaterializer parameterMaterializer,
            CancellationToken cancellationToken)
        {
            if (_workflowRepository is null)
            {
                CompleteFailedStep(step, nodeExecutionId, identity, WorkflowRuntimeErrorCodes.WorkflowInvocationNotFound, "No workflow repository was supplied for workflow.invoke.", cancellationToken);
                return;
            }

            if (++_invocations > _options.MaximumInvocations)
            {
                CompleteFailedStep(step, nodeExecutionId, identity, WorkflowRuntimeErrorCodes.ExecutionLimitExceeded, "Workflow invocation limit was exceeded.", cancellationToken);
                return;
            }

            WorkflowNode node = _nodesById[step.Step.NodeId];
            WorkflowValueResolutionContext valueContext = new(_request.Inputs, MergeVariables(), _completedNodeOutputs, new Dictionary<string, WorkflowIterationContext>(StringComparer.Ordinal));
            WorkflowValueResult materialized = parameterMaterializer.MaterializeParameters(node.Parameters, valueContext);
            if (!materialized.IsSuccess || materialized.Value is not JsonObject parameters)
            {
                CompleteFailedStep(step, nodeExecutionId, identity, WorkflowRuntimeErrorCodes.ParameterMaterializationFailed, materialized.Error?.Message ?? "Invocation parameter materialization failed.", cancellationToken);
                return;
            }

            WorkflowReference? reference = BuildWorkflowReference(parameters["workflow"]);
            if (reference is null)
            {
                CompleteFailedStep(step, nodeExecutionId, identity, WorkflowRuntimeErrorCodes.WorkflowInvocationNotFound, "workflow.invoke requires a workflow reference.", cancellationToken);
                return;
            }

            WorkflowRepositoryLookupResult lookup = await _workflowRepository.LookupAsync(reference, cancellationToken).ConfigureAwait(false);
            if (!lookup.Found || lookup.Workflow is null)
            {
                CompleteFailedStep(step, nodeExecutionId, identity, WorkflowRuntimeErrorCodes.WorkflowInvocationNotFound, lookup.Diagnostic ?? "Workflow reference was not found.", cancellationToken);
                return;
            }

            Dictionary<string, JsonNode?> inputs = BuildInvocationInputs(parameters["inputs"] as JsonObject);
            WorkflowExecutionRequest childRequest = new(lookup.Workflow, _request.ExecutionId + ":child:" + identity.Attempt.ToString(System.Globalization.CultureInfo.InvariantCulture), _request.PlanId + ":child", inputs, eventSink: _request.EventSink);
            DefaultWorkflowRuntime childRuntime = new(_validator, _analyzer, _planner, _catalog, handlerResolver, parameterMaterializer, _clock, _options, _workflowRepository, _resourceProviders.Values.ToArray(), _locatorResolver);
            WorkflowRuntimeResult child = await childRuntime.ExecuteAsync(childRequest, cancellationToken).ConfigureAwait(false);
            if (child.Result.Status != WorkflowExecutionStatus.Succeeded)
            {
                CompleteFailedStep(step, nodeExecutionId, identity, WorkflowRuntimeErrorCodes.WorkflowInvocationFailed, child.Result.Error?.Message ?? "Child workflow invocation failed.", cancellationToken);
                return;
            }

            JsonObject resultObject = new()
            {
                ["executionId"] = child.Result.ExecutionId,
                ["workflowId"] = child.Result.WorkflowId,
                ["status"] = child.Result.Status.ToString(),
                ["outputs"] = ToJsonObject(child.Result.Outputs),
            };
            NodeHandlerOutputs outputs = new(["result"], new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal)
            {
                ["result"] = new([resultObject]),
            });
            PropagateOutputs(step, outputs);
            NodeExecutionResult result = new(_request.ExecutionId, _request.Workflow.Id, _invocationId, node.Id, node.Type, NodeExecutionStatus.Succeeded, identity.Attempt, ProjectOutputs(outputs.DataOutputs));
            step.Result = result;
            step.Outputs = new NodePortValueMap(outputs.DataOutputs);
            step.Status = WorkflowStepRuntimeStatus.Succeeded;
            _completedNodeOutputs[node.Id] = step.Outputs;
            StoreNodeResult(nodeExecutionId, result);
            _stateStore.TransitionNode(nodeExecutionId, ExecutionLifecycleState.Completed, _clock.UtcNow, result);
            _nodeSnapshots.Add(_stateStore.GetNode(nodeExecutionId));
            await EmitAsync(RuntimeWorkflowEventKind.NodeCompleted, "Node completed.", node.Id, cancellationToken).ConfigureAwait(false);
        }

        private IReadOnlyDictionary<string, JsonNode?> MergeVariables()
        {
            Dictionary<string, JsonNode?> variables = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, JsonNode?> value in _request.Workflow.Variables)
            {
                variables[value.Key] = value.Value?.DeepClone();
            }

            foreach (KeyValuePair<string, JsonNode?> value in _request.Variables)
            {
                variables[value.Key] = value.Value?.DeepClone();
            }

            return variables;
        }

        private IReadOnlyDictionary<string, NodePortValueSet> BuildDataInputs(StepState step)
        {
            return BuildDataInputs(step.Step);
        }

        private IReadOnlyDictionary<string, NodePortValueSet> BuildDataInputs(WorkflowExecutionPlanStep step)
        {
            Dictionary<string, List<JsonNode?>> values = new(StringComparer.Ordinal);
            foreach (WorkflowExecutionPlanDependency dependency in step.DependsOn.Where(static dependency => dependency.Kind == WorkflowExecutionPlanDependencyKind.Data && dependency.TargetPort is not null && dependency.SourcePort is not null))
            {
                string sourceNodeId = _stepsById[dependency.StepId].NodeId;
                string sourcePort = dependency.SourcePort!;
                string targetPort = dependency.TargetPort!;
                IReadOnlyList<JsonNode?> sourceValues = _completedNodeOutputs[sourceNodeId].Values[sourcePort].Values;
                if (!values.TryGetValue(targetPort, out List<JsonNode?>? targetValues))
                {
                    targetValues = [];
                    values.Add(targetPort, targetValues);
                }

                targetValues.AddRange(sourceValues.Select(static value => value?.DeepClone()));
            }

            return values.ToDictionary(static pair => pair.Key, static pair => new NodePortValueSet(pair.Value), StringComparer.Ordinal);
        }

        private async ValueTask<PreparedNodeResult> PrepareNodeParametersAsync(
            WorkflowExecutionPlanStep step,
            WorkflowNode node,
            NodeParameterMaterializer parameterMaterializer,
            WorkflowValueResolutionContext valueContext,
            CancellationToken cancellationToken)
        {
            var stripped = (JsonObject)node.Parameters.DeepClone();
            HashSet<string> resourcePaths = new(StringComparer.Ordinal);
            List<NodeResourceBinding> resourceBindings = [];
            foreach (WorkflowExecutionPlanResourceUse use in step.Resources)
            {
                string path = "/" + EscapePointerToken(use.SlotName);
                resourcePaths.Add(path);
                RemovePointer(stripped, path);
                if (_request.Workflow.Resources.TryGetValue(use.ResourceName, out WorkflowResourceDefinition? resourceDefinition))
                {
                    resourceBindings.Add(new NodeResourceBinding(use.SlotName, use.ResourceName, resourceDefinition.Kind, use.Access, use.Capabilities, use.Required));
                }
            }

            try
            {
                foreach (WorkflowResourceReferenceOccurrence occurrence in new WorkflowResourceReferenceReader().FindResourceReferences(node.Parameters))
                {
                    if (!resourcePaths.Contains(occurrence.Path))
                    {
                        return PreparedNodeResult.Failure(new WorkflowError(WorkflowRuntimeErrorCodes.ParameterMaterializationFailed, "Undeclared `$resource` wrapper is not allowed.", node.Id));
                    }
                }
            }
            catch (WorkflowResourceReferenceFormatException exception)
            {
                return PreparedNodeResult.Failure(new WorkflowError(WorkflowRuntimeErrorCodes.ParameterMaterializationFailed, exception.Message, node.Id));
            }

            List<NodeLocatorBinding> locatorBindings = [];
            HashSet<string> locatorPaths = new(StringComparer.Ordinal);
            if (_catalog.TryGetDefinition(step.NodeType, step.TypeVersion, out WorkflowNodeDefinition? nodeDefinition))
            {
                foreach (NodeLocatorSlotDefinition slot in nodeDefinition!.Locators.Values.OrderBy(static slot => slot.Name, StringComparer.Ordinal))
                {
                    locatorPaths.Add(slot.ParameterPointer);
                    if (!TryResolvePointer(node.Parameters, slot.ParameterPointer, out JsonNode? wrapper))
                    {
                        if (slot.Required)
                        {
                            return PreparedNodeResult.Failure(new WorkflowError(WorkflowRuntimeErrorCodes.ParameterMaterializationFailed, $"Required locator slot '{slot.Name}' is missing.", node.Id));
                        }

                        continue;
                    }

                    LocatorReference reference;
                    try
                    {
                        reference = new LocatorReferenceReader().Read(wrapper!);
                    }
                    catch (LocatorReferenceFormatException exception)
                    {
                        return PreparedNodeResult.Failure(new WorkflowError(WorkflowRuntimeErrorCodes.ParameterMaterializationFailed, exception.Message, node.Id));
                    }

                    ResolvedLocatorPlan? resolved = step.Locators.FirstOrDefault(use => string.Equals(use.SlotName, slot.Name, StringComparison.Ordinal))?.ResolvedLocator;
                    if (resolved is null)
                    {
                        if (_locatorResolver is null)
                        {
                            return PreparedNodeResult.Failure(new WorkflowError(WorkflowRuntimeErrorCodes.ParameterMaterializationFailed, "A locator resolver is required for `$locator` wrappers.", node.Id));
                        }

                        try
                        {
                            resolved = await _locatorResolver.ResolveAsync(reference, cancellationToken).ConfigureAwait(false);
                        }
                        catch (LocatorPlanResolutionException exception)
                        {
                            return PreparedNodeResult.Failure(new WorkflowError(exception.Code, exception.Message, node.Id));
                        }
                    }

                    if (!slot.AcceptedCardinalities.Contains(resolved.Cardinality))
                    {
                        return PreparedNodeResult.Failure(new WorkflowError("SKR2009", $"Locator slot '{slot.Name}' does not accept the resolved cardinality.", node.Id));
                    }

                    locatorBindings.Add(new NodeLocatorBinding(slot.Name, reference, resolved, slot.Required));
                    RemovePointer(stripped, slot.ParameterPointer);
                }
            }

            try
            {
                foreach (LocatorReferenceOccurrence occurrence in new LocatorReferenceReader().FindLocatorReferences(node.Parameters))
                {
                    if (!locatorPaths.Contains(occurrence.Path))
                    {
                        return PreparedNodeResult.Failure(new WorkflowError(WorkflowRuntimeErrorCodes.ParameterMaterializationFailed, "Undeclared `$locator` wrapper is not allowed.", node.Id));
                    }
                }
            }
            catch (LocatorReferenceFormatException exception)
            {
                return PreparedNodeResult.Failure(new WorkflowError(WorkflowRuntimeErrorCodes.ParameterMaterializationFailed, exception.Message, node.Id));
            }

            WorkflowValueResult materialized = parameterMaterializer.MaterializeParameters(stripped, valueContext);
            if (!materialized.IsSuccess)
            {
                return PreparedNodeResult.Failure(new WorkflowError(WorkflowRuntimeErrorCodes.ParameterMaterializationFailed, materialized.Error?.Message ?? "Parameter materialization failed.", node.Id));
            }

            return PreparedNodeResult.Success(new PreparedNodeParameters((JsonObject)materialized.Value!, resourceBindings, locatorBindings));
        }

        private async ValueTask<INodeResourceAccessor> PrepareResourceAccessorAsync(WorkflowExecutionPlanStep step, IReadOnlyList<NodeResourceBinding> preparedBindings, CancellationToken cancellationToken)
        {
            if (step.Resources.Count == 0)
            {
                return EmptyNodeResourceAccessor.Instance;
            }

            foreach (WorkflowExecutionPlanResourceUse use in step.Resources)
            {
                if (!_request.Workflow.Resources.TryGetValue(use.ResourceName, out WorkflowResourceDefinition? definition))
                {
                    if (use.Required)
                    {
                        Fail(WorkflowRuntimeErrorCodes.RequiredDependencyUnavailable, "A required workflow resource declaration was unavailable.", step.NodeId);
                    }

                    continue;
                }

                if (!_resourceProviders.TryGetValue(definition.Kind, out IWorkflowRuntimeResourceProvider? provider))
                {
                    if (use.Required || definition.Required)
                    {
                        Fail(WorkflowRuntimeErrorCodes.RuntimeResourceProviderUnavailable, "A required runtime resource provider was unavailable.", step.NodeId);
                    }

                    continue;
                }

                if (!_resourcesByName.TryGetValue(use.ResourceName, out IWorkflowRuntimeResourceInstance? instance))
                {
                    WorkflowRuntimeResourceRequest request = new(_request.ExecutionId, _invocationId, _request.Workflow.Id, use.ResourceName, definition);
                    instance = await provider.CreateAsync(request, cancellationToken).ConfigureAwait(false);
                    if (!string.Equals(instance.Kind, definition.Kind, StringComparison.Ordinal))
                    {
                        Fail(WorkflowRuntimeErrorCodes.RuntimeResourceProviderInvalid, "Runtime resource provider returned a resource with the wrong kind.", step.NodeId);
                        await instance.DisposeAsync().ConfigureAwait(false);
                        continue;
                    }

                    _resourcesByName[use.ResourceName] = instance;
                }
            }

            return new RuntimeNodeResourceAccessor(preparedBindings, _resourcesByName);
        }

        private static void RemovePointer(JsonObject root, string pointer)
        {
            if (string.IsNullOrEmpty(pointer) || !pointer.StartsWith("/", StringComparison.Ordinal))
            {
                return;
            }

            JsonNode? current = root;
            string[] segments = pointer[1..].Split('/');
            for (int index = 0; index < segments.Length - 1; index++)
            {
                string segment = Unescape(segments[index]);
                if (current is not JsonObject currentObject || !currentObject.TryGetPropertyValue(segment, out current))
                {
                    return;
                }
            }

            if (current is JsonObject parent)
            {
                parent.Remove(Unescape(segments[^1]));
            }
        }

        private static string Unescape(string value)
        {
            return value.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
        }

        private static string EscapePointerToken(string value)
        {
            return value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
        }

        private static bool TryResolvePointer(JsonNode? root, string pointer, out JsonNode? value)
        {
            value = root;
            if (root is null)
            {
                return false;
            }

            if (pointer.Length == 0)
            {
                return true;
            }

            if (!pointer.StartsWith("/", StringComparison.Ordinal))
            {
                value = null;
                return false;
            }

            foreach (string rawToken in pointer[1..].Split('/'))
            {
                string token = Unescape(rawToken);
                if (value is JsonObject obj)
                {
                    if (!obj.TryGetPropertyValue(token, out value))
                    {
                        return false;
                    }
                }
                else if (value is JsonArray array)
                {
                    if (!int.TryParse(token, out int index) || index < 0 || index >= array.Count)
                    {
                        value = null;
                        return false;
                    }

                    value = array[index];
                }
                else
                {
                    value = null;
                    return false;
                }
            }

            return true;
        }

        private int NextActivationOrdinal(string nodeId)
        {
            _nodeActivationOrdinals.TryGetValue(nodeId, out int current);
            int next = current + 1;
            _nodeActivationOrdinals[nodeId] = next;
            return next;
        }

        private void CompleteActivationFailure(WorkflowExecutionPlanStep step, string nodeExecutionId, NodeExecutionIdentity identity, string code, string message, CancellationToken cancellationToken)
        {
            CompleteActivationFailure(step, nodeExecutionId, identity, new WorkflowError(code, message, step.NodeId), cancellationToken);
        }

        private void CompleteActivationFailure(WorkflowExecutionPlanStep step, string nodeExecutionId, NodeExecutionIdentity identity, WorkflowError error, CancellationToken cancellationToken)
        {
            NodeExecutionResult result = new(_request.ExecutionId, _request.Workflow.Id, _invocationId, identity.NodeId, identity.Definition.Type, NodeExecutionStatus.Failed, identity.Attempt, error: error);
            if (_steps.TryGetValue(step.StepId, out StepState? state))
            {
                state.Status = WorkflowStepRuntimeStatus.Failed;
                state.Result = result;
            }

            StoreNodeResult(nodeExecutionId, result);
            _stateStore.TransitionNode(nodeExecutionId, ExecutionLifecycleState.Completed, _clock.UtcNow, result);
            _nodeSnapshots.Add(_stateStore.GetNode(nodeExecutionId));
            _terminalStatus = WorkflowExecutionStatus.Failed;
            _terminalError = error;
            _events.PublishAsync(RuntimeWorkflowEventKind.NodeFailed, "Node failed.", identity.NodeId, data: ErrorData(error), cancellationToken: cancellationToken).AsTask().GetAwaiter().GetResult();
        }

        private void CompleteActivationCancelled(WorkflowExecutionPlanStep step, string nodeExecutionId, NodeExecutionIdentity identity)
        {
            WorkflowError error = new(WorkflowRuntimeErrorCodes.ExecutionCancelled, "Node execution was cancelled.", identity.NodeId);
            NodeExecutionResult result = new(_request.ExecutionId, _request.Workflow.Id, _invocationId, identity.NodeId, identity.Definition.Type, NodeExecutionStatus.Cancelled, identity.Attempt, error: error);
            if (_steps.TryGetValue(step.StepId, out StepState? state))
            {
                state.Status = WorkflowStepRuntimeStatus.Cancelled;
                state.Result = result;
            }

            StoreNodeResult(nodeExecutionId, result);
            _stateStore.TransitionNode(nodeExecutionId, ExecutionLifecycleState.Cancelling, _clock.UtcNow);
            _stateStore.TransitionNode(nodeExecutionId, ExecutionLifecycleState.Completed, _clock.UtcNow, result);
            _nodeSnapshots.Add(_stateStore.GetNode(nodeExecutionId));
        }

        private WorkflowError? ValidateOutputs(StepState step, NodeHandlerOutputs outputs)
        {
            WorkflowNodeAnalysis nodeAnalysis = _analysisByNode[step.Step.NodeId];
            var outputPorts = nodeAnalysis.EffectivePorts
                .Where(static port => port.Direction == WorkflowPortDirection.Output)
                .ToDictionary(static port => port.Id, StringComparer.Ordinal);

            foreach (string control in outputs.ActivatedControlOutputs)
            {
                if (!outputPorts.TryGetValue(control, out WorkflowEffectivePort? port) || !port.Roles.Contains("control", StringComparer.Ordinal))
                {
                    return new WorkflowError(WorkflowRuntimeErrorCodes.InvalidHandlerControlOutput, "Handler returned an unknown or non-control output.", step.Step.NodeId);
                }
            }

            foreach (KeyValuePair<string, NodePortValueSet> data in outputs.DataOutputs)
            {
                if (!outputPorts.TryGetValue(data.Key, out WorkflowEffectivePort? port) || !port.Roles.Contains("data", StringComparer.Ordinal))
                {
                    return new WorkflowError(WorkflowRuntimeErrorCodes.InvalidHandlerDataOutput, "Handler returned an unknown or non-data output.", step.Step.NodeId);
                }

                if (!port.AllowsMultiple && data.Value.Values.Count > 1)
                {
                    return new WorkflowError(WorkflowRuntimeErrorCodes.InvalidHandlerDataOutput, "Handler returned multiple values for a single-value output.", step.Step.NodeId);
                }
            }

            return null;
        }

        private void PropagateOutputs(StepState step, NodeHandlerOutputs outputs)
        {
            foreach (string control in outputs.ActivatedControlOutputs)
            {
                foreach (WorkflowExecutionPlanDependency dependency in _plan.Dependencies.Where(dependency => dependency.Kind == WorkflowExecutionPlanDependencyKind.Control && dependency.StepId == step.Step.StepId && string.Equals(dependency.SourcePort, control, StringComparison.Ordinal)))
                {
                    if (dependency.TargetStepId is not null && _steps.TryGetValue(dependency.TargetStepId, out StepState? target) && dependency.TargetPort is not null)
                    {
                        target.ActivatedControlInputs.Add(dependency.TargetPort);
                    }
                }
            }
        }

        private void CompleteFailedStep(StepState step, string nodeExecutionId, NodeExecutionIdentity identity, string code, string message, CancellationToken cancellationToken)
        {
            CompleteFailedStep(step, nodeExecutionId, identity, new WorkflowError(code, message, step.Step.NodeId), cancellationToken);
        }

        private void CompleteFailedStep(StepState step, string nodeExecutionId, NodeExecutionIdentity identity, WorkflowError error, CancellationToken cancellationToken)
        {
            NodeExecutionResult result = new(_request.ExecutionId, _request.Workflow.Id, _invocationId, identity.NodeId, identity.Definition.Type, NodeExecutionStatus.Failed, identity.Attempt, error: error);
            step.Status = WorkflowStepRuntimeStatus.Failed;
            step.Result = result;
            StoreNodeResult(nodeExecutionId, result);
            _stateStore.TransitionNode(nodeExecutionId, ExecutionLifecycleState.Completed, _clock.UtcNow, result);
            _nodeSnapshots.Add(_stateStore.GetNode(nodeExecutionId));
            _terminalStatus = WorkflowExecutionStatus.Failed;
            _terminalError = error;
            _events.PublishAsync(RuntimeWorkflowEventKind.NodeFailed, "Node failed.", identity.NodeId, data: ErrorData(error), cancellationToken: cancellationToken).AsTask().GetAwaiter().GetResult();
        }

        private void CompleteCancelledStep(StepState step, string nodeExecutionId, NodeExecutionIdentity identity)
        {
            WorkflowError error = new(WorkflowRuntimeErrorCodes.ExecutionCancelled, "Node execution was cancelled.", identity.NodeId);
            NodeExecutionResult result = new(_request.ExecutionId, _request.Workflow.Id, _invocationId, identity.NodeId, identity.Definition.Type, NodeExecutionStatus.Cancelled, identity.Attempt, error: error);
            step.Status = WorkflowStepRuntimeStatus.Cancelled;
            step.Result = result;
            StoreNodeResult(nodeExecutionId, result);
            _stateStore.TransitionNode(nodeExecutionId, ExecutionLifecycleState.Cancelling, _clock.UtcNow);
            _stateStore.TransitionNode(nodeExecutionId, ExecutionLifecycleState.Completed, _clock.UtcNow, result);
            _nodeSnapshots.Add(_stateStore.GetNode(nodeExecutionId));
        }

        private async ValueTask MarkCancellationAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (_stateStore.GetExecution(_request.ExecutionId).State == ExecutionLifecycleState.Running)
                {
                    _stateStore.TransitionExecution(_request.ExecutionId, ExecutionLifecycleState.Cancelling, _clock.UtcNow);
                }

                if (_stateStore.GetInvocation(_invocationId).State == ExecutionLifecycleState.Running)
                {
                    _stateStore.TransitionInvocation(_invocationId, ExecutionLifecycleState.Cancelling, _clock.UtcNow);
                }
            }
            catch (KeyNotFoundException)
            {
            }

            await EmitAsync(RuntimeWorkflowEventKind.ExecutionCancelled, "Execution cancelled.", cancellationToken: CancellationToken.None).ConfigureAwait(false);
        }

        private void SkipUnreachedSteps()
        {
            foreach (StepState step in _steps.Values.Where(static step => step.Status == WorkflowStepRuntimeStatus.Pending || step.Status == WorkflowStepRuntimeStatus.Ready))
            {
                step.Status = WorkflowStepRuntimeStatus.Skipped;
                NodeExecutionResult result = new(_request.ExecutionId, _request.Workflow.Id, _invocationId, step.Step.NodeId, step.Step.NodeType, NodeExecutionStatus.Skipped, 1);
                step.Result = result;
                StoreNodeResult($"node-execution:{_request.ExecutionId}:{step.Step.NodeId}:1", result);
            }
        }

        private WorkflowExecutionResult CreateWorkflowResult()
        {
            WorkflowExecutionMetrics metrics = new(_nodeResults.Count(result => result.Status == NodeExecutionStatus.Succeeded), _events.RecordsEmitted, _stopwatch.ElapsedMilliseconds);
            return new WorkflowExecutionResult(
                _request.ExecutionId,
                _request.Workflow.Id,
                _invocationId,
                null,
                _terminalError is not null ? _terminalStatus : WorkflowExecutionStatus.Succeeded,
                _terminalOutcome,
                AggregateWorkflowOutputs(),
                metrics,
                _terminalError);
        }

        private IReadOnlyDictionary<string, JsonNode?> AggregateWorkflowOutputs()
        {
            Dictionary<string, JsonNode?> outputs = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, WorkflowOutputDefinition> output in _request.Workflow.Outputs)
            {
                if (output.Value.Mode == WorkflowOutputMode.Stream || output.Value.From is null)
                {
                    continue;
                }

                string nodeId = output.Value.From.Value.Node;
                string port = output.Value.From.Value.Port;
                if (!_completedNodeOutputs.TryGetValue(nodeId, out NodePortValueMap? nodeOutput) || !nodeOutput.Values.TryGetValue(port, out NodePortValueSet? values))
                {
                    continue;
                }

                outputs[output.Key] = output.Value.Mode == WorkflowOutputMode.Collection
                    ? ToArray(values.Values)
                    : Project(values);
            }

            return outputs;
        }

        private void StoreNodeResult(string nodeExecutionId, NodeExecutionResult result)
        {
            if (_nodeResults.Count < _options.MaximumStoredNodeResults)
            {
                _nodeResults.Add(result);
            }
            else
            {
                _terminalStatus = WorkflowExecutionStatus.Failed;
                _terminalError = new WorkflowError(WorkflowRuntimeErrorCodes.ExecutionLimitExceeded, "Stored node result limit was exceeded.");
            }
        }

        private bool IsTerminalReturnCompleted()
        {
            return _steps.Values.Any(static step => step.Step.Terminal && step.Status == WorkflowStepRuntimeStatus.Succeeded);
        }

        private void Fail(string code, string message, string? nodeId = null)
        {
            _terminalStatus = WorkflowExecutionStatus.Failed;
            _terminalError = new WorkflowError(code, message, nodeId);
        }

        private ValueTask EmitAsync(RuntimeWorkflowEventKind kind, string message, string? nodeId = null, CancellationToken cancellationToken = default)
        {
            if (!_options.EmitStateTransitionEvents && kind is not RuntimeWorkflowEventKind.ExecutionFailed and not RuntimeWorkflowEventKind.ExecutionCancelled)
            {
                return ValueTask.CompletedTask;
            }

            return _events.PublishAsync(kind, message, nodeId, cancellationToken: cancellationToken);
        }

        private static IReadOnlyDictionary<string, JsonNode?> ProjectOutputs(IReadOnlyDictionary<string, NodePortValueSet> outputs)
        {
            Dictionary<string, JsonNode?> projected = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, NodePortValueSet> output in outputs)
            {
                projected[output.Key] = Project(output.Value);
            }

            return projected;
        }

        private static WorkflowReference? BuildWorkflowReference(JsonNode? workflow)
        {
            if (workflow is JsonObject reference)
            {
                string? id = reference["id"]?.GetValueKind() == JsonValueKind.String ? reference["id"]!.GetValue<string>() : null;
                if (string.IsNullOrWhiteSpace(id))
                {
                    return null;
                }

                string? version = reference["version"]?.GetValueKind() == JsonValueKind.String ? reference["version"]!.GetValue<string>() : null;
                return new WorkflowReference(id, version);
            }

            if (workflow?.GetValueKind() == JsonValueKind.String)
            {
                string id = workflow.GetValue<string>();
                return string.IsNullOrWhiteSpace(id) ? null : new WorkflowReference(id);
            }

            return null;
        }

        private static WorkflowInteractionRequest BuildInteractionRequest(NodeExecutionIdentity identity, JsonObject parameters)
        {
            if (parameters["kind"] is null || parameters["prompt"] is null || parameters["prompt"]!.GetValueKind() != JsonValueKind.String)
            {
                throw new InvalidOperationException("interaction.request requires materialized kind and prompt parameters.");
            }

            WorkflowInteractionKind kind = parameters["kind"]!.GetValue<string>() switch
            {
                "confirmation" => WorkflowInteractionKind.Confirmation,
                "choice" => WorkflowInteractionKind.Choice,
                "manual-action" => WorkflowInteractionKind.ManualAction,
                "secret" => WorkflowInteractionKind.Secret,
                "text" => WorkflowInteractionKind.Text,
                "multiple-choice" => WorkflowInteractionKind.MultipleChoice,
                _ => throw new InvalidOperationException("interaction.request kind is not supported."),
            };

            TimeSpan? timeout = null;
            if (parameters["timeoutSeconds"] is not null && TryGetLong(parameters["timeoutSeconds"], out long seconds) && seconds >= 0)
            {
                timeout = TimeSpan.FromSeconds(seconds);
            }

            return new WorkflowInteractionRequest(
                $"interaction:{identity.ExecutionId}:{identity.NodeId}:{identity.Attempt.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                identity.ExecutionId,
                identity.InvocationId,
                identity.WorkflowId,
                identity.NodeId,
                kind,
                parameters["prompt"]!.GetValue<string>(),
                timeout: timeout);
        }

        private static Dictionary<string, JsonNode?> BuildInvocationInputs(JsonObject? inputs)
        {
            Dictionary<string, JsonNode?> mapped = new(StringComparer.Ordinal);
            if (inputs is null)
            {
                return mapped;
            }

            foreach (KeyValuePair<string, JsonNode?> input in inputs)
            {
                mapped[input.Key] = input.Value?.DeepClone();
            }

            return mapped;
        }

        private static JsonObject ToJsonObject(IReadOnlyDictionary<string, JsonNode?> values)
        {
            JsonObject json = [];
            foreach (KeyValuePair<string, JsonNode?> value in values)
            {
                json[value.Key] = value.Value?.DeepClone();
            }

            return json;
        }

        private static bool TryGetLong(JsonNode? node, out long value)
        {
            value = 0;
            if (node is not JsonValue jsonValue)
            {
                return false;
            }

            if (jsonValue.TryGetValue(out long longValue))
            {
                value = longValue;
                return true;
            }

            if (jsonValue.TryGetValue(out int intValue))
            {
                value = intValue;
                return true;
            }

            if (jsonValue.TryGetValue(out double doubleValue) && double.IsInteger(doubleValue))
            {
                value = checked((long)doubleValue);
                return true;
            }

            return false;
        }

        private static JsonNode? Project(NodePortValueSet values)
        {
            IReadOnlyList<JsonNode?> items = values.Values;
            if (items.Count == 0)
            {
                return null;
            }

            if (items.Count == 1)
            {
                return items[0]?.DeepClone();
            }

            return ToArray(items);
        }

        private static JsonArray ToArray(IReadOnlyList<JsonNode?> items)
        {
            JsonArray array = [];
            foreach (JsonNode? item in items)
            {
                array.Add(item?.DeepClone());
            }

            return array;
        }

        private static WorkflowOutcome? ExtractOutcome(JsonObject? metadata)
        {
            if (metadata?["outcome"] is not JsonObject outcome)
            {
                return null;
            }

            string kindText = outcome["kind"]?.GetValue<string>() ?? "success";
            WorkflowOutcomeKind kind = kindText switch
            {
                "partial" => WorkflowOutcomeKind.Partial,
                "requires-action" => WorkflowOutcomeKind.RequiresAction,
                "no-results" => WorkflowOutcomeKind.NoResults,
                "skipped" => WorkflowOutcomeKind.Skipped,
                _ => WorkflowOutcomeKind.Success,
            };
            string code = outcome["code"]?.GetValue<string>() ?? "completed";
            string? message = outcome["message"]?.GetValueKind() == JsonValueKind.String ? outcome["message"]!.GetValue<string>() : null;
            var data = outcome["data"] as JsonObject;
            return new WorkflowOutcome(kind, code, message, data);
        }

        private static JsonObject ErrorData(WorkflowError error)
        {
            return new JsonObject
            {
                ["code"] = error.Code,
                ["message"] = error.Message,
            };
        }

        private enum LoopSignal
        {
            Continue,
            Break,
            Terminal,
            NoProgress,
        }
    }

    private sealed class StepState
    {
        public StepState(WorkflowExecutionPlanStep step)
        {
            Step = step;
        }

        public WorkflowExecutionPlanStep Step { get; }

        public WorkflowStepRuntimeStatus Status { get; set; } = WorkflowStepRuntimeStatus.Pending;

        public bool EntryActivated { get; set; }

        public SortedSet<string> ActivatedControlInputs { get; } = new(StringComparer.Ordinal);

        public NodePortValueMap Outputs { get; set; } = new();

        public NodeExecutionResult? Result { get; set; }

        public bool IsTerminalSuccess => Status == WorkflowStepRuntimeStatus.Succeeded;
    }

    private sealed class DefaultNodeExecutionContext(NodeExecutionIdentity identity, INodeExecutionEventWriter events, INodeResourceAccessor resources, INodeLocatorAccessor locators) : INodeExecutionContext
    {
        public NodeExecutionIdentity Identity { get; } = identity;

        public INodeExecutionEventWriter Events { get; } = events;

        public INodeResourceAccessor Resources { get; } = resources;

        public INodeLocatorAccessor Locators { get; } = locators;
    }

    private sealed class PreparedNodeResult
    {
        private PreparedNodeResult(PreparedNodeParameters? parameters, WorkflowError? error)
        {
            Parameters = parameters;
            Error = error;
        }

        public bool IsSuccess => Error is null;

        public PreparedNodeParameters? Parameters { get; }

        public WorkflowError? Error { get; }

        public static PreparedNodeResult Success(PreparedNodeParameters parameters)
        {
            return new(parameters, null);
        }

        public static PreparedNodeResult Failure(WorkflowError error)
        {
            return new(null, error);
        }
    }

    private sealed class EmptyNodeResourceAccessor : INodeResourceAccessor
    {
        public static readonly EmptyNodeResourceAccessor Instance = new();

        public IReadOnlyList<NodeResourceBinding> Bindings { get; } = Array.AsReadOnly(Array.Empty<NodeResourceBinding>());

        public bool TryGetBinding(string slotName, out NodeResourceBinding? binding)
        {
            binding = null;
            return false;
        }

        public ValueTask<INodeResourceLease> AcquireAsync(string slotName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Runtime resource resolution is not implemented in Phase 0-13.");
        }
    }

    private sealed class RuntimeNodeExecutionEventWriter(EventCoordinator events, string nodeId) : INodeExecutionEventWriter
    {
        public ValueTask WriteLogAsync(WorkflowLogLevel level, string message, JsonObject? data = null, CancellationToken cancellationToken = default)
        {
            JsonObject payload = data is null ? [] : (JsonObject)data.DeepClone();
            payload["level"] = level.ToString();
            return events.PublishAsync(RuntimeWorkflowEventKind.NodeLog, message, nodeId, payload, cancellationToken);
        }

        public ValueTask ReportProgressAsync(double? progress, string? message = null, JsonObject? data = null, CancellationToken cancellationToken = default)
        {
            JsonObject payload = data is null ? [] : (JsonObject)data.DeepClone();
            if (progress is not null)
            {
                payload["progress"] = progress.Value;
            }

            return events.PublishAsync(RuntimeWorkflowEventKind.NodeProgress, message ?? "Node progress.", nodeId, payload, cancellationToken);
        }

        public ValueTask EmitOutputAsync(string channel, JsonNode? payload, string? recordKey = null, CancellationToken cancellationToken = default)
        {
            JsonObject data = new()
            {
                ["channel"] = channel,
            };
            if (recordKey is not null)
            {
                data["recordKey"] = recordKey;
            }

            data["payload"] = payload?.DeepClone();
            return events.PublishOutputAsync(nodeId, data, cancellationToken);
        }
    }

    private sealed class EventCoordinator
    {
        private readonly object _gate = new();
        private readonly WorkflowExecutionRequest _request;
        private readonly string _invocationId;
        private readonly IWorkflowClock _clock;
        private long _sequence;
        private long _recordsEmitted;

        public EventCoordinator(WorkflowExecutionRequest request, string invocationId, IWorkflowClock clock)
        {
            _request = request;
            _invocationId = invocationId;
            _clock = clock;
        }

        public async ValueTask PublishOutputAsync(string nodeId, JsonObject data, CancellationToken cancellationToken)
        {
            await PublishAsync(RuntimeWorkflowEventKind.NodeOutput, "Node output.", nodeId, data, cancellationToken).ConfigureAwait(false);
            Interlocked.Increment(ref _recordsEmitted);
        }

        public ValueTask PublishAsync(RuntimeWorkflowEventKind kind, string message, string? nodeId = null, JsonObject? data = null, CancellationToken cancellationToken = default)
        {
            RuntimeWorkflowEvent workflowEvent;
            lock (_gate)
            {
                long sequence = ++_sequence;
                workflowEvent = new RuntimeWorkflowEvent(
                    $"event:{_request.ExecutionId}:{sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                    sequence,
                    _request.ExecutionId,
                    _request.Workflow.Id,
                    _invocationId,
                    null,
                    _clock.UtcNow,
                    kind,
                    message,
                    nodeId,
                    data);
            }

            return _request.EventSink.PublishAsync(workflowEvent, cancellationToken);
        }

        public long RecordsEmitted => Interlocked.Read(ref _recordsEmitted);
    }
}
