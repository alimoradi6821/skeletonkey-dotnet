# Automation Capability Roadmap 0.1

## Status

This document defines the staged implementation plan that follows [ADR 0030: Domain-Agnostic Automation Capability Foundation](../architecture/0030-domain-agnostic-automation-capability-foundation.md).

It is a roadmap and acceptance contract, not a new application-domain model. The capabilities below must remain usable by unrelated consumers such as browser automations, support tools, crawlers, back-office workflows, importers, desktop agents, and future products.

## Goals

The roadmap adds the minimum generic primitives required for reliable recurring automations that:

- observe external state;
- identify and deduplicate work across independent executions;
- call external HTTP services;
- reuse expensive or stateful runtime resources when explicitly hosted that way;
- interact with modern dynamic web applications without application-specific handlers;
- resolve secrets without embedding them in workflow artifacts;
- produce deterministic fingerprints and time values;
- emit useful failure evidence;
- coordinate concurrent workers without duplicate ownership;
- recover conservatively around uncertain external side effects.

## Non-Goals

The roadmap does not add:

- social-network-specific nodes or models;
- chat, message, conversation, customer, ticket, or AI-specific workflow contracts;
- a second workflow execution engine;
- a second scheduler inside workflow semantics;
- arbitrary JavaScript or C# execution as a substitute for typed capabilities;
- direct database, Playwright, `HttpClient`, or secret-store types in Core public contracts;
- application-specific polling intervals, locator catalogs, payload schemas, or business rules.

## Architectural Invariants

Every phase must preserve these invariants:

1. Core workflow, analysis, planning, and runtime contracts remain host-neutral and domain-agnostic.
2. Concrete technologies live behind abstractions and are composed at the host boundary.
3. New built-in nodes use stable type identifiers and explicit `typeVersion` values.
4. Existing retry, timeout, error-policy, event, materialization, and planning infrastructure is reused instead of reimplemented when semantics match.
5. Durable cross-execution state remains separate from durable execution checkpoints.
6. Host scheduling remains outside `WorkflowDocument` as established by ADR 0029.
7. Secret plaintext is never intentionally emitted to logs, events, diagnostics, package metadata, or checkpoints.
8. Side effects with uncertain external outcomes are not blindly replayed.
9. Provider-neutral tests exist before a concrete provider is considered complete.
10. A consuming product must be able to implement its business flow entirely outside SkeletonKey's reusable Core assemblies.

---

# Phase 0 — Foundation and Guardrails

## Purpose

Freeze the architectural boundaries before introducing new capability packages.

## Deliverables

- ADR 0030 defining the domain-agnostic capability direction.
- This implementation roadmap.
- Architecture/contract-safety coverage preventing direct concrete-provider and vendor-domain dependencies in default runtime/core surfaces.
- Documentation table-of-contents entries.
- No new product-specific integration code.

## Acceptance Criteria

- The roadmap identifies package boundaries, node families, provider responsibilities, and test expectations for Phases 1 through 5.
- Default runtime/core assemblies have a test guard against concrete HTTP/database/browser/provider dependencies and named product/vendor integration leakage.
- Existing runtime behavior remains unchanged.
- No workflow schema migration is required by Phase 0.

---

# Phase 1 — External HTTP I/O and Durable State

## Implementation Status

Phase 1 is implemented through provider-neutral HTTP and durable-state contracts, built-in node catalogs/handlers, the concrete `SystemHttpTransport` and SQLite providers, default Runner composition, standalone durable-state provisioning, and architecture guardrails.

Verification coverage is intentionally concentrated at both capability and host-composition boundaries:

- HTTP handler serialization and catalog coverage;
- concrete local HTTP round-trip and cancellation coverage;
- a real Runner workflow that POSTs JSON, binds the parsed response into durable state, and verifies sensitive response-header redaction;
- SQLite persistence across independent provider instances;
- atomic create-if-absent compare/exchange under concurrency;
- stale-version replacement protection after delete/recreate;
- workflow-scope persistence across independent executions;
- contract-safety checks preventing concrete HTTP/SQLite types from leaking into capability abstractions.

The Phase 1 implementation does not add application-, social-network-, AI-provider-, or business-domain concepts to reusable SkeletonKey contracts.

## 1.1 HTTP Capability

### Proposed Projects

```text
src/SkeletonKey.Http.Abstractions
src/SkeletonKey.Http.BuiltIns
src/SkeletonKey.Http.HttpClient

tests/SkeletonKey.Http.Abstractions.Tests
tests/SkeletonKey.Http.BuiltIns.Tests
tests/SkeletonKey.Http.HttpClient.Tests
```

Exact project names may change only if repository naming conventions require it; dependency direction must not change.

### Provider-Neutral Contract

The abstraction layer should represent an HTTP request without exposing `System.Net.Http.HttpClient`, `HttpRequestMessage`, `HttpResponseMessage`, or provider-specific handler types in public contracts.

The minimum request model should support:

- HTTP method;
- absolute URI;
- headers;
- optional text body;
- optional JSON body through SkeletonKey's normal materialized values;
- request timeout input when it cannot be expressed purely through existing node timeout policy;
- bounded response-body limits;
- cancellation.

The minimum response model should support:

- numeric status code;
- response headers in an immutable/read-only shape;
- response text when requested/available;
- parsed JSON when valid and requested by the handler contract;
- final URI after redirects when available;
- provider-neutral timing/metadata only when deterministic contract semantics can be defined.

### Built-In Node

Initial type identifier:

```text
http.request
```

Initial type version:

```text
1
```

The node must integrate with ordinary binding/materialization so URL, headers, and body values can come from prior node outputs or workflow inputs.

### Security Requirements

- Request/response logging must not dump authorization headers or resolved secret plaintext.
- Response size must be bounded to avoid unbounded memory use.
- Redirect behavior must be explicit and testable.
- TLS validation must not be silently disabled by a built-in option.

### HTTP Acceptance Criteria

- A workflow can POST JSON to a local test server and consume status/text/JSON outputs.
- Cancellation interrupts in-flight requests.
- Retry is driven by existing runtime policy unless the HTTP contract explicitly documents a separate transport retry.
- Public abstraction APIs do not expose `HttpClient` types.
- Sensitive headers are redacted from diagnostic output.

## 1.2 Durable State Capability

### Proposed Projects

```text
src/SkeletonKey.State.Abstractions
src/SkeletonKey.State.BuiltIns
src/SkeletonKey.State.Sqlite

tests/SkeletonKey.State.Abstractions.Tests
tests/SkeletonKey.State.BuiltIns.Tests
tests/SkeletonKey.State.Sqlite.Tests
```

### Provider-Neutral Contract

A state store represents durable key/value facts shared across independent workflow executions. It does not expose runtime checkpoint internals.

Initial operations:

```text
state.get
state.put
state.exists
state.delete
state.compareExchange
```

State values should use immutable SkeletonKey-compatible value/JSON representations rather than arbitrary CLR object graphs.

Each stored entry should support enough metadata for safe atomic updates, for example a provider-neutral version/etag token.

### Scope

The contract must define stable key scoping without encoding an application domain. At minimum it must prevent unrelated workflows or configured state namespaces from accidentally colliding.

Candidate scopes:

```text
workflow
host
custom namespace
```

The final scope names must be specified before implementation is considered stable.

### Compare/Exchange

`state.compareExchange` is required in Phase 1 rather than deferred because read-then-write is insufficient for deduplication when multiple executions or hosts race for the same key.

The operation must be able to express:

- create only if absent;
- replace only if the expected version/value matches;
- return whether the exchange succeeded;
- return the observed/current entry metadata needed for a deterministic branch decision.

### SQLite Provider

The first concrete provider is local SQLite because it gives a durable single-host implementation without making a network database mandatory.

Requirements:

- schema creation/migration is deterministic;
- writes are transactional;
- compare/exchange is atomic at the provider boundary;
- cancellation and busy/lock behavior are bounded;
- values and metadata round-trip without provider-specific objects entering abstractions;
- provider storage path is host configuration, not workflow business semantics.

### State Acceptance Criteria

- A value written in execution A is observable in independent execution B.
- Two concurrent compare/exchange attempts for the same absent key produce at most one winner.
- Deleting and recreating keys does not accidentally reuse stale version tokens.
- State survives process restart with the SQLite provider.
- Durable state can be used without enabling execution checkpoint/resume.
- Checkpoint/resume can be enabled without exposing or mutating durable state internals.

---

# Phase 2 — Host-Lifetime Resources and Health

## Implementation Status

Phase 2 is implemented through the `host` resource lifetime, the provider-neutral host registry and health contracts, shared cross-execution lease coordination, checkpoint ownership separation, process-lifetime registry composition in the standalone host, and Playwright health/recycle integration.

Verification includes fake-provider reuse/recycle tests, independent-runtime reuse, checkpoint exclusion, missing-registry failure, JSON/schema round-trip coverage, and an opt-in real Chromium persistent-profile smoke that verifies reuse and replacement after deliberate closure.

## Purpose

Allow explicitly configured hosts to reuse resources such as a browser context or desktop session across multiple ordinary workflow executions without making those resources global workflow semantics.

## Runtime Contract

The current resource lifetime model is extended with a host-owned lifetime, conceptually:

```text
Invocation
Execution
Host
```

The exact enum/API change must be compatibility-reviewed against existing resource specifications before implementation.

A host-lifetime resource:

- is created or obtained by the host resource registry;
- may be leased/accessed by multiple independent workflow executions subject to declared access rules;
- is not disposed at the end of each ordinary execution;
- is disposed when the owning host shuts down or explicitly recycles it;
- must not imply that every host supports persistent resources.

## Host Resource Registry

A generic host registry owns resource instances and coordinates:

- identity;
- creation;
- access mode;
- health checks;
- recycle/recreate;
- final disposal.

The default workflow runtime must consume the capability through an abstraction rather than depending on a particular standalone host implementation.

## Health Contract

Long-lived resources need provider-neutral health states sufficient for host decisions. Initial semantics should distinguish at least:

```text
Healthy
Degraded
Disconnected
Unrecoverable
```

Exact names may vary, but the contract must distinguish reusable, questionable, reconnectable/recyclable, and permanently invalid resources.

A health check must not itself silently create a new resource unless that behavior is explicitly part of the provider contract.

## Browser Reuse Confirmation

Playwright should demonstrate the generic model without changing the abstraction into a browser-specific host lifecycle:

```text
host start
  -> persistent browser/page resource created
  -> execution A uses it
  -> execution B uses the same healthy resource
  -> provider becomes disconnected
  -> host recycles provider resource
  -> execution C uses the replacement
host stop
  -> resource disposed
```

## Acceptance Criteria

- Existing execution/invocation lifetime behavior remains compatible.
- A host-lifetime test resource is reused across two independent workflow runs and disposed exactly once at host shutdown.
- Exclusive resources cannot be concurrently leased in violation of their declared access mode.
- An unhealthy resource can be recycled without restarting the entire host.
- A Playwright integration test confirms persistent resource reuse and recovery from a deliberately closed/disconnected browser context.
- Workflow scheduling settings remain outside `WorkflowDocument`.

---

# Phase 3 — Structured Web Primitives

## Implementation Status

Phase 3 is implemented through `web.extractCollection`, `web.scroll`, `web.scrollIntoView`, `web.type`, `web.insertText`, `web.clear`, `web.focus`, and `web.waitForCondition`.

Verification covers parent-relative field alignment, nested locator wrappers inside field arrays, a real Chromium virtualized-list continuation loop, event-sensitive input semantics, observable asynchronous waits, and the no-Playwright-public-type abstraction boundary.

## Purpose

Make modern SPA and virtualized-list automation possible through typed provider-neutral web operations instead of application-specific code or arbitrary scripts.

## 3.1 Structured Collection Extraction

Initial node:

```text
web.extractCollection
```

A collection extraction defines:

- a parent/item locator;
- zero or more named fields;
- a field locator resolved relative to the current item;
- a field read mode such as text, attribute, value, or another explicitly supported provider-neutral read;
- bounded item count/output size.

Critical semantic rule: **child field locators resolve relative to the matched parent item**, not globally against the page.

Representative result:

```json
[
  {
    "name": "item-a",
    "preview": "value-a",
    "href": "/a"
  },
  {
    "name": "item-b",
    "preview": "value-b",
    "href": "/b"
  }
]
```

The abstraction should prefer performing scoped extraction inside the provider rather than leaking provider element handles into generic workflow values.

## 3.2 Scroll

Initial nodes:

```text
web.scroll
web.scrollIntoView
```

`web.scroll` must support page and element scrolling where the provider supports them and should return observable position information such as:

- scroll offset;
- scroll extent;
- viewport/client extent;
- whether the start or end has been reached.

This allows workflows to implement bounded loops for virtualized content without fixed assumptions.

## 3.3 Text Input Semantics

Initial nodes or explicitly versioned operations:

```text
web.fill
web.type
web.insertText
web.clear
web.focus
```

Where existing handlers already cover an operation, the implementation should extend rather than duplicate them.

The contracts must document semantic differences:

- fill/set value behavior;
- key-by-key typing and generated keyboard events;
- direct text insertion behavior;
- clearing behavior;
- focus behavior.

## 3.4 Observable Wait Conditions

Initial node:

```text
web.waitForCondition
```

Initial condition families should include bounded versions of:

```text
exists
visible
hidden
textEquals
textContains
attributeEquals
countEquals
countGreaterThan
valueEquals
```

The node must use timeout and cancellation semantics consistent with existing runtime/web policies. It must not implement unbounded polling.

## Acceptance Criteria

- Collection extraction preserves field alignment because all fields are scoped to one parent element.
- Virtualized-list tests can extract, scroll, and continue until a deterministic end condition.
- `fill`, `type`, and `insertText` have distinct integration tests against event-sensitive controls/contenteditable elements.
- Wait-condition tests do not depend on arbitrary sleeps for correctness.
- No Playwright element handles or Playwright public types leak through web abstractions.
- No target-application-specific selector logic is added to reusable packages.

---

# Phase 4 — Secrets, Hashing, and Workflow-Visible Time

## Implementation Status

Phase 4 is implemented through the provider-neutral `$secret` wrapper and secret-provider boundary, the default runner environment provider, deterministic `data.hash`, and runtime-owned `time.now`.

Verification includes secret non-retention in framework checkpoints/events, child-workflow propagation, runner environment composition, canonical object/array hash cases, and exact fake-clock time output.

## 4.1 Secret Resolution

### Proposed Project

```text
src/SkeletonKey.Secrets.Abstractions
```

Concrete secret providers may be added separately as needed.

### Binding Form

A secret binding should be explicit and distinguishable from ordinary workflow data, conceptually:

```json
{
  "$secret": "service-api-token"
}
```

The final serialized syntax requires normal schema/compatibility review before acceptance.

### Required Security Properties

- Secret plaintext is not serialized back into the workflow artifact.
- Secret plaintext is redacted from node-input diagnostic serialization.
- Secret plaintext is not stored in execution checkpoints.
- Secret plaintext is not exposed through ordinary node outputs unless a deliberately privileged contract says otherwise.
- Providers may resolve from environment variables, OS credential stores, encrypted files, Vault-like systems, or other hosts without changing workflow semantics.

## 4.2 Deterministic Hashing

Initial node:

```text
data.hash
```

Minimum algorithm:

```text
SHA-256
```

Structured values must use a documented canonical representation so the same logical input produces the same hash across supported runs/platforms.

The node is intended for generic fingerprints, idempotency keys, cache keys, and integrity checks; it is not a password-hashing primitive.

## 4.3 Time

Initial node:

```text
time.now
```

The handler must consume SkeletonKey's existing clock abstraction rather than call the machine wall clock directly, allowing deterministic tests.

Initial outputs should include UTC time in an unambiguous representation. Additional local-time behavior requires explicit timezone semantics.

## Acceptance Criteria

- A secret can be resolved into an HTTP authorization header without appearing in logs/checkpoints/error payloads captured by tests.
- Hash output is deterministic for canonical structured input.
- Hash tests include ordering/canonicalization cases.
- `time.now` can be tested with a fake clock and returns the injected time exactly.

---

# Phase 5 — Production Hardening

## 5.1 Automatic Diagnostics

Introduce a provider-neutral diagnostic contributor abstraction so providers can attach useful bounded evidence when an operation fails.

Examples:

Web provider:

```text
screenshot artifact
current URL
page title
locator resolution trace
candidate match counts
frame path
```

HTTP provider:

```text
method
redacted authority/path metadata
status
bounded timing information
```

Desktop provider:

```text
window/process metadata
screenshot artifact
locator trace
```

Diagnostics must be policy-controlled, bounded, redacted, and must not change execution semantics.

## 5.2 Durable Leases/Locks

If compare/exchange is insufficient for long-running ownership, add a generic lease abstraction or state operations with:

- acquire;
- lease identifier/fencing token;
- expiry;
- renew;
- release;
- deterministic failure when ownership is lost.

Lease correctness must not depend solely on process-local mutexes.

## 5.3 Side-Effect Recovery

Extend existing side-effect/recovery semantics only where needed to make uncertain external outcomes explicit.

The runtime must distinguish at least:

- operation definitely not attempted;
- operation completed and durably recorded;
- external result uncertain because the process/provider failed during or after dispatch.

The default recovery action for an uncertain non-idempotent external side effect must not be blind replay.

Handlers/providers may expose an explicit reconciliation mechanism when the external system allows verification.

## Acceptance Criteria

- Failure diagnostics are attached without leaking configured secrets.
- Diagnostic capture failure does not replace the original workflow failure.
- Two workers cannot simultaneously hold the same exclusive durable lease under concurrency tests.
- Expired leases can be reclaimed with fencing/version protection.
- Crash-injection tests around an external side effect demonstrate that an uncertain operation is surfaced for reconciliation rather than automatically duplicated.

---

# End-to-End Capability Confirmation

After Phase 5, a consumer outside SkeletonKey Core must be able to express this generic pattern using only reusable primitives:

```text
scheduled host occurrence
    -> inspect external UI/system
    -> extract structured items
    -> compute stable fingerprint
    -> atomically claim unseen work in durable state
    -> call external HTTP service
    -> interact with the external UI/system
    -> verify observable result where possible
    -> persist completion
```

The same primitives must be reusable for materially different domains without adding business-specific types to SkeletonKey's default runtime.

## Expected Package Direction

The intended direction after all phases is approximately:

```text
SkeletonKey core workflow / analysis / planning / runtime

SkeletonKey.Http.Abstractions
SkeletonKey.Http.BuiltIns
SkeletonKey.Http.HttpClient

SkeletonKey.State.Abstractions
SkeletonKey.State.BuiltIns
SkeletonKey.State.Sqlite

SkeletonKey.Secrets.Abstractions
SkeletonKey.Data.BuiltIns

SkeletonKey.Web.Abstractions
SkeletonKey.Web.BuiltIns
SkeletonKey.Web.Playwright

host composition / consumer application
```

This diagram is a dependency-direction target, not permission to create empty placeholder projects before their phase is implemented.

## Verification Strategy

Each capability phase should include four levels where applicable:

1. **Contract tests** — public surface, immutability, versioning, dependency boundaries.
2. **Unit tests** — handler semantics and deterministic edge cases.
3. **Provider integration tests** — real SQLite/HTTP/browser behavior with local deterministic fixtures.
4. **End-to-end workflow tests** — deserialize, validate, analyze, plan, execute, and observe outputs through the normal SkeletonKey pipeline.

Tests must prefer deterministic local fixtures. External SaaS accounts and live third-party websites are not required for SkeletonKey conformance.

## Compatibility Rule

This roadmap does not itself reserve every proposed type identifier forever. An identifier becomes normative when its implementing specification/catalog entry is accepted. Once shipped as a supported public node contract, changes follow the repository compatibility policy and require a `typeVersion` transition when semantics are incompatible.
