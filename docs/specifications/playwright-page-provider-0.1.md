# Playwright Page Provider 0.1

`PlaywrightPageResourceProvider` implements `web.page` using the official `Microsoft.Playwright` package. Playwright types remain inside the provider assembly.

Supported constraints are `engine`, `connection`, `cdpEndpoint`, `visibility`, `profile`, `userDataDirectory`, `viewportWidth`, `viewportHeight`, `locale`, `userAgent`, `defaultTimeoutMilliseconds`, and `network`.

## Connection modes

The default `connection: "launch"` mode keeps the existing managed-browser behavior. The provider launches Chromium, Firefox, or WebKit itself. Persistent profile mode requires an explicit user-data directory. Raw browser launch arguments are not accepted.

`connection: "cdp"` attaches to an already-running Chromium-based browser through Chrome DevTools Protocol by calling Playwright's CDP connection API. It requires an explicit `cdpEndpoint`, for example:

```json
{
  "engine": "chromium",
  "connection": "cdp",
  "cdpEndpoint": "http://127.0.0.1:9222",
  "defaultTimeoutMilliseconds": 30000
}
```

CDP resources use the externally managed browser's default context and existing pages. SkeletonKey does not create, own, close, or replace that default browser context. Resource disposal disconnects the Playwright browser connection while leaving the externally managed browser lifecycle to its owner.

CDP attachment is Chromium-only. Launch/context-creation constraints such as `channel`, `visibility`, `profile`, `userDataDirectory`, viewport, locale, and user agent are rejected in CDP mode because those settings must be chosen by the process that starts the external browser. Declarative network interception is also rejected in CDP mode because SkeletonKey cannot guarantee service-worker and context-creation semantics for an externally created context.

By default the host permits only loopback CDP endpoints. A host must explicitly construct `PlaywrightPageProviderOptions(allowRemoteCdpEndpoints: true)` to permit non-loopback endpoints. This is a host trust decision because a CDP endpoint provides privileged control over the attached browser. The connect timeout is separately bounded by `cdpConnectTimeoutMilliseconds` and defaults to 30 seconds.

CDP-attached resources do not participate in durable browser-context reconstruction and do not support storage-state import that replaces the browser context. Ordinary provider-neutral page operations continue through `IWebPageAdapter`.

## Network interception

When `network` is present in launch mode, the provider registers one browser-context route before page creation, blocks service workers, applies the bounded first-match policy described by [Web Network Interception 0.1](web-network-interception-0.1.md), and restores the route when storage-state import replaces an ephemeral context.

## Durable reconstruction

The provider implements runtime-resource recovery for launched ephemeral profiles. At a safe checkpoint it captures Playwright storage state, stable page IDs, the active page, open page URLs, closed/stale references, and bounded ID counters. The state format is independently versioned as `0.1`, allows at most 64 pages and stale-reference IDs, bounds each URL to 8,192 characters, and bounds UTF-8 storage state to 4 MiB.

Recovery launches a new browser, creates a new context with the saved storage state, then reconstructs open pages by navigating to their captured absolute URLs. Every navigation is revalidated by `IWebNavigationPolicy`; an HTTP error response or policy rejection aborts recovery. Network interception is attached before page reconstruction.

Persistent profiles, CDP-attached resources, and resources with pending dialogs are explicitly non-resumable. In-flight page operations, downloads, uploads, popup waits, and dialogs are never replayed. Storage state can contain sensitive authentication material, so hosts must protect checkpoint storage appropriately.
