# Host-Lifetime Resources and Health 0.1

## Status

Implemented Phase 2 contract for host-owned runtime resources.

## Purpose

Host-lifetime resources allow an explicitly configured host to reuse expensive or stateful runtime resources across independent workflow executions. The primary browser use case is a long-running agent that keeps one authenticated Playwright context alive while ordinary workflows execute on a schedule.

Host lifetime is resource ownership, not workflow scheduling. Scheduling remains outside `WorkflowDocument`.

## Workflow Contract

`WorkflowResourceLifetime` supports:

```text
invocation
execution
host
```

The JSON representation is:

```json
{
  "resources": {
    "page": {
      "kind": "web.page",
      "lifetime": "host",
      "access": "exclusive",
      "required": true
    }
  }
}
```

A host-lifetime declaration is a requirement. A runtime that has no host resource registry fails deterministically instead of silently downgrading the resource to execution lifetime.

## Host Resource Registry

`IWorkflowRuntimeHostResourceRegistry` is the runtime-facing abstraction. `WorkflowRuntimeHostResourceRegistry` is the process-local implementation used by the default standalone host.

Registry identity is:

```text
workflow id + workflow resource name
```

A reusable entry also carries a deterministic fingerprint of the resource definition. Changing kind, lifetime, access, required flag, capabilities, or constraints causes the old instance to be disposed and replaced.

The registry:

- creates a resource lazily through the configured provider;
- returns the same healthy instance across independent executions;
- validates provider identity, access mode, and required capabilities;
- recycles changed or unhealthy instances;
- supports explicit recycle;
- disposes all owned instances at host shutdown.

The registry never discovers providers and never changes a workflow schedule.

## Health Contract

A resource may implement `IWorkflowRuntimeResourceHealthParticipant`.

Health states are:

```text
Healthy
Degraded
Disconnected
Unrecoverable
```

`Healthy` and `Degraded` are reusable. `Disconnected` and `Unrecoverable` cause replacement before the next reuse. A health exception is treated as unrecoverable unless it is caller cancellation.

Health checks observe the existing resource. They do not create replacements themselves.

## Access Coordination

Exclusive/shared lease coordination belongs to the runtime resource instance rather than to one node accessor. Separate executions that receive the same host-owned instance therefore participate in the same lease gate.

An `exclusive` lease blocks other exclusive or shared leases until it is released. Shared leases may coexist with other shared leases while excluding exclusive access.

## Execution and Checkpoint Ownership

The ordinary execution references a host-owned instance but does not dispose it when the execution completes.

Host-lifetime resources are not serialized into execution checkpoints and are not reconstructed from checkpoint payloads. On resume, the current host registry supplies the host resource again when a remaining node first needs it.

Invocation- and execution-lifetime resource checkpoint behavior remains unchanged.

## Playwright Provider

`PlaywrightPageResource` implements the health contract.

Its check:

- reports `Unrecoverable` after the resource has been disposed;
- reports `Disconnected` when the non-persistent browser reports a lost connection;
- requires at least one open tracked page;
- performs a lightweight browser-context protocol round trip without creating a page or browser.

The standalone host owns one registry for its complete process lifetime. Every scheduled occurrence creates an ordinary runner execution but receives the same host registry. A `web.page` resource declared with `lifetime: "host"` can therefore survive the five-minute scheduling boundary.

## Verification

Phase 2 verification covers:

- healthy reuse and exactly-once host-shutdown disposal;
- automatic recycle after a disconnected health result;
- explicit recycle;
- exclusive lease coordination across separate accessors;
- reuse across independent default-runtime executions;
- exclusion of host resources from execution checkpoints;
- deterministic failure without a configured host registry;
- JSON and schema support for `lifetime: "host"`;
- an opt-in Chromium integration smoke using a real persistent Playwright profile, healthy reuse, deliberate closure, and automatic replacement.

## Consumer Guidance

For a long-lived browser agent, use a persistent Playwright profile with an explicit user-data directory and declare the page resource as host lifetime. Keep application-specific login/challenge detection, selectors, deduplication, and message semantics outside reusable SkeletonKey packages.
