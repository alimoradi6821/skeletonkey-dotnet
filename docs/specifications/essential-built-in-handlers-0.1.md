# Essential Built-In Handlers 0.1

The built-in runtime provides exact handlers for these formally specified nodes:

- `core.start` version 1
- `core.end` version 1
- `core.return` version 1
- `flow.if` version 1
- `flow.switch` version 1
- `flow.foreach` version 1
- `flow.repeat` version 1
- `flow.while` version 1
- `interaction.request` version 1 when a host supplies an interaction handler

`core.start` succeeds, activates `main`, and produces no arbitrary data.

`core.end` succeeds without control or data outputs. Its catalog definition is terminal, so successful execution closes the active workflow path without requiring synthetic outcome metadata.

`core.return` succeeds, activates no outgoing control ports, and supplies terminal outcome metadata for workflow aggregation.

`flow.if` reads an already-materialized boolean `condition` and activates exactly `true` or `false`.

`flow.switch` reads already-materialized `cases` and activates the first case whose `when` value is `true`; otherwise it activates `default`.

`flow.foreach`, `flow.repeat`, and `flow.while` execute through bounded runtime-owned iteration semantics and exact built-in boundary handlers.

`interaction.request` calls the host-neutral interaction boundary when supplied explicitly by the host. Durable human-interaction continuation remains a host/runtime concern rather than an implicit handler behavior.

`workflow.invoke` intentionally has no ordinary node handler. Invocation is runtime-owned because child workflow identity, dependency validation, resource mapping, stream forwarding, checkpointing, and terminal propagation span more than a single handler call.
