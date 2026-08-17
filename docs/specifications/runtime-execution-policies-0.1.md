# Runtime Execution Policies 0.1

Phase 0-18 makes workflow-declared `timeout`, `retry`, and `onError` policies executable for exact `INodeHandler` invocations. Policy execution is owned by the runtime; handlers neither sleep nor create retry identities.

## Attempt Semantics

`retry.maxAttempts` is the total attempt count, including the first attempt. Every attempt receives a distinct one-based `NodeExecutionIdentity.Attempt` and produces its own terminal `NodeExecutionResult` and lifecycle snapshot. A retry does not consume another runtime activation; it does consume the executed-attempt limit.

Expected handler failures, unexpected handler exceptions, and handler timeouts are retryable. Parameter materialization failures, missing or mismatched handlers, resource or locator preparation failures, and invalid handler outputs are deterministic contract failures and are not retried. Cancellation is never converted to failure, retried, or consumed by `onError`.

## Timeout

`timeout` is an ISO-8601 duration validated before execution. It bounds the handler call only. When it expires, the runtime cancels the attempt token and records `SKR1023`. A handler that ignores cancellation cannot block the scheduler, but the host remains responsible for isolating non-cooperative external work.

## Retry Delay

When `delay` is omitted, retry is immediate. Otherwise the zero-based retry delay is:

`min(delay * backoff^(completedAttempt - 1), maxDelay)`

The maximum-duration and date-range bounds are applied before waiting. `IWorkflowRuntimeDelay` lets hosts supply a test clock or specialized scheduler without changing policy calculation. Events `NodeRetryScheduled` and `NodeRetryStarted` expose the completed and next attempt numbers.

## On-Error Behavior

`onError` runs after attempts are exhausted or after a non-retryable failure:

- `fail` preserves the original error and fails the workflow.
- `continue` retains the failed node attempt result, clears the workflow terminal error, and activates the conventional `next` control output when that output exists.
- `stop` terminates the workflow with `SKR1024`; the original error code remains in the `NodeExecutionStopped` event payload.

## Checkpoint Boundary

Checkpoint format 0.2 persists `RetryAttempt` and `RetryNotBeforeUtc`. For a top-level handler node, the runtime saves a `Ready` checkpoint after a failed attempt and before waiting or starting the next attempt. Resume therefore starts the next identity and never silently repeats the persisted failed attempt. Format 0.1 remains readable and migrates through default retry metadata.

Repeated loop-body handlers honor timeout, retry, backoff, and on-error behavior. Their retries remain inside the enclosing loop activation; because the loop step is already `Running`, a process interruption still requires explicit loop recovery under `SKR3006`.

Loop orchestration, child-workflow invocation orchestration, and suspended in-memory interaction continuations are runtime boundaries rather than exact handler calls and do not gain automatic retries in this version. Parallel, distributed, compensation, jitter, and durable resource-handle recovery remain out of scope.
