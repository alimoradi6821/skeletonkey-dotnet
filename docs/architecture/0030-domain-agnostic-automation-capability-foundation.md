# ADR 0030: Domain-Agnostic Automation Capability Foundation

- Status: Accepted
- Date: 2026-09-17
- Decision makers: SkeletonKey maintainers

## Context and Problem Statement

SkeletonKey already separates workflow contracts, analysis, planning, runtime execution, resources, provider abstractions, and concrete automation providers. It also supports recurring standalone execution, durable execution checkpoints, browser automation, desktop automation, plugins, runtime policies, and resumable resources.

The next capability set must support long-running recurring automations that discover external work, deduplicate it across executions, call external services, interact with stateful applications, and recover from process or provider failures. Typical consumers may automate social inboxes, support queues, CRMs, administrative panels, crawlers, importers, or other application-specific workflows.

Those use cases must not introduce application concepts such as a particular social network, messaging product, AI provider, CRM, customer, conversation, or direct message into SkeletonKey's reusable runtime. The architectural question is how to add the missing primitives without turning SkeletonKey into a collection of product-specific integrations or weakening the existing host-neutral boundaries.

## Decision Drivers

- Keep SkeletonKey domain-agnostic and reusable across unrelated automation products.
- Preserve the existing separation between workflow semantics, host lifecycle, provider abstractions, and concrete providers.
- Express external I/O, durable state, resource reuse, web interaction, secrets, deterministic data operations, and diagnostics as generic capabilities.
- Keep concrete technologies such as `HttpClient`, SQLite, PostgreSQL, Redis, Playwright, and secret stores behind provider or adapter boundaries.
- Make recurring automation safe against duplicate work, process crashes, stale long-lived resources, and partial side effects.
- Preserve deterministic testing by using explicit clocks, state abstractions, and versioned node contracts instead of hidden global state.
- Allow future consumers to compose these primitives without requiring changes to SkeletonKey Core for each application domain.
- Avoid arbitrary scripting as a substitute for missing first-class automation contracts.

## Considered Options

- Add reusable capability families to SkeletonKey behind explicit abstractions and provider packages.
- Add application-specific integrations directly to the runtime and built-in node catalog.
- Put all missing behavior into generic script/evaluate nodes.
- Require every consuming application to implement durable state, HTTP calls, browser lifecycle, retries, and diagnostics outside SkeletonKey.

## Decision Outcome

Chosen option: **add reusable capability families behind explicit abstractions and provider packages while keeping SkeletonKey Core domain-agnostic**.

The reusable architecture is organized around capabilities rather than business domains. Product-specific workflows remain consumers of SkeletonKey and define their own locators, state keys, API payloads, scheduling choices, and business rules.

### Capability Boundaries

The following capability families are approved for incremental implementation after this foundation phase:

1. **External HTTP I/O**
   - Provider-neutral request/response contracts.
   - A generic `http.request` node.
   - Concrete HTTP transport in a provider package rather than Core.
   - Retry and timeout behavior composed with existing runtime execution policies where possible.

2. **Durable cross-execution state**
   - A provider-neutral state-store abstraction.
   - Generic operations such as get, put, exists, delete, and atomic compare/exchange.
   - Provider packages such as local SQLite first, with PostgreSQL or Redis possible later.
   - State is distinct from one-execution runtime variables and from durable execution checkpoints.

3. **Host-lifetime resources**
   - A generic resource lifetime longer than one workflow execution for hosts that intentionally reuse expensive or stateful resources.
   - Host-owned creation, health checking, recycling, and disposal.
   - Workflow semantics must not assume a specific host process, browser, desktop application, or agent product.

4. **Structured web interaction**
   - Provider-neutral collection extraction with element-relative field resolution.
   - First-class scrolling and scroll-position results.
   - Explicit text-input operations for fill, type, insert-text, clear, and focus semantics where required.
   - Rich wait conditions based on observable page state instead of fixed sleeps.
   - Playwright remains a concrete provider, not part of the public provider-neutral contracts.

5. **Secrets**
   - A secret-resolution abstraction owned outside workflow business data.
   - Secret values must be redacted from logs, runtime events, diagnostics, and durable checkpoints unless an explicitly safe representation is defined.
   - Concrete secret stores remain host/provider concerns.

6. **Deterministic data and time primitives**
   - Generic deterministic hashing for idempotency keys and fingerprints.
   - Workflow-visible time through the existing clock abstraction rather than direct wall-clock calls inside handlers.

7. **Diagnostics and resource health**
   - Provider-neutral diagnostic contribution contracts.
   - Provider-specific evidence such as screenshots, locator traces, current URL, response metadata, or window metadata.
   - Long-lived resources must expose enough health information for the host to recycle stale or disconnected resources safely.

8. **Atomic leases and side-effect recovery**
   - Durable atomic ownership primitives may build on the state abstraction when compare/exchange alone is insufficient.
   - Side-effecting handlers must preserve explicit recovery semantics and must not blindly replay an operation whose external outcome is uncertain.

### Dependency Direction

Dependency direction remains inward:

```text
consumer workflow / host
        |
        v
built-in capability contracts and nodes
        |
        v
provider-neutral abstractions
        ^
        |
concrete providers/adapters
```

Core workflow, analysis, planning, and runtime assemblies must not reference concrete HTTP clients, databases, browser engines, secret stores, social platforms, AI providers, or product-specific integration packages.

Concrete providers may depend on the corresponding abstractions. Consumer applications may compose concrete providers and domain-specific logic at the host boundary.

### Contract Versioning

New nodes use stable type identifiers and explicit `typeVersion` values consistent with the existing node catalog and compatibility policy. Provider-neutral contract changes follow the existing compatibility rules instead of silently changing node behavior.

### Durable State Is Not Checkpoint State

Durable workflow checkpoints answer: **where was this execution and how can it resume?**

The new state capability answers: **what durable application-neutral facts must be visible across independent executions?**

A workflow may use durable state for idempotency or progress markers without exposing or mutating runtime checkpoint internals. State providers must not become a backdoor into runtime activation state.

### Host-Lifetime Resources Are Not Workflow Scheduling

A host-lifetime resource extends resource ownership beyond one workflow execution. It does not move scheduling into `WorkflowDocument` and does not change the separation established by ADR 0029.

The host decides when executions occur and how long host-scoped resources exist. A workflow only declares the resource access it requires through the normal resource model.

### Domain-Agnostic Rule

Reusable SkeletonKey production assemblies must not introduce public contracts or direct dependencies tied to a specific consumer domain or vendor when the behavior can be represented by a generic capability.

For example, the reusable layer may contain `http.request`, state operations, collection extraction, hashing, and resource-health contracts. It must not require `InstagramMessage`, `WhatsAppConversation`, `OpenAIResponse`, or equivalent product-specific runtime concepts.

Vendor- or product-specific adapters may exist later as optional integration packages, but they must not become dependencies of Core, analysis, planning, or default runtime assemblies.

### Arbitrary Scripting Boundary

A general-purpose browser script/evaluate node is not part of the initial capability plan. Missing automation behavior should first be represented by typed, provider-neutral capabilities. If arbitrary scripting is later required, it must be introduced by a separate decision with explicit capability, security, compatibility, and recovery boundaries.

## Implementation Sequence

This decision is implemented incrementally:

- **Phase 0 — Foundation and guardrails:** architecture decision, implementation roadmap, domain-agnostic contract safety, and baseline confirmation.
- **Phase 1 — External I/O and durable state:** HTTP abstractions/nodes, state abstractions/nodes, and a local SQLite state provider.
- **Phase 2 — Host resource lifetime:** host-scoped resource ownership, browser/resource reuse, health checks, and recycling.
- **Phase 3 — Structured web primitives:** collection extraction, relative scopes, scrolling, explicit text input, and observable wait conditions.
- **Phase 4 — Secrets and deterministic utilities:** secret resolution/redaction, hashing, and workflow-visible clock primitives.
- **Phase 5 — Production hardening:** automatic diagnostics, leases/locks, and stronger side-effect recovery contracts.

Each phase must preserve existing workflow behavior unless a separately versioned contract explicitly changes it.

### Consequences

- Positive: consuming applications can build complex recurring agents without moving their domain model into SkeletonKey.
- Positive: the same primitives support social inboxes, CRMs, ticketing systems, crawlers, administrative panels, and unrelated automation products.
- Positive: transport, persistence, browser, and secret technologies remain replaceable.
- Positive: durable idempotency and long-lived resource reuse become first-class concerns instead of ad-hoc host code.
- Positive: typed primitives remain analyzable and testable by SkeletonKey's existing catalog, validation, planning, and runtime pipeline.
- Tradeoff: more packages and contracts must be maintained as capabilities are introduced.
- Tradeoff: provider-neutral contracts require more design work than exposing concrete technology APIs directly.
- Tradeoff: host-lifetime resources and durable state increase lifecycle and concurrency complexity and require explicit tests.
- Negative: some application-specific workflows may still require custom plugins or adapters; SkeletonKey intentionally does not absorb their business semantics.

## Confirmation

Phase 0 is considered complete when repository evidence demonstrates all of the following:

- this ADR is accepted and published in the architecture table of contents;
- an implementation roadmap specifies capability boundaries and acceptance criteria for Phases 1 through 5;
- contract-safety tests protect default runtime/core public surfaces from direct vendor/domain-specific automation dependencies;
- no application-specific integration code is introduced by Phase 0;
- existing compatibility, workflow, runtime, and standalone-host decisions remain authoritative and are referenced rather than duplicated.

Later phases confirm this decision by adding generic packages and nodes that follow these boundaries without adding consumer-domain concepts to Core.

## Pros and Cons of the Options

### Reusable Capability Families

- Good, because one architecture supports many unrelated automation products.
- Good, because provider technologies remain replaceable.
- Good, because typed nodes preserve validation, planning, retry, diagnostics, and compatibility behavior.
- Bad, because contract design and conformance work is required before each provider implementation.

### Application-Specific Runtime Integrations

- Good, because one target application could be implemented quickly.
- Bad, because business concepts leak into reusable runtime contracts.
- Bad, because each new application expands Core and creates incompatible lifecycle assumptions.

### Arbitrary Script Nodes

- Good, because scripts can bypass missing typed capabilities quickly.
- Bad, because static analysis, security boundaries, recovery behavior, deterministic testing, and compatibility become substantially weaker.
- Bad, because provider-neutral abstractions become optional rather than architectural boundaries.

### Consumer-Owned Infrastructure Only

- Good, because SkeletonKey itself remains smaller.
- Bad, because every consumer repeats HTTP, persistence, idempotency, browser lifecycle, diagnostics, and recovery infrastructure.
- Bad, because those behaviors cannot participate consistently in SkeletonKey policies, events, conformance, or compatibility guarantees.

## More Information

- [Automation Capability Roadmap 0.1](../specifications/automation-capability-roadmap-0.1.md)
- [ADR 0013: Runtime State, Context, and Handler Boundaries](0013-runtime-state-context-and-handler-boundaries.md)
- [ADR 0018: Locator and Playwright Web Runtime](0018-locator-and-playwright-web-runtime.md)
- [ADR 0020: Durable Execution Checkpoints and Resume](0020-durable-execution-checkpoints-and-resume.md)
- [ADR 0021: Runtime-Owned Execution Policies](0021-runtime-owned-execution-policies.md)
- [ADR 0027: Resumable Runtime Resources](0027-resumable-runtime-resources.md)
- [ADR 0029: Export Sealed Standalone Workflow Applications](0029-standalone-export-of-sealed-workflow-applications.md)
- [Compatibility Policy 0.1](../specifications/compatibility-policy-0.1.md)
- [Node Capabilities and Behavior 0.1](../specifications/node-capabilities-and-behavior-0.1.md)
