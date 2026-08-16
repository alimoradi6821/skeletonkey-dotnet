# 0009 Subworkflow Invocation and Explicit Data Binding

Phase 0-7B defines first-class subworkflow invocation and structured data-binding contracts.

## Principles

The primary goal is a complete, stable, professional automation library.

Legacy behavior informs requirements, but legacy syntax does not define the language.

Every complete platform operation should be representable as a workflow.

AI systems are optional consumers of the public language and documentation, not design authorities for the runtime.

## Decision

SkeletonKey reserves `workflow.invoke` with `typeVersion = 1` as the normative invocation node type. The node has one fixed data output port named `result`. Child final outputs remain inside the workflow execution result object exposed through that port.

This avoids dynamic port generation, output-name collisions, hidden catalog mutation, and runtime-specific shortcuts. A future visual catalog can add conveniences, but the language contract remains stable.

## Data Binding

Invocation inputs use structured workflow values. Ordinary JSON is literal by default. Bindings are explicit with `$binding`, and application data that must contain reserved wrapper names uses `$literal`.

Bindings use RFC 6901 JSON Pointer paths because the syntax is small, deterministic, standard, and unambiguous. Dot paths, JSONPath, implicit trimming, expression execution, and type coercion are intentionally excluded.

## Outcomes and Identity

Child technical status and child business outcome remain separate. Child outcomes do not automatically propagate to parent outcomes because orchestration logic must decide how to interpret them.

Root execution identity and workflow invocation identity are separate. `ExecutionId` identifies the full root execution. `InvocationId` identifies one workflow invocation. `ParentInvocationId` links child invocations to their caller.

## Streams

Stream forwarding is explicit. `workflow.invoke` can declare `forward`, `suppress`, or `map`. This phase does not implement event delivery or stream forwarding behavior.

## Deferred Work

Cross-workflow resolution, referenced workflow existence checks, child input compatibility, child output names, child stream source channels, recursion analysis, invocation execution, binding evaluation, expression evaluation, and runtime security policy are deferred.

Legacy scenario syntax, legacy mutable context behavior, and legacy template syntax are not supported.
