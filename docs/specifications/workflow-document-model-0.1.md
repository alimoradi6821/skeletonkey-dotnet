# Workflow Document Model 0.1

This document describes the in-memory workflow document model introduced in Phase 0-3 and extended through Phase 0-7C. The official JSON representation is documented separately in `workflow-json-format-0.1.md`, the normative JSON Schema is documented in `workflow-json-schema-0.1.md`, semantic validation is documented in `workflow-validation-0.1.md`, expressions are documented in `workflow-expressions-0.1.md`, control flow is documented in `workflow-control-flow-0.1.md`, iteration is documented in `workflow-iteration-0.1.md`, and reusable language fixtures are documented in `conformance-suite-0.1.md`.

## Root workflow

`WorkflowDocument` represents a graph-based workflow. It contains:

- `Schema`: schema URI declaration, defaulting to `WorkflowSpecification.CurrentSchemaUri`.
- `SpecVersion`: language version declaration, defaulting to `WorkflowSpecification.CurrentVersion`.
- `Id`: workflow identifier.
- `Name`: workflow display name.
- `Description`: optional human-readable description.
- `Inputs`: input definitions keyed by input name.
- `Variables`: initial JSON variable values keyed by variable name.
- `Nodes`: graph node declarations.
- `Connections`: directed graph connections.
- `Outputs`: workflow output declarations keyed by output name.
- `Designer`: optional designer-only metadata.

## Inputs

`WorkflowInputDefinition` declares an input type, required flag, optional JSON default value, and optional description. Initial input types are `String`, `Integer`, `Number`, `Boolean`, `Object`, and `Array`.

Input defaults are represented by the model. Semantic default validation is handled by the separate validation layer.

## Variables

Variables are arbitrary JSON values represented with `JsonNode?`. They are declarations, not runtime variable storage.

## Nodes

`WorkflowNode` declares a node instance with an ID, namespace-style type, type version, optional display name, optional description, disabled flag, parameters, and optional execution policy declaration.

A node is document data, not an execution record. It has no status, result, timing, error, or attempt fields.

## Node parameters

Node parameters are held as `JsonObject` so future node types can define their own schemas without changing the root model.

Reserved language node types now include `workflow.invoke`, `flow.if`, `flow.switch`, `flow.foreach`, `flow.repeat`, `flow.while`, and `core.return`. Control flow is represented by graph nodes, ports, and connections, not nested action arrays.

## Connections and endpoints

`WorkflowConnection` connects a source `WorkflowEndpoint` to a target `WorkflowEndpoint`. An endpoint contains a node ID and port name. Endpoint direction is implied by whether it appears as `From` or `To`.

## Outputs

`WorkflowOutputDefinition` declares values or streams a workflow exposes to a host.

Output modes are:

- `Single`: one final value sourced from a node endpoint.
- `Collection`: one final collection sourced from a node endpoint.
- `Stream`: records emitted on a named channel.

Single and collection outputs use `From`. Stream outputs use `Channel`. Optional descriptions are informational. Constructors preserve declarations and leave invalid combinations to semantic validation.

## Invocation and binding contracts

`WorkflowReference` identifies a referenced workflow by ID and optional exact Semantic Version 2.0 version.

`WorkflowBinding` declares an explicit local data binding from a workflow input, workflow variable, or node output port. It stores a read-only JSON Pointer path, missing-value behavior, and optional default JSON. Default JSON is defensively cloned.

`WorkflowBinding` also supports explicit iteration-context bindings from `flow.foreach`, `flow.repeat`, and `flow.while` node IDs.

`WorkflowInvocationStreamPolicy` declares `Forward`, `Suppress`, or `Map` stream behavior for future subworkflow invocations. The contract is immutable and does not implement forwarding.

Control-flow helper contracts describe foreach execution policy, switch cases, return outcomes, and host-neutral iteration context data. They do not implement scheduling, branching, looping, or return execution.

## Execution policy declarations

`WorkflowExecutionPolicy` declares optional future runtime preferences:

- `Timeout`: ISO-8601 duration text.
- `OnError`: `Fail`, `Continue`, or `Stop`.
- `Retry`: optional retry declaration.

No execution behavior is implemented in this phase. Execution policy declarations are semantically checked by the validation layer where possible.

## Retry declarations

`WorkflowRetryPolicy` declares maximum attempts, optional delay, backoff multiplier, and optional maximum delay. Defaults are one attempt and a backoff of one.

## Designer metadata

`WorkflowDesignerMetadata` contains positions and sizes keyed by node ID. A node can have a position without a size, and a size without a position.

Designer metadata has no runtime meaning.

## Default values

Omitted collections become empty read-only collections, including outputs. Omitted node parameters become an empty JSON object. Omitted execution policy `OnError` defaults to `Fail`.

## Immutability guarantees

Constructors defensively copy incoming collections. JSON-backed values are deep cloned on input and when returned from public properties. Caller mutation of source collections, source JSON values, or returned JSON copies does not mutate an already constructed model.

## Validation boundary

Constructors do not validate node ID syntax, node type syntax, type versions, required start nodes, connection targets, graph reachability, cycles, port compatibility, input defaults, JSON serialization shape, or node parameter schemas. JSON structure is described by the normative schema, and semantic workflow validation is performed by `SkeletonKey.Validation`.

## C# construction example

```csharp
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Outputs;
using SkeletonKey.Workflow.Specification;

var workflow = new WorkflowDocument(
    schema: WorkflowSpecification.CurrentSchemaUri,
    specVersion: WorkflowSpecification.CurrentVersion,
    id: "minimal",
    name: "Minimal workflow",
    nodes:
    [
        new WorkflowNode(
            id: "start",
            type: "core.start",
            typeVersion: 1),
        new WorkflowNode(
            id: "end",
            type: "core.end",
            typeVersion: 1),
    ],
    connections:
    [
        new WorkflowConnection(
            new WorkflowEndpoint("start", "main"),
            new WorkflowEndpoint("end", "main")),
    ],
    outputs:
    [
        new KeyValuePair<string, WorkflowOutputDefinition>(
            "result",
            new WorkflowOutputDefinition(
                WorkflowOutputMode.Single,
                new WorkflowEndpoint("end", "main"))),
    ]);
```

## JSON example

```json
{
  "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
  "specVersion": "0.1.0",
  "id": "minimal",
  "name": "Minimal workflow",
  "nodes": [
    {
      "id": "start",
      "type": "core.start",
      "typeVersion": 1,
      "parameters": {}
    },
    {
      "id": "end",
      "type": "core.end",
      "typeVersion": 1,
      "parameters": {}
    }
  ],
  "connections": [
    {
      "from": { "node": "start", "port": "main" },
      "to": { "node": "end", "port": "main" }
    }
  ],
  "outputs": {
    "result": {
      "mode": "single",
      "from": { "node": "end", "port": "main" }
    }
  }
}
```

## Phase 0-7D Addendum

Workflow root canonical order is `$schema`, `specVersion`, `id`, `name`, `description`, `inputs`, `variables`, `resources`, `nodes`, `connections`, `outputs`, and `designer`. Omitted `resources` deserialize as an empty immutable dictionary. Resource declarations are provider-neutral requirements, not live runtime objects.
