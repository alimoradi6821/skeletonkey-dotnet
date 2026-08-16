# Execution Results and Events 0.1

This document describes host-neutral execution contracts added in Phase 0-7A.

No execution engine is implemented by these contracts.

## Workflow Execution Status

`WorkflowExecutionStatus` describes technical execution state, such as queued, running, succeeded, failed, canceled, or timed out.

Technical status is separate from business outcome. A workflow may technically succeed while its business outcome is rejected or requires action.

## Business Outcome

`WorkflowOutcome` describes optional business meaning:

- `Success`
- `Partial`
- `RequiresAction`
- `NoResults`
- `Skipped`

The outcome contains a stable code, optional message, and optional JSON data.

## Final Workflow Result

`WorkflowExecutionResult` contains:

- execution ID
- invocation ID
- optional parent invocation ID
- workflow ID
- technical status
- optional business outcome
- final output dictionary
- metrics
- optional technical error

Final outputs contain `single` and `collection` workflow outputs. Streamed records are delivered through events and are not required to appear in final outputs.

## Node Result

`NodeExecutionResult` contains:

- execution ID
- workflow ID
- invocation ID
- node ID
- node type
- node technical status
- attempt number
- output port dictionary
- optional technical error

## Events

`WorkflowEvent` is the base event contract. Defined event contracts are:

- `WorkflowOutputEvent` for streamed output records.
- `WorkflowLogEvent` for host-neutral log records.
- `WorkflowProgressEvent` for progress snapshots.

`IWorkflowEventSink` accepts events asynchronously. It does not define queueing, persistence, retries, streaming protocols, logging providers, or dispatch implementation.

Node handlers do not construct full workflow events. `INodeExecutionEventWriter` is the handler-facing observation boundary; a future runtime owns event IDs, sequence numbers, timestamps, execution identity enrichment, redaction, and dispatch.

Runtime lifecycle-state contracts and immutable execution snapshots are separate from final execution results. Snapshots describe point-in-time runtime state; result contracts describe terminal technical outcomes.

## Invocation Identity

`ExecutionId` identifies the complete root execution. Root and child workflow invocations share it.

`InvocationId` identifies the specific workflow invocation that produced a result, node result, or event.

`ParentInvocationId` is null for the root invocation and set for child workflow invocations.

## JSON Payload Immutability

JSON payloads and JSON dictionaries are defensively cloned on input and when returned. Callers cannot mutate internal contract state through `JsonNode` references.

## Deferred Work

Phase 0-7A and later contract phases intentionally defer execution engines, graph traversal, node handler implementations, event dispatch, transports, subworkflow invocation, expressions, data binding, resource resolution, node catalog discovery, Playwright, FlaUI, CLI, API, cloud, backend, and editor integrations.
## Phase 0-7D Addendum

Human-interaction request and response contracts are host-neutral and separate from execution result/event delivery. Secret interaction values must be redacted by future hosts and must not be included in ordinary log events.
