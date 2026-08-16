# Workflow Value Resolution Context 0.1

`WorkflowValueResolutionContext` is the immutable data source for binding resolution and expression evaluation.

It contains:

- workflow inputs
- workflow variables
- prior node outputs
- active iteration contexts

Inputs and variables are keyed by ordinal, case-sensitive names and defensively clone JSON values. Node outputs are keyed by node ID and use `NodePortValueMap`. Iterations are keyed by explicit loop node ID.

Expression roots are projected as:

- `inputs`
- `variables`
- `nodes`
- `iterations`

Node outputs are projected as `nodes['node-id'].outputs['port-id']`.

`NodePortValueSet` projection:

- zero values: missing property
- one value: that JSON value, including explicit JSON null
- multiple values: ordered JSON array

Iteration projection includes `index` and `number`; includes `item` only when present; includes `count` only when known.

The context exposes no host services, resource instances, locator instances, secret store, clock, randomness, mutable workflow state, mutable runtime state, filesystem, network, or browser objects.
