# Standalone Export 0.1

Status: **Implementation draft complete; acceptance pending build and clean-machine verification.**

Standalone Export defines a SkeletonKey distribution mode that binds one root workflow and one host execution-settings document into a scenario-specific Windows application. It is a packaging and hosting contract. It does not define a second workflow engine and it does not extend the core workflow document with scheduling semantics.

ADR 0029 is the architectural authority for this separation.

## Goals

A conforming Standalone Export implementation must:

- accept one valid SkeletonKey root workflow;
- accept one standalone execution-settings document;
- validate and prepare workflow execution through the existing SkeletonKey pipeline;
- package the workflow, settings, and required supported dependencies into a deterministic scenario-specific application payload;
- produce package metadata that identifies the exact workflow and settings content;
- run the embedded root workflow according to the embedded host schedule;
- reject attempts to replace the embedded root workflow or settings at runtime.

## Non-Goals

Standalone Export 0.1 does not define:

- a new workflow language or workflow schema version;
- a new node execution engine;
- distributed scheduling;
- overlapping parallel workflow occurrences;
- durable distributed queues;
- remote orchestration;
- Windows Service installation;
- Windows Task Scheduler registration;
- arbitrary cron expressions;
- runtime editing of the packaged workflow or schedule.

Those capabilities may be specified separately later without changing the distinction between workflow semantics and host settings.

## Inputs

The logical export input set contains:

```text
scenario.workflow.json
execution.settings.json
locators/                 optional
workflows/                optional subworkflows
plugins/                  reserved; not packaged by the initial 0.1 implementation
```

The root workflow remains a normal SkeletonKey workflow document and must conform to the existing workflow JSON, semantic validation, analysis, and planning contracts.

`execution.settings.json` is a standalone-host document. It is not embedded as an additional property inside the workflow document.

## Execution Settings Document

The initial document shape is proposed as:

```json
{
  "specVersion": "0.1",
  "schedule": {
    "type": "interval",
    "interval": "PT5M"
  },
  "execution": {
    "runImmediately": true,
    "overlap": "skip",
    "continueAfterFailure": true
  }
}
```

Unknown properties and duplicate properties are rejected by the initial strict parser. Durations use ISO-8601 duration text where applicable.

### `specVersion`

`specVersion` identifies the standalone settings contract, independently from the workflow specification version.

Version `0.1` is the only version described by this proposal. Its structural schema is maintained at `schemas/standalone/0.1/schema.json`; semantic validation remains authoritative for bounded durations, duplicate-property rejection, and schedule behavior.

## Schedule Types

### Once

```json
{
  "specVersion": "0.1",
  "schedule": {
    "type": "once"
  }
}
```

The host starts one workflow execution and exits after that occurrence reaches a terminal state, subject to normal cancellation and process termination behavior.

### Interval

```json
{
  "specVersion": "0.1",
  "schedule": {
    "type": "interval",
    "interval": "PT5M"
  }
}
```

The interval must be a positive bounded fixed duration. The initial implementation accepts values from `PT1S` through `P365D`, inclusive, and rejects calendar-year/month components such as `P1M` because they do not represent a fixed elapsed duration.

Scheduled occurrences are calculated by the standalone host. They do not become workflow runtime delays, workflow retry delays, graph nodes, or workflow execution policies.

### Daily

```json
{
  "specVersion": "0.1",
  "schedule": {
    "type": "daily",
    "time": "08:30"
  }
}
```

`time` is a local wall-clock time in `HH:mm` form and uses the generated process's `TimeZoneInfo.Local`. For a spring-forward gap, an invalid requested wall-clock time is normalized forward to the first valid local minute on the same date. For an ambiguous fall-back time, the earlier UTC occurrence is selected. Explicit named time zones remain outside 0.1.

## Host Execution Settings

### `runImmediately`

When true for a recurring schedule, the standalone host starts one occurrence after successful application initialization instead of waiting for the first scheduled boundary.

For `once`, execution is inherently immediate and this property may be omitted or rejected as redundant by the final schema.

### `overlap`

Version 0.1 supports only:

```json
"overlap": "skip"
```

When a new schedule occurrence becomes due while a previous occurrence is still running, the new occurrence is skipped. It is not queued and it does not run in parallel.

This host overlap rule is separate from SkeletonKey runtime parallel scheduling inside one workflow execution.

### `continueAfterFailure`

For recurring schedules, `true` means that a failed workflow occurrence does not terminate the host solely because of that workflow failure; later schedule occurrences remain eligible to run.

This does not alter workflow `retry`, `onError`, or node timeout semantics. Each occurrence is still an ordinary independent SkeletonKey execution.

## Execution Identity

Every occurrence must receive a unique execution identifier. A deterministic package identifier and an occurrence-specific component should be sufficient to correlate logs and artifacts without reusing an execution identifier across repeated runs.

An occurrence must not resume or silently reuse the state of a previous occurrence unless a future standalone checkpoint/resume contract explicitly requests that behavior.

## Exported Application Boundary

A generated application is sealed to its root workflow and execution settings.

A conforming generated executable must not support interfaces equivalent to:

```text
Scenario.exe other.workflow.json
Scenario.exe --workflow other.workflow.json
Scenario.exe --settings other.settings.json
```

External arguments may later be permitted for host-safe operations such as version display, diagnostics, or explicit shutdown behavior, but they must not replace the sealed root workflow/settings in version 0.1.

## Package Manifest

The generated payload must contain package metadata equivalent to:

```json
{
  "format": "skeletonkey.standalone/0.1",
  "skeletonKeyVersion": "0.1.0",
  "targetRuntime": "win-x64",
  "workflow": {
    "id": "check-orders",
    "sha256": "..."
  },
  "settings": {
    "sha256": "..."
  },
  "dependencies": []
}
```

The exact serialization format may evolve during implementation, but the following properties are normative design requirements:

- workflow content is cryptographically identified;
- settings content is cryptographically identified;
- SkeletonKey version is recorded;
- target packaging/runtime identity is recorded;
- supported packaged dependencies are individually identifiable or covered by a deterministic package manifest digest.

The package manifest is metadata, not a source of mutable runtime settings.

## Workflow Preparation and Execution

Standalone Export must reuse the normal SkeletonKey components for workflow behavior. Packaging must not create a simplified alternate executor.

At minimum, the root workflow must pass the same applicable stages used by the runner:

```text
deserialize
validate
analyze
invocation analysis
plan
execute
```

Supported locator documents, subworkflows, plugins, runtime resource providers, and browser/desktop providers must preserve their existing contracts.

The standalone host owns only application lifecycle concerns such as schedule calculation, occurrence start/skip decisions, process cancellation, and termination.

## Failure Behavior

A workflow occurrence produces the normal SkeletonKey terminal result.

For `once`, the generated application may map that terminal result to a process exit code consistent with runner conventions.

For recurring schedules with `continueAfterFailure: true`, one failed occurrence must not prevent later schedule occurrences from starting. Initialization failure, invalid embedded payload, corrupted package metadata, or an unrecoverable host failure may terminate the application.

The implementation must distinguish workflow failure from host/package failure in logs and diagnostics.

## Cancellation and Shutdown

Process cancellation must cooperatively cancel an active workflow occurrence through the existing SkeletonKey cancellation path.

The host must not start another occurrence after shutdown has begun.

The first implementation may use console/process lifetime only. Service-control integration is explicitly outside 0.1.

## Packaging Target

The initial product requirement is a Windows `.exe`. The first implementation should target a documented Windows runtime identifier such as `win-x64` and should prefer self-contained distribution when compatible with SkeletonKey dependencies.

The standalone host is published as a self-contained single-file application with managed, native, and content payloads configured for self-extraction. Playwright browser binaries themselves are not provisioned by `export standalone`; they remain an explicit machine dependency and must already be installed by the normal SkeletonKey browser-installation path. Standalone Export must not silently treat a missing external dependency as part of workflow semantics.

## Planned Command Surface

The design target is conceptually:

```text
skeletonkey export standalone \
  --workflow scenario.workflow.json \
  --settings execution.settings.json \
  --output Scenario.exe
```

The implementation accepts `--locator-directory`, `--workflow-directory`, and `--runtime`; version 0.1 currently supports only `win-x64`. Plugin packaging is intentionally deferred until plugin dependency closure can be sealed and verified safely.

The initial compiler implementation runs from a SkeletonKey source checkout with the .NET SDK available because it publishes `tools/SkeletonKey.Standalone.Host` with the selected snapshot embedded as resources. This is a packaging-time requirement, not a runtime requirement of the generated executable.

Command names and switches remain provisional until conformance and clean-machine verification complete.

## Validation Requirements Before Acceptance

Before this specification moves from Proposed to Accepted, tests and verification must cover at least:

- valid and invalid settings parsing;
- strict rejection of unknown or malformed settings;
- `once` execution;
- interval execution;
- daily execution and documented local-time behavior;
- `runImmediately` behavior;
- overlap skip behavior;
- workflow failure followed by a later recurring occurrence;
- cancellation during an active occurrence;
- deterministic workflow/settings digests;
- corruption or digest mismatch detection;
- rejection of arbitrary runtime workflow/settings replacement;
- normal existing `skeletonkey run` behavior remaining unchanged;
- clean-machine execution of a generated Windows application.

## Compatibility

Standalone settings have their own `specVersion` and compatibility policy. A workflow remains independently versioned by the existing workflow specification.

Changing schedule configuration must not require a new workflow schema version. Changing workflow behavior must not require a new standalone settings schema version unless the host contract itself changes.

A generated executable is an immutable distribution artifact. Updating either the workflow or standalone settings requires producing a new executable/package.
