# Workflow Resources 0.1

Workflow documents may declare root `resources` keyed by resource names matching `^[A-Za-z_][A-Za-z0-9_-]*$`.

Each resource definition contains `kind`, `lifetime`, `access`, `required`, `capabilities`, `constraints`, and `description` in canonical JSON order. Kind and capability IDs use dotted lower-case identifiers such as `web.browser` and `web.persistent-profile`. Capability order is preserved and duplicate capabilities are semantically invalid.

Lifetimes are `execution` and `invocation`. Execution-scoped resources may be shared across a root execution when explicitly mapped. Invocation-scoped resources belong to one workflow invocation and are not inherited by child workflows.

Access modes are `exclusive` and `shared`. The runtime uses planned resource conflicts when selecting parallel handler batches, while concrete providers remain host-supplied.

Node resource slots differ from acquired resource leases. A node definition may declare a slot, the default analyzer matches that slot to a workflow `$resource` wrapper, and the default planner records a planned resource use. A handler invocation acquires an `INodeResourceLease` only through slot-scoped access. The host supplies explicit providers; the runtime creates instances lazily and owns their execution lifetime.

Standard resource kinds are `web.browser`, `web.context`, `web.page`, `desktop.application`, and `interaction.handler`. Standard browser constraints are optional `engine`, `profile`, and `visibility` values. They are requirements or preferences, not Playwright launch options. The closed `desktop.application` constraints and lifecycle rules are defined by [Desktop Automation 0.1](desktop-automation-0.1.md).

Workflow values may contain `{ "$resource": { "name": "browser" } }`. `$literal` prevents interpretation. `workflow.invoke` nodes may map child resource names to parent resources through `parameters.resources`; there is no implicit child resource inheritance.

Generic JSON materialization rejects `$resource` references because live resources are not JSON values. Runtime preparation binds resources through declared slots before handler invocation.

## Durable recovery

An instance may implement `IWorkflowRuntimeResourceCheckpointParticipant` to return immutable, provider-versioned reconstruction state at a safe checkpoint boundary. A recovery-capable provider implements `IWorkflowRuntimeResourceRecoveryProvider`. The runtime persists the resource name and kind outside the provider payload, validates both on resume, and installs reconstructed instances before scheduling remaining steps.

Returning null records an explicit non-resumable resource state. The runtime fails closed with `SKR3008` when an activated resource cannot be reconstructed and uses `SKR3009` when capture or reconstruction throws. Providers must bound and strictly validate their own JSON payloads; they must not serialize live process, browser, or operating-system handles.
