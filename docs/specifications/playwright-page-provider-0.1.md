# Playwright Page Provider 0.1

`PlaywrightPageResourceProvider` implements `web.page` using the official `Microsoft.Playwright` package. Playwright types remain inside the provider assembly.

Supported constraints are `engine`, `visibility`, `profile`, `userDataDirectory`, `viewportWidth`, `viewportHeight`, `locale`, `userAgent`, `defaultTimeoutMilliseconds`, and `network`.

Persistent profile mode requires an explicit user-data directory. Raw browser launch arguments are not accepted.

When `network` is present, the provider registers one browser-context route before page creation, blocks service workers, applies the bounded first-match policy described by [Web Network Interception 0.1](web-network-interception-0.1.md), and restores the route when storage-state import replaces an ephemeral context.

## Durable reconstruction

The provider implements runtime-resource recovery for ephemeral profiles. At a safe checkpoint it captures Playwright storage state, stable page IDs, the active page, open page URLs, closed/stale references, and bounded ID counters. The state format is independently versioned as `0.1`, allows at most 64 pages and stale-reference IDs, bounds each URL to 8,192 characters, and bounds UTF-8 storage state to 4 MiB.

Recovery launches a new browser, creates a new context with the saved storage state, then reconstructs open pages by navigating to their captured absolute URLs. Every navigation is revalidated by `IWebNavigationPolicy`; an HTTP error response or policy rejection aborts recovery. Network interception is attached before page reconstruction.

Persistent profiles and resources with pending dialogs are explicitly non-resumable. In-flight page operations, downloads, uploads, popup waits, and dialogs are never replayed. Storage state can contain sensitive authentication material, so hosts must protect checkpoint storage appropriately.
