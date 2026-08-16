# Execution Lifecycle State 0.1

`ExecutionLifecycleState` describes the lifecycle of a workflow execution, workflow invocation, or node execution attempt before or alongside final technical status.

Values:

- `Created`: the scope exists but has not been prepared.
- `Ready`: the scope has passed preparation and may begin.
- `Running`: the scope is actively executing or awaiting normal asynchronous work.
- `Suspended`: the scope is intentionally waiting for an external continuation, such as future durable human interaction or checkpoint-based continuation.
- `Cancelling`: cancellation has been requested and cleanup may still be running.
- `Completed`: the lifecycle is terminal.

Lifecycle state does not replace terminal result status. Existing `WorkflowExecutionStatus`, `NodeExecutionStatus`, and `NodeHandlerCompletionStatus` represent technical completion as succeeded, failed, or cancelled.

`WorkflowExecutionStateSnapshot`, `WorkflowInvocationStateSnapshot`, and `NodeExecutionStateSnapshot` expose immutable point-in-time state. Revisions and timestamps are supplied by a future runtime. These contracts do not access clocks, mutate state, persist state, validate transitions, dispatch observations, or execute workflows.

`ExecutionStateTransition` is an immutable observation contract for state transitions. It records scope, identity, previous state, current state, revision, timestamp, and optional reason text. It does not perform transition mutation, validation, storage, or dispatch.
