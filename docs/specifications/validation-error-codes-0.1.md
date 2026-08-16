# Validation Error Codes 0.1

Phase 0-9 catalog-aware workflow analysis and execution planning issue codes are documented separately from semantic validation errors because those layers are not part of `WorkflowSemanticValidator`.

- `SKA1001` unknown catalog node type
- `SKA1002` unknown catalog node version
- `SKA1003` unknown source port
- `SKA1004` unknown target port
- `SKA1005` missing required resource
- `SKA1006` resource kind mismatch
- `SKA1007` missing resource capability
- `SKP1001` planning blocked by semantic validation errors
- `SKP1002` planning blocked by analysis errors
- `SKP1003` graph ordering unavailable
- `SKP1004` resource scheduling unavailable
- `SKP1005` missing node definition
- `SKP1006` unresolved dynamic port
- `SKP1007` invalid dependency
- `SKP1008` dependency cycle
- `SKP1009` invalid loop structure
- `SKP1010` missing entry step
- `SKP1011` missing terminal path
- `SKP1012` unsatisfied resource requirement
- `SKP1013` unsupported execution characteristic
- `SKP1014` invalid invocation boundary

`SKC1001` through `SKC1014` are reserved for node catalog semantic validation.

Validation codes use the stable `SKWxxxx` format. Codes are pre-release for workflow language 0.1 and must remain stable once the language reaches 1.0.

## Code Groups

- `SKW20xx`: Specification and root document
- `SKW21xx`: Inputs
- `SKW22xx`: Variables
- `SKW23xx`: Nodes
- `SKW24xx`: Connections
- `SKW25xx`: Graph structure
- `SKW26xx`: Execution policies
- `SKW27xx`: Designer metadata
- `SKW28xx`: Workflow outputs
- `SKW29xx`: Workflow invocation and bindings
- `SKW30xx`: Control flow, iteration, and expressions

| Code | Severity | Path pattern | Meaning |
| --- | --- | --- | --- |
| SKW2001 | Error | `/$schema` | Invalid schema URI. |
| SKW2002 | Error | `/specVersion` | Invalid specification version. |
| SKW2003 | Error | `/id` | Workflow ID is required. |
| SKW2004 | Error | `/id` | Invalid workflow ID. |
| SKW2005 | Error | `/name` | Workflow name is required. |
| SKW2101 | Error | `/inputs/{inputName}` | Invalid input name. |
| SKW2102 | Error | `/inputs/{inputName}/default` | Required input declares a default. |
| SKW2103 | Error | `/inputs/{inputName}/default` | Input default type mismatch. |
| SKW2201 | Error | `/variables/{variableName}` | Invalid variable name. |
| SKW2301 | Error | `/nodes` | Workflow has no nodes. |
| SKW2302 | Error | `/nodes/{index}/id` | Node ID is required. |
| SKW2303 | Error | `/nodes/{index}/id` | Invalid node ID. |
| SKW2304 | Error | `/nodes/{duplicateIndex}/id` | Duplicate node ID. |
| SKW2305 | Error | `/nodes/{index}/type` | Node type is required. |
| SKW2306 | Error | `/nodes/{index}/type` | Invalid node type. |
| SKW2307 | Error | `/nodes/{index}/typeVersion` | Invalid node type version. |
| SKW2308 | Error | `/nodes` | Invalid start node count. |
| SKW2309 | Error | `/nodes/{index}/disabled` | Start node is disabled. |
| SKW2401 | Error | `/connections/{index}/from/node` | Source node is required. |
| SKW2402 | Error | `/connections/{index}/to/node` | Target node is required. |
| SKW2403 | Error | `/connections/{index}/from/node` | Connection references unknown source node. |
| SKW2404 | Error | `/connections/{index}/to/node` | Connection references unknown target node. |
| SKW2405 | Error | `/connections/{index}/from/port` | Invalid source port. |
| SKW2406 | Error | `/connections/{index}/to/port` | Invalid target port. |
| SKW2407 | Error | `/connections/{duplicateIndex}` | Duplicate connection. |
| SKW2408 | Error | `/connections/{index}/to/node` | Incoming connection to start node. |
| SKW2409 | Error | `/connections/{index}/from/node` | Outgoing connection from end node. |
| SKW2501 | Warning | `/nodes/{index}` | Unreachable enabled node. |
| SKW2601 | Error | `/nodes/{index}/policy/timeout` | Invalid timeout. |
| SKW2602 | Error | `/nodes/{index}/policy/retry/maxAttempts` | Invalid retry attempt count. |
| SKW2603 | Error | `/nodes/{index}/policy/retry/delay` | Invalid retry delay. |
| SKW2604 | Error | `/nodes/{index}/policy/retry/backoff` | Invalid retry backoff. |
| SKW2605 | Error | `/nodes/{index}/policy/retry/maxDelay` | Invalid retry maximum delay. |
| SKW2606 | Error | `/nodes/{index}/policy/retry/maxDelay` | Maximum delay is less than delay. |
| SKW2701 | Warning | `/designer/positions/{nodeId}` | Designer position references unknown node. |
| SKW2702 | Warning | `/designer/sizes/{nodeId}` | Designer size references unknown node. |
| SKW2703 | Warning | `/designer/positions/{nodeId}/x` or `/designer/positions/{nodeId}/y` | Invalid designer position. |
| SKW2704 | Warning | `/designer/sizes/{nodeId}/width` or `/designer/sizes/{nodeId}/height` | Invalid designer size. |
| SKW2801 | Error | `/outputs/{outputName}` | Invalid workflow output name. |
| SKW2802 | Error | `/outputs/{outputName}/from` | Single or collection output is missing a source endpoint. |
| SKW2803 | Error | `/outputs/{outputName}/channel` | Stream output is missing a channel. |
| SKW2804 | Error | `/outputs/{outputName}` | Output declares properties incompatible with its mode. |
| SKW2805 | Error | `/outputs/{outputName}/from/node` | Output source references an unknown node. |
| SKW2806 | Error | `/outputs/{outputName}/from/port` | Invalid output source port. |
| SKW2807 | Error | `/outputs/{outputName}/channel` | Invalid output channel name. |
| SKW2901 | Error | `/nodes/{index}/parameters/workflow` | Missing invocation workflow reference. |
| SKW2902 | Error | `/nodes/{index}/parameters/workflow/id` | Invalid referenced workflow ID. |
| SKW2903 | Error | `/nodes/{index}/parameters/workflow/version` | Invalid referenced workflow version. |
| SKW2904 | Error | `/nodes/{index}/parameters/inputs/{inputName}` | Invalid invocation input name. |
| SKW2905 | Error | `.../$binding` | Malformed binding wrapper. |
| SKW2906 | Error | `.../$binding/source` | Unknown binding source. |
| SKW2907 | Error | `.../$binding/name` | Unknown workflow input binding. |
| SKW2908 | Error | `.../$binding/name` | Unknown workflow variable binding. |
| SKW2909 | Error | `.../$binding/node` | Unknown node binding. |
| SKW2910 | Error | `.../$binding/node` | Self-referencing node binding. |
| SKW2911 | Error | `.../$binding/port` | Invalid node binding port. |
| SKW2912 | Error | `.../$binding/path` | Invalid binding JSON Pointer. |
| SKW2913 | Error | `.../$binding` | Invalid missing-value configuration. |
| SKW2914 | Error | `.../$literal` | Invalid literal wrapper. |
| SKW2915 | Error | `/nodes/{index}/parameters/streams` | Invalid invocation stream policy. |
| SKW2916 | Error | `/nodes/{index}/parameters/streams/mappings/{sourceChannel}` | Invalid invocation stream channel. |
| SKW2917 | Error | `/nodes/{index}/parameters/streams/mappings/{sourceChannel}` | Undeclared parent stream channel. |
| SKW2918 | Error | `/nodes/{index}/typeVersion` | Unsupported workflow.invoke node version. |
| SKW3001 | Error | `.../$expression` | Malformed expression wrapper. |
| SKW3002 | Error | `.../$expression` | Expression syntax error. |
| SKW3003 | Error | `.../$expression` | Unknown expression input. |
| SKW3004 | Error | `.../$expression` | Unknown expression variable. |
| SKW3005 | Error | `.../$expression` | Unknown expression node. |
| SKW3006 | Error | `.../$expression` | Self-referencing expression node. |
| SKW3007 | Error | `.../iteration` or `.../$expression` | Unknown iteration reference. |
| SKW3008 | Error | `.../$binding` | Invalid iteration binding shape. |
| SKW3009 | Error | `.../$expression` | Unknown expression function. |
| SKW3010 | Error | `/nodes/{index}/typeVersion` | Unsupported control node version. |
| SKW3011 | Error | `/nodes/{index}/parameters` | Invalid control-node parameter shape. |
| SKW3012 | Error | `/nodes/{index}/parameters/condition` | Invalid condition value. |
| SKW3013 | Error | `/nodes/{index}/parameters/cases` | Missing switch cases. |
| SKW3014 | Error | `/nodes/{index}/parameters/cases/{caseIndex}/id` | Invalid switch case ID. |
| SKW3015 | Error | `/nodes/{index}/parameters/cases/{caseIndex}/id` | Duplicate switch case ID. |
| SKW3016 | Error | `/nodes/{index}/parameters/execution` | Invalid foreach execution policy. |
| SKW3017 | Error | `/nodes/{index}/parameters/count` | Invalid repeat count. |
| SKW3018 | Error | `/nodes/{index}/parameters/maxIterations` | Invalid while iteration limit. |
| SKW3019 | Error | `/connections/{index}/from/port` or `/connections/{index}/to/port` | Invalid loop control port. |
| SKW3020 | Error | `/connections/{index}/from/port` | Invalid conditional output port. |
| SKW3021 | Error | `/nodes/{index}/parameters/outcome` | Invalid return outcome. |
| SKW3022 | Error | `/connections/{index}/from/node` | Outgoing connection from return. |
| SKW3023 | Error | `/connections/{index}/to/port` | Invalid reserved control input port. |
## Phase 0-7D Addendum

`SKW3101` through `SKW3122` are reserved for resources, locator references, and human-interaction workflow validation. `SKL1001` through `SKL1011` are reserved for locator document semantic validation.
