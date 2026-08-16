# Runtime State Transitions 0.1

Runtime state snapshots use the existing execution lifecycle contract.

Legal transitions are:

- `Created` to `Ready`
- `Ready` to `Running`
- `Running` to `Suspended`
- `Suspended` to `Running`
- `Running` to `Cancelling`
- `Ready` to `Cancelling`
- `Suspended` to `Cancelling`
- `Running` to `Completed`
- `Cancelling` to `Completed`

Invalid transitions are rejected, including `Completed` to `Running`, `Created` to `Completed`, and `Ready` to `Suspended`.

The in-memory state store increments revisions, preserves creation timestamps, records runtime-supplied update timestamps, and returns immutable snapshots. It is thread-safe and intentionally non-persistent.
