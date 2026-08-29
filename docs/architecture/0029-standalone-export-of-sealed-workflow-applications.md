# ADR 0029: Export Sealed Standalone Workflow Applications

- Status: Accepted
- Date: 2026-08-28
- Decision makers: SkeletonKey maintainers

## Context and Problem Statement

SkeletonKey currently provides a general-purpose runner that accepts workflow artifacts at invocation time and executes them through the normal deserialize, validate, analyze, plan, and runtime pipeline. A new product requirement needs a different distribution shape: given one SkeletonKey workflow plus host-level execution settings, produce a Windows executable dedicated to that exact scenario and those exact settings.

This capability must not turn scheduling or deployment concerns into workflow semantics, must not fork or duplicate the SkeletonKey execution engine, and must not make the generated executable a second general-purpose runner. The exported application is an output form of SkeletonKey: a sealed host around one workflow.

The architectural question is where this capability belongs and how to preserve the existing workflow and runtime boundaries while supporting scenario-specific executable output.

## Decision Drivers

- Preserve the existing SkeletonKey workflow document model and execution semantics.
- Reuse the same validation, analysis, planning, locator, resource, handler, and runtime components as the normal runner.
- Keep host scheduling and application lifecycle settings outside the workflow schema.
- Produce an executable that is dedicated to one workflow and cannot be repurposed at runtime to execute arbitrary workflow files.
- Make the relationship between an exported executable, its workflow, its settings, and its SkeletonKey version auditable and deterministic.
- Leave room for future distribution modes such as Windows Service or Scheduled Task packaging without requiring those modes in the first implementation.
- Avoid a separate Flow Agent execution engine or a wrapper process that shells out to `skeletonkey.exe`.

## Considered Options

- Add a SkeletonKey standalone export mode that embeds one workflow and one execution-settings document in a scenario-specific executable.
- Build a separate Flow Agent engine that consumes SkeletonKey as a dependency.
- Package the existing `skeletonkey` runner executable next to workflow/settings files and invoke it as a child process.
- Add schedule and deployment settings directly to the core workflow JSON format.

## Decision Outcome

Chosen option: **add a SkeletonKey standalone export mode that produces a sealed scenario-specific application**.

Standalone export is a packaging/output capability of SkeletonKey, not a new execution engine. The exported host must delegate workflow execution to the existing SkeletonKey runtime path. It must not reimplement workflow semantics, node execution, locators, resources, policies, or browser/desktop behavior.

The export inputs are logically separate artifacts:

1. an existing SkeletonKey workflow document;
2. a standalone execution-settings document owned by the host/export layer;
3. any referenced locator documents, subworkflows, plugins, or other explicitly supported runtime dependencies required by that workflow.

The workflow document answers **what the automation does**. The standalone execution-settings document answers **when and how the generated application hosts that workflow**.

The generated application is sealed to those inputs. It must not expose a runtime command-line option that replaces the embedded root workflow or execution settings with arbitrary external files. A new export is required when the workflow or execution settings change.

The initial implemented command surface is:

```text
skeletonkey export standalone \
  --workflow scenario.workflow.json \
  --settings execution.settings.json \
  --output Scenario.exe
```

The 0.1 implementation uses this command surface and currently targets `win-x64`. Additional packaging targets require a later contract change or extension.

### Separation from Workflow Semantics

Standalone scheduling is a host concern and must not be added to `WorkflowDocument`, the workflow JSON schema, workflow analysis, or workflow planning contracts.

Workflow-declared execution policies such as node timeout, retry, and `onError` continue to describe behavior *inside one workflow execution*. Standalone settings describe repeated application-level invocation of that workflow.

For example, an interval schedule does not create workflow nodes, control-flow edges, retries, runtime activations, or new workflow policy semantics. It causes the standalone host to initiate another ordinary workflow execution at the configured time.

### Sealed Artifact Identity

Every generated standalone application must carry immutable package metadata sufficient to identify at least:

- the standalone package format version;
- the SkeletonKey version used to produce the application;
- the root workflow identifier and cryptographic digest;
- the execution-settings cryptographic digest;
- digests or deterministic identities for packaged locator/subworkflow/plugin artifacts when included;
- the target runtime identifier and packaging mode.

The exported executable must use the embedded workflow and settings that correspond to those recorded digests.

### Initial Scheduling Boundary

The first standalone settings version is expected to support only a deliberately small host scheduling surface:

- `once`;
- `interval`;
- `daily`.

The initial overlap behavior is `skip`: when a scheduled occurrence arrives while the previous workflow execution is still active, that occurrence is not started.

A host-level setting may allow immediate execution on application startup. A workflow failure must not corrupt the schedule state or silently convert the standalone application into a permanently failed process when the configured schedule expects future occurrences.

Distributed scheduling, parallel overlapping runs, durable distributed queues, cron expressions, Windows Service installation, and Windows Task Scheduler registration are outside the first implementation boundary.

### Consequences

- Positive: SkeletonKey remains the only workflow execution engine.
- Positive: the normal runner remains general-purpose while standalone exports are intentionally scenario-specific.
- Positive: scheduling can evolve independently from the workflow document model.
- Positive: Flow Agent or another producer can generate workflow/settings artifacts without owning a separate runtime implementation.
- Positive: changing only execution settings requires a new package but does not require rewriting the workflow.
- Positive: exported binaries can be traced back to exact workflow/settings content through hashes and version metadata.
- Tradeoff: every workflow/settings change requires rebuilding the standalone application.
- Tradeoff: recurring schedules require the generated process, or a future OS-managed host mode, to remain available to trigger future executions.
- Tradeoff: browser/runtime dependencies still need an explicit packaging strategy; standalone export does not make those dependencies disappear.
- Negative: standalone packaging adds build and verification surface to SkeletonKey and must be tested separately from normal runner packaging.

## Confirmation

The implementation is now represented by `StandaloneExporter`, the sealed host template, settings/schedule/package contracts, and verification tests. The decision is considered fully confirmed only when repository evidence demonstrates all of the following:

- a documented standalone execution-settings contract exists without changes to the core workflow schema for scheduling;
- the standalone export path validates the root workflow through the existing SkeletonKey validation/analyze/plan/runtime components;
- an exported executable runs without accepting an arbitrary replacement root workflow at runtime;
- package metadata records deterministic hashes for the embedded workflow and settings;
- changing the workflow or settings changes the produced package identity;
- tests cover `once`, interval scheduling, daily scheduling, overlap skipping, cancellation, and failure followed by a future scheduled occurrence;
- packaging verification executes a generated Windows application on a clean supported target;
- the normal `skeletonkey run` behavior remains unchanged.

## Pros and Cons of the Options

### SkeletonKey Standalone Export

- Good, because it reuses the existing execution engine and preserves one semantic implementation.
- Good, because it makes standalone applications a first-class SkeletonKey output without making standalone scheduling part of workflow semantics.
- Good, because the exported artifact can be sealed and provenance-aware.
- Bad, because SkeletonKey gains responsibility for another packaging and verification mode.

### Separate Flow Agent Engine

- Good, because product-specific scheduling could evolve independently.
- Bad, because it creates a second execution host architecture and risks duplicating lifecycle and runtime behavior already owned by SkeletonKey.
- Bad, because execution semantics may drift between SkeletonKey and Flow Agent.

### Wrapper Around `skeletonkey.exe`

- Good, because an early prototype could be built quickly.
- Bad, because it introduces process spawning, stdout/stderr translation, cancellation forwarding, version coordination, and child-process lifecycle failure modes.
- Bad, because the output is not truly a sealed scenario-specific application; it is a launcher around a general runner.

### Schedule Settings Inside Workflow JSON

- Good, because only one input document would be needed.
- Bad, because it changes the meaning of a workflow from host-neutral automation behavior to deployment-specific application lifecycle configuration.
- Bad, because the same workflow could no longer be cleanly hosted by different products with different scheduling policies.

## More Information

- [Standalone Export 0.1](../specifications/standalone-export-0.1.md)
- [ADR 0019: Advanced Web, Runner, and Windows Packaging](0019-advanced-web-runner-and-windows-packaging.md)
- [Runtime Execution Policies 0.1](../specifications/runtime-execution-policies-0.1.md)
- `src/SkeletonKey.Runner.Core/SkeletonKeyRunner.cs`
- `src/SkeletonKey.Runtime.Default/DefaultWorkflowRuntime.cs`
