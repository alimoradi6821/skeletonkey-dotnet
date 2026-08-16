# Workflow Control Flow 0.1

Control flow is graph-native. Control nodes choose graph paths through explicit ports; they do not contain nested action arrays.

## flow.if

`flow.if` requires `condition`. The condition may be a boolean literal, `$binding`, or `$expression`.

Input port: `main`.

Output ports: `true`, `false`.

## flow.switch

`flow.switch` requires ordered `cases`. Each case has `id`, `when`, and optional `description`.

Case IDs match `^[A-Za-z][A-Za-z0-9_-]*$`, are case-sensitive, must be unique, and cannot be `default`.

Input port: `main`.

Output ports: one port per case ID plus `default`.

Cases are declared in future runtime evaluation order. Dynamic port generation is deferred to the node catalog.

## core.return

`core.return` requires `outcome` with `kind` and `code`. Optional `message` may be a string, binding, or expression. Optional `data` is workflow data.

Outcome kinds are `success`, `partial`, `requires-action`, `no-results`, and `skipped`.

`core.return` accepts input port `main` and must not have outgoing connections.

## Validation Boundaries

Single-document semantic validation checks reserved control-node versions, parameter shapes, switch case rules, static condition shapes, reserved ports, and return terminal behavior.

It does not prove execution order, branch reachability, branch convergence, output availability on every path, or node catalog port existence.

## Deferred Runtime Behavior

The default execution planner records branch and return boundary metadata. It does not choose branches, evaluate conditions, execute returns, schedule runtime work, or traverse a graph.
## Phase 0-7D Addendum

Control-flow contracts are unchanged by resource, locator, and interaction declarations. These declarations remain workflow-value contracts and reserved node contracts; no graph execution, branch execution, or loop execution is introduced.
