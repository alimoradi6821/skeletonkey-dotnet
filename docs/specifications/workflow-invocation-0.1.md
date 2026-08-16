# Workflow Invocation 0.1

Workflow invocation contracts reserve the built-in node type `workflow.invoke` with `typeVersion = 1`.

This phase defines declarations only. It does not load, resolve, execute, or validate referenced workflow documents.

## Workflow Reference

`parameters.workflow` is required:

```json
{
  "id": "check-account",
  "version": "1.0.0"
}
```

`id` uses the workflow ID syntax. `version`, when present, must be an exact Semantic Version 2.0 value. Version ranges, `latest`, file paths, package IDs, registry URLs, and remote locations are not part of this contract.

Published production workflow packages should normally pin exact referenced versions for reproducibility.

## Parameters

Canonical parameter order is:

1. `workflow`
2. `inputs`
3. `streams`

`inputs` defaults to `{}`. `streams.mode` defaults to `forward`.

## Inputs

Invocation inputs are workflow values. They can contain literal JSON, nested arrays, nested objects, `$binding` wrappers, `$expression` wrappers, and `$literal` wrappers.

The current semantic validator checks local binding references and expression references. It does not evaluate inputs or validate child input names, child required inputs, or child input types.

## Fixed Result Port

The future invocation node exposes one fixed data output port:

```text
result
```

The value is the existing workflow execution result contract. Child final outputs remain under the result object's `outputs` member. Dynamic child output ports are not part of the normative contract.

## Status and Outcome

A child technical failure is distinct from a child business outcome. A child workflow can technically succeed while returning a business outcome such as `RequiresAction` or `Skipped`.

Parent workflows decide how to handle child status and outcome. Outcomes do not automatically propagate.

## Streams

Invocation stream policy modes are:

- `forward`: child streams remain visible under original channel names.
- `suppress`: child streams are not forwarded beyond the invocation boundary.
- `map`: child stream channels are renamed into parent stream channels.

Mapped target channels must be declared by stream outputs in the parent workflow. Child source channel existence is deferred until referenced workflows can be resolved.

## Identity

`ExecutionId` identifies the complete root execution. Root and child invocations share it.

`InvocationId` identifies one workflow invocation.

`ParentInvocationId` links a child invocation to the invocation that called it. Root invocations have no parent.

## Recursion Boundary

Direct or indirect recursion is not rejected only because a workflow references itself. No recursion executes in this phase. Future runtimes must enforce bounded invocation depth and may add dependency analysis or security policy.

## Deferred Validation

Single-document validation cannot verify referenced workflow existence, version availability, child required inputs, unknown child input names, child type compatibility, child output names, child stream source channels, recursion cycles, maximum invocation depth, or runtime capabilities.
## Phase 0-7D Addendum

`workflow.invoke` parameters may include `resources`, a closed mapping from child workflow resource names to parent `$resource` wrappers. Mapping is explicit; child workflows do not inherit parent resources automatically and compatibility is deferred.
