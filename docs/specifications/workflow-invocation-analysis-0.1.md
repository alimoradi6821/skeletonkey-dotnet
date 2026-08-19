# Workflow Invocation Analysis 0.1

## Scope

`WorkflowInvocationGraphAnalyzer` validates the complete reachable invocation dependency graph rooted at one `WorkflowDocument`. Resolution is explicit through `IWorkflowRepository`; the analyzer performs no filesystem, network, registry, package, or assembly discovery.

## Reachability and order

Analysis begins at every enabled `core.start` node and follows declared graph connections. Only enabled, reachable `workflow.invoke` nodes are analyzed. Nodes and dependencies are processed in document order, producing deterministic dependency and issue collections.

## Repository resolution

An unversioned reference resolves only its workflow ID. A versioned reference resolves only `id@version`; it never falls back to an unversioned document. Repository cancellation propagates. Other repository failures produce `SKD1009` without exposing exception details.

The resolved document ID must exactly equal the requested reference ID.

## Child input compatibility

For every resolved child:

- each required input without a default must be supplied;
- each supplied input name must be declared by the child;
- literal and `$literal` values must match the child's declared JSON type;
- `$binding` and `$expression` values defer type checking until materialization.

Integer values must be finite integral JSON numbers. Number values must be finite. JSON null is incompatible with every declared input type in this version.

## Stream compatibility

For `streams.mode: map`, every mapping source key must name a stream channel declared by a child workflow stream output. Parent target channels remain part of single-document semantic validation.

## Cycles and limits

Direct and indirect invocation recursion is invalid. Dependency depth starts at one for a root workflow's child and must not exceed `WorkflowInvocationAnalysisOptions.MaximumDepth`.

## Stable issue codes

| Code | Meaning |
|---|---|
| `SKD1001` | Referenced workflow not found |
| `SKD1002` | Resolved workflow identity mismatch |
| `SKD1003` | Direct or indirect invocation cycle |
| `SKD1004` | Maximum dependency depth exceeded |
| `SKD1005` | Required child input missing |
| `SKD1006` | Unknown child input supplied |
| `SKD1007` | Static child input type mismatch |
| `SKD1008` | Unknown mapped child stream channel |
| `SKD1009` | Workflow repository failure |

## Deferred work

This analysis does not validate child output value types, resource mapping compatibility, runtime provider capabilities, remote availability, version ranges, signatures, trust policy, or distributed placement.
