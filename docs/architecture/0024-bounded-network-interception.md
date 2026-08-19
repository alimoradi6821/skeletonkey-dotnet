# Phase 0-21 Bounded Network Interception

Status: implemented; repository verification required before release tagging.

Phase 0-21 adds provider-neutral network interception contracts and a Playwright implementation for `web.page`. An optional resource-level policy contains an ordered list of rules and a default `allow` or `block` action. Rules use deterministic first-match evaluation and can allow, block, modify request headers, or fulfill a request with a bounded synthetic response.

Playwright routing is registered on the browser context before any page is created. This covers the primary page, later pages, and popups. Service workers are blocked whenever interception is enabled because Playwright context routes cannot observe requests handled by a service worker. A replacement context created by storage-state import receives the same policy before its first page is created.

The public policy model is immutable and bounded. A policy accepts at most 128 unique rules and at most 100000 routed requests during the resource lifetime. Identifiers, patterns, method filters, resource-type filters, header collections, header values, response bodies, and response status codes all have explicit limits. Protected credential and transport headers cannot be injected declaratively, header line injection is rejected, and interception fails closed after its request budget is exhausted.

The Runner requires no new command-line switch. Network policy is part of the existing `web.page` resource constraints and is enforced by the Playwright provider selected by the Runner.

This phase does not add response-body rewriting for real upstream responses, HAR recording or replay, WebSocket message interception, dynamic runtime rule mutation, credential injection, remote proxy configuration, network-state checkpointing, or distributed browser routing.
