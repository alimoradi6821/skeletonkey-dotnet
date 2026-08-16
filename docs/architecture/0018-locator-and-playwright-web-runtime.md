# ADR 0018: Locator and Playwright Web Runtime

## Status

Accepted for Phase 0-15.

## Decision

The primary goal is a complete, stable, professional automation library.

Locator documents define stable semantic element identities. They remain separate from Workflow documents so UI target catalogs can evolve independently from orchestration graphs and can be reused across workflows.

The runtime resolves Locator references into provider-neutral Locator plans. Web handlers use scoped page resources and declared Locator slots. Handlers cannot perform arbitrary Locator repository lookup; they can only access locator plans declared by their node definition and prepared by the runtime.

Locator slots mirror Resource slots because both represent non-JSON execution dependencies consumed before handler invocation. Consumed `$resource` and `$locator` wrappers are omitted from materialized handler JSON.

Web handlers depend on provider-neutral adapters. The Playwright provider is an implementation detail and does not leak into the Workflow language or Handler contracts.

Phase 0-15 implements a page-owned browser/context model: a `web.page` resource internally owns Playwright, browser, context, and page lifetime, then exposes only `IWebPageAdapter`.

Strategies use ordered fallback and cardinality is enforced. Multiple matches are not silently reduced to the first; singular actions require one match unless an explicit non-negative element index is provided.

Browser installation is separate from build. Screenshots return owned data instead of arbitrary filesystem paths. Navigation has a policy boundary; the default allows `http`, `https`, `data`, and `about`, and rejects `javascript` and `file`.

No CAPTCHA bypass is implemented. Frames, popups, uploads, downloads, dialogs, network interception, persistence, parallel scheduling, retry execution, plugin discovery, and CLI are deferred.
