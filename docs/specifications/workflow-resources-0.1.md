# Workflow Resources 0.1

Workflow documents may declare root `resources` keyed by resource names matching `^[A-Za-z_][A-Za-z0-9_-]*$`.

Each resource definition contains `kind`, `lifetime`, `access`, `required`, `capabilities`, `constraints`, and `description` in canonical JSON order. Kind and capability IDs use dotted lower-case identifiers such as `web.browser` and `web.persistent-profile`. Capability order is preserved and duplicate capabilities are semantically invalid.

Lifetimes are `execution` and `invocation`. Execution-scoped resources may be shared across a root execution when explicitly mapped. Invocation-scoped resources belong to one workflow invocation and are not inherited by child workflows.

Access modes are `exclusive` and `shared`. They are declarative contracts for future hosts; no locking, scheduling, or resource creation is implemented.

Node resource slots differ from acquired resource leases. A node definition may declare a slot, the default analyzer may match that slot to a workflow `$resource` wrapper, and the default planner may record a planned resource use. A future handler invocation may acquire an `INodeResourceLease` only through slot-scoped access. These contracts do not perform resource resolution, creation, locking, pooling, or persistence.

Standard resource kinds are `web.browser`, `web.context`, `web.page`, and `interaction.handler`. Standard browser constraints are optional `engine`, `profile`, and `visibility` values. They are requirements or preferences, not Playwright launch options.

Workflow values may contain `{ "$resource": { "name": "browser" } }`. `$literal` prevents interpretation. `workflow.invoke` nodes may map child resource names to parent resources through `parameters.resources`; there is no implicit child resource inheritance.

Generic JSON materialization rejects `$resource` references because live resources are not JSON values. Future runtime preparation must bind resources through declared slots before handler invocation.
