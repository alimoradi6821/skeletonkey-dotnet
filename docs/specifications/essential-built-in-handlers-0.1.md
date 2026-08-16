# Essential Built-In Handlers 0.1

Phase 0-13 implements exact handlers for these formally specified built-in nodes:

- `core.start` version 1
- `core.return` version 1
- `flow.if` version 1
- `flow.switch` version 1
- `interaction.request` version 1 when a host supplies an interaction handler

`core.start` succeeds, activates `main`, and produces no arbitrary data.

`core.return` succeeds, activates no outgoing control ports, and supplies terminal outcome metadata for workflow aggregation.

`flow.if` reads an already-materialized boolean `condition` and activates exactly `true` or `false`.

`flow.switch` reads already-materialized `cases` and activates the first case whose `when` value is `true`; otherwise it activates `default`.

`interaction.request` calls the existing host-neutral interaction handler without persistence, durable suspension, or resume. It is available only when supplied explicitly by the host.

The runtime does not implement handlers for `flow.foreach`, `flow.repeat`, `flow.while`, or `workflow.invoke`. Reachable instances fail explicitly as unsupported runtime boundaries.
