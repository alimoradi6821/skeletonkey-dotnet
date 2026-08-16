# Execution Planning 0.1

Execution planning converts validated and analyzed workflow contracts into a host-neutral execution plan contract.

`IWorkflowExecutionPlanner` accepts a `WorkflowDocument` and `WorkflowAnalysisResult`. `DefaultWorkflowExecutionPlanner` provides the default deterministic implementation.

`WorkflowExecutionPlan` contains a plan ID, workflow identity, workflow specification version, optional catalog identity and version context, ordered plan steps, resource declarations referenced by the plan, explicit dependencies, node-to-step mapping, entry step IDs, and terminal step IDs.

`WorkflowExecutionPlanStep` identifies the workflow node, resolved node definition key, step kind, predecessor dependencies, planned resource uses, potential suspension, terminal behavior, and optional control, invocation, and loop boundary metadata.

Dependencies distinguish control, data, and resource ordering dependencies. A plan is allowed to describe a dependency graph; it is not limited to a flat executable list.

Resource uses declare workflow resource name, node resource slot name, optional accepted or resolved resource kind, required capabilities, required or optional use, and shared or exclusive access mode.

`WorkflowExecutionPlanResult` is either `Ready` with a plan or `Blocked` with deterministic issues.

Planning does not run node handlers, allocate resources, acquire locks, evaluate expressions, evaluate bindings, invoke child workflows, materialize node parameters, perform I/O, launch browsers, or persist state. A plan is not runtime state.

Child workflow loading, handler resolution, resource resolution, and locator resolution remain deferred.

Execution plans describe planned steps and resource uses. Runtime state is represented separately by immutable execution, invocation, and node state snapshots. Planned resource uses are not acquired resource leases, and plan steps are not handler instances.
