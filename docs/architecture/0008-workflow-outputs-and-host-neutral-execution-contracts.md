# 0008 Workflow Outputs and Host-Neutral Execution Contracts

Phase 0-7A adds workflow output declarations and host-neutral execution result and event contracts.

## Decision

Workflow documents may declare `outputs` without changing the workflow language version, which remains `0.1.0`.

Outputs are declarative:

- `single` and `collection` outputs identify a source endpoint for final workflow results.
- `stream` outputs identify a channel for event-delivered records.

Execution contracts live in `SkeletonKey.Abstractions` and are deliberately host-neutral. They do not depend on ASP.NET, Microsoft.Extensions.Logging, HTTP, WebSockets, Playwright, FlaUI, plugin hosts, or node catalogs.

## Result Contracts

`WorkflowExecutionResult` represents the final technical workflow result. Its `Status` is separate from optional business `Outcome`.

`NodeExecutionResult` represents a node-level technical result. Output dictionaries use node output port names.

Final output dictionaries contain single and collection workflow outputs. Streamed records are carried by `WorkflowOutputEvent` and are not required to be duplicated in final outputs.

## Event Contracts

`WorkflowEvent` is the base event type. Phase 0-7A defines:

- `WorkflowOutputEvent`
- `WorkflowLogEvent`
- `WorkflowProgressEvent`

`IWorkflowEventSink` is an async event sink abstraction only. It does not dispatch events or prescribe transport behavior.

## Immutability

Contract types defensively clone JSON payloads on input and when returned from public properties. Collections are copied into read-only dictionaries.

## Deferred Work

Phase 0-7A intentionally does not implement workflow execution, node handlers, graph traversal, data binding, expressions, subworkflow invocation, event dispatch, transports, node catalogs, or host integrations.
