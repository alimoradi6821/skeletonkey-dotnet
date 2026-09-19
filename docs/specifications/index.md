# Specifications

This section contains SkeletonKey's versioned language, runtime, resource, locator, plugin, browser, desktop, validation, planning, compatibility, and packaging contracts.

For the GA 0.1.0 support boundary, start with:

- [Compatibility Policy 0.1](compatibility-policy-0.1.md)
- [Workflow Document Model 0.1](workflow-document-model-0.1.md)
- [Workflow JSON Format 0.1](workflow-json-format-0.1.md)
- [Workflow Runtime 0.1](workflow-runtime-0.1.md)
- [Built-in Node Catalog 0.1](built-in-node-catalog-0.1.md)
- [Desktop Automation 0.1](desktop-automation-0.1.md)
- [Web Page Resource 0.1](web-page-resource-0.1.md)
- [Local Plugin Package 0.1](local-plugin-package-0.1.md)

## Capability Roadmap

- [Automation Capability Roadmap 0.1](automation-capability-roadmap-0.1.md) defines the staged path for generic HTTP I/O, durable state, host-lifetime resources, structured web operations, secrets, deterministic utilities, diagnostics, leases, and side-effect recovery without introducing consumer-domain concepts into SkeletonKey Core.

## Phase 1 Capability Contracts

- [HTTP Request 0.1](http-request-0.1.md) defines provider-neutral bounded outbound HTTP I/O and the `http.request` node.
- [Durable State 0.1](durable-state-0.1.md) defines cross-execution JSON state, workflow/host/custom scopes, atomic compare/exchange, and the local SQLite provider boundary.

## Phase 2 Resource Contract

- [Host-Lifetime Resources and Health 0.1](host-lifetime-resources-0.1.md) defines host-owned resource reuse, health, recycle, cross-execution lease coordination, checkpoint separation, and Playwright health behavior.

## Phase 3 Web Contract

- [Structured Web Primitives 0.1](structured-web-primitives-0.1.md) defines parent-relative collection extraction, observable scrolling, distinct text-input operations, and bounded condition waits.

## Phase 4 Data and Secret Contract

- [Secrets, Hashing, and Workflow-Visible Time 0.1](secrets-hash-time-0.1.md) defines host-owned `$secret` resolution, deterministic `data.hash`, and runtime-clock-backed `time.now`.

## Proposed Packaging Contracts

- [Standalone Export 0.1](standalone-export-0.1.md) defines the scenario-specific sealed executable output mode. It is intentionally separate from the workflow schema and remains proposed until implementation verification is complete.

The table of contents contains the complete specification set.
