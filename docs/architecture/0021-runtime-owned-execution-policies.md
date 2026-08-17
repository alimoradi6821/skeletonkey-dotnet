# Phase 0-18 Runtime-Owned Execution Policies

Status: implemented; repository verification required before release tagging.

The default runtime now executes validated node timeout, retry, exponential-backoff, maximum-delay, and on-error declarations around exact handler calls. Each retry receives a new runtime-owned node identity and terminal result. The host-neutral `IWorkflowRuntimeDelay` boundary makes waiting deterministic in tests while leaving backoff calculation in the runtime.

Timeout uses a linked per-attempt cancellation token and produces stable code `SKR1023`. Expected handler failures, unexpected exceptions, and timeouts are retryable. Deterministic preparation and contract failures are not. Root cancellation always wins and is never retried.

After attempts are exhausted, `fail` preserves the original error, `continue` consumes the terminal error and activates the conventional `next` control edge, and `stop` terminates with `SKR1024` while retaining the original code in the runtime event stream.

Checkpoint format 0.2 adds the completed retry count and UTC not-before timestamp. The default runtime persists the failed result and retry schedule before waiting or creating the next top-level identity. This preserves Phase 0-17's at-most-once rule across process restart. Legacy 0.1 checkpoints remain accepted with zero-valued retry metadata.

The design deliberately excludes automatic retry of runtime-owned loop orchestration, child invocation orchestration, and suspended interaction boundaries. Loop-body handler attempts do honor policies, but the enclosing running-loop checkpoint still requires explicit recovery after interruption. Parallel scheduling, jitter, compensation, distributed leases, database stores, and live resource recovery remain separate work.
