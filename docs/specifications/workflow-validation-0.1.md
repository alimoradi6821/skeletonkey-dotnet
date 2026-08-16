# Workflow Semantic Validation 0.1

Successful JSON deserialization does not imply semantic validity.

Semantic validity does not imply that all node types or ports are available at runtime.

The normative JSON Schema is documented in `workflow-json-schema-0.1.md`. Strict JSON parsing, JSON Schema validation, and semantic validation are separate layers. Cross-runtime fixtures for all three layers are documented in `conformance-suite-0.1.md`.

## Lifecycle

Semantic validation runs against a `WorkflowDocument` after parsing or programmatic construction. It performs no I/O, no node catalog lookup, no plugin discovery, and no execution. Catalog-aware node-definition, effective-port, resource-slot, capability, and execution-plan checks are performed by the default analyzer and planner after semantic validation.

`WorkflowSemanticValidator` implements `IWorkflowValidator`:

```csharp
public interface IWorkflowValidator
{
    public WorkflowValidationResult Validate(WorkflowDocument workflow);
}
```

`Validate(null)` throws `ArgumentNullException`. Invalid workflow content is reported as validation issues and does not throw.

## Result Model

`WorkflowValidationResult` contains `Issues`, `Errors`, `Warnings`, and `IsValid`.

`IsValid` is true only when there are no error issues. Warnings do not invalidate workflows. Collections are immutable and never null.

`WorkflowValidationIssue` contains `Code`, `Severity`, `Message`, and `Path`.

## Severities

`Error` means the workflow is semantically invalid.

`Warning` means the workflow has a non-fatal concern. Phase 0-5 warnings cover unreachable enabled nodes and malformed designer metadata.

## JSON Pointer Paths

Issue paths use JSON Pointer syntax. Root-level issues use an empty string.

Dictionary keys are escaped:

- `~` becomes `~0`
- `/` becomes `~1`

Example: input key `user/name` appears in paths as `/inputs/user~1name`.

## Deterministic Issue Ordering

Issues are emitted in this order:

1. Specification and root document
2. Inputs
3. Variables
4. Nodes
5. Connections
6. Graph structure
7. Workflow outputs
8. Invocation and bindings
9. Control flow, iteration, and expressions
10. Execution policies
11. Designer metadata

Node and connection order follows the workflow document. Input and variable order follows dictionary enumeration order. Validation does not sort or mutate the workflow.

## Identifier Rules

Workflow IDs and node IDs must match:

```text
^[A-Za-z][A-Za-z0-9_-]*$
```

Input and variable names must match:

```text
^[A-Za-z_][A-Za-z0-9_-]*$
```

Node types must match:

```text
^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+$
```

Port names must match:

```text
^[A-Za-z][A-Za-z0-9_-]*$
```

Identifiers are not normalized or trimmed.

Stream output channel names must match:

```text
^[a-z][a-z0-9.-]*$
```

## Root Validation

The root document must declare the current schema URI and specification version. Workflow ID and name are required. Non-empty workflow IDs must match the workflow ID pattern.

Description content and length are not validated in this phase.

## Input Validation

Input dictionary keys are the declared input names and must match the input name pattern.

Required inputs must not declare a default value. This includes explicit JSON `null` defaults. In workflow language 0.1, required means the caller must explicitly supply the value.

Optional inputs may declare explicit JSON `null` defaults.

Non-null defaults must match their declared `WorkflowInputType`:

- `String`: JSON string
- `Integer`: JSON integer with no fractional component
- `Number`: finite JSON number
- `Boolean`: JSON boolean
- `Object`: JSON object
- `Array`: JSON array

Values are not coerced.

## Variable Validation

Variable dictionary keys must match the variable name pattern.

Variable values may contain any valid JSON value, including null. Inputs and variables occupy separate namespaces, so identical names are allowed.

## Node Validation

A workflow must declare at least one node.

Each node must have a non-empty valid node ID, a non-empty valid node type, and a `TypeVersion` of at least 1.

Node IDs are case-sensitive. Duplicate node IDs produce an error on every duplicate after the first.

Exactly one `core.start` node must exist, and that start node must not be disabled.

`core.end` is not required in this phase.

Ordinary node parameters are not validated because node definitions and parameter schemas do not exist yet. Reserved language node parameters are validated for invocation, control flow, iteration, expressions, and early return.

## Connection Validation

Connection source and target node IDs are required and must reference existing nodes.

Source and target ports must match the port name pattern.

Duplicate connections are rejected when `from.node`, `from.port`, `to.node`, and `to.port` all match case-sensitively.

`core.start` must not have incoming connections. `core.end` must not have outgoing connections.

The validator does not check port existence, port type compatibility, connection multiplicity, required ports, cycles, self-connections, or branch semantics.

## Reachability Analysis

Reachability starts from the single valid `core.start` node.

Analysis only runs when the validator can safely build a graph: exactly one start node exists, the start node ID is valid, node IDs are unique enough for graph construction, and connection node references are valid.

Every enabled node should be reachable from `core.start`. Unreachable enabled nodes produce warnings. Disabled nodes are excluded from unreachable warnings.

Cycles are allowed.

## Output Validation

Output dictionary keys are the declared output names and must match the input and variable name pattern.

Single and collection outputs require a source endpoint and must not declare a channel.

Stream outputs require a channel and must not declare a source endpoint.

Output source nodes must exist. Output source ports must match the port name pattern. The validator does not check port existence or output value type compatibility because node definitions and port catalogs do not exist yet.

## Invocation and Binding Validation

`workflow.invoke` nodes must declare `typeVersion = 1` and `parameters.workflow`.

Referenced workflow IDs use the workflow ID pattern. Referenced workflow versions, when present, must be exact Semantic Version 2.0 values.

Invocation input names use the input and variable name pattern.

Structured bindings are scanned deterministically in invocation input values. `$literal` content is not scanned.

Input bindings must reference declared workflow inputs. Variable bindings must reference declared workflow variables. Node bindings must reference existing nodes, must not reference the same invocation node, and must use valid port names.

Iteration bindings must reference existing `flow.foreach`, `flow.repeat`, or `flow.while` nodes.

Binding paths must be empty or valid read-only RFC 6901 JSON Pointers.

Stream policy validation checks mode/mapping combinations, mapping channel syntax, and that mapped target channels are declared by stream outputs in the parent workflow.

The validator cannot verify referenced workflow existence, version availability, child required inputs, child input names, child type compatibility, child outputs, child stream source channels, invocation recursion, or invocation depth.

## Control Flow, Iteration, And Expression Validation

Expressions are parsed and inspected for syntax, allowlisted functions, local input references, local variable references, local node references, self-references, and local iteration references. Expressions are not evaluated.

Reserved control nodes must use `typeVersion = 1`. `flow.if`, `flow.switch`, `flow.foreach`, `flow.repeat`, `flow.while`, and `core.return` are validated for closed parameter shapes, condition values, switch case rules, loop policy rules, return outcomes, and reserved graph ports.

The validator does not prove whether a node executes before another node, whether a referenced output is available on every path, whether a node is inside the active loop scope, whether all branches converge, whether loops terminate, whether parallel bodies are safe, or whether future catalog ports exist.

## Execution Policy Validation

Execution policies are declarations only. They do not execute behavior.

Timeout must be a valid duration greater than zero.

Retry `MaxAttempts` must be at least 1.

Retry `Delay`, when supplied, must be a valid duration greater than or equal to zero.

Retry `Backoff` must be finite and greater than or equal to 1.0.

Retry `MaxDelay`, when supplied, must be a valid duration greater than or equal to zero. When both delay and maximum delay are valid, maximum delay must be greater than or equal to delay.

The accepted duration subset includes day and time components such as `PT0S`, `PT0.5S`, `PT30S`, `PT2M`, `PT1H`, `P1D`, and `P1DT2H30M`. Calendar months, calendar years, negative durations, whitespace, invalid syntax, and overflowing values are rejected.

## Designer Validation

Designer metadata has no runtime semantics.

Positions and sizes keyed by unknown node IDs produce warnings.

Position `X` and `Y` must be finite numbers.

Size `Width` and `Height` must be finite and greater than zero.

Designer metadata is not modified, normalized, or removed.

## Catalog-Aware Analysis Boundary

Catalog-aware workflow analysis is a separate layer after semantic validation. Semantic validation still performs no catalog lookup and does not require node packages to be installed.

Future analyzers may use `IWorkflowNodeDefinitionCatalog` to check node availability, node versions, catalog ports, and node resource requirements before execution planning.

## Deferred Rules

Phase 0-9 intentionally does not implement catalog discovery, catalog-aware analysis execution, planning execution, ordinary node parameter schema validation, plugin discovery, graph execution, node handlers, subworkflow invocation runtime, binding evaluation, expression evaluation, branch execution, loop execution, return execution, templates, Playwright, FlaUI, CLI, API, agent, cloud integration, or a visual workflow editor.
## Phase 0-7D Addendum

Validation now checks resource declaration names, resource kind and capability syntax, duplicate capabilities, standard browser constraint shapes, local resource references, invocation resource mappings, locator reference syntax and exact versions, and `interaction.request` static parameter and port rules. It does not resolve resources, locator catalogs, browser engines, human handlers, or child workflow resource compatibility.

## Phase 0-9 Addendum

Node catalog, catalog-aware analysis, and execution planning contracts now exist as explicit layers. `WorkflowSemanticValidator` remains catalog-free and does not perform analysis or planning.
