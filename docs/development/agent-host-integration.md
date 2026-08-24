# Agent Host Integration 0.1

SkeletonKey is intended to run inside or beside a Windows Agent. The central server selects work; the Agent owns local execution, process lifetime, durable checkpoints, and the installed runtime slot.

## Task contract

A host task consumed by `build/invoke-agent-task.ps1` has this shape:

```json
{
  "formatVersion": "0.1",
  "taskId": "server-task-id",
  "executionId": "durable-execution-id",
  "operation": "run",
  "requiredVersion": "0.1.0",
  "workflow": "workflows/canary-core.workflow.json",
  "locatorDirectory": "locators",
  "inputs": {}
}
```

`operation` is `run` or `resume`. `workflow`, `locatorDirectory`, and `pluginDirectory` are confined to their corresponding directories inside the active slot. Checkpoint paths are never supplied by the task; the Agent always uses host-owned `state/checkpoints` outside versioned slots.

`requiredVersion` is optional but recommended for server-dispatched work and mandatory for resume/update orchestration where version affinity matters.

## Deployment contract

The Agent uses two immutable payload slots, `blue` and `green`. New payloads are staged only into the inactive slot. Every bundle file, the embedded release manifest, and `skeletonkey.exe` are hash verified before promotion. `deployment-state.json` is an atomic pointer containing active/previous slots, versions, revision, and active/previous bundle-manifest hashes.

Promotion does not delete the old slot. Rollback swaps the pointer back after verifying the previous bundle.

## Process lifetime

The Agent must treat the Runner plus its descendant browser processes as one execution process tree. Cancellation or host shutdown must terminate the entire tree. Durable recovery behavior is then determined by the last checkpoint:

- safe boundary: resume the same execution identity;
- terminal boundary: return the persisted immutable result;
- in-flight `Running` handler: fail closed with `SKR3006`; do not automatically replay an ambiguous external side effect.

## Server mapping

The server should persist at least: task ID, execution ID, required runtime version, workflow identity/version, inputs fingerprint, current Agent ID, attempt, and final Runner envelope. The server should not send arbitrary executable paths or checkpoint paths.

A task result can be mapped from the Agent envelope fields `taskId`, `executionId`, `slot`, `runnerExitCode`, `status`, and nested `runner` output.

## Upgrade rule

Do not delete a previous slot while it may still own resumable executions. A server can either keep version affinity and route resumes to the required slot/version, or deliberately start a new execution identity after operator review when compatibility cannot be guaranteed.
