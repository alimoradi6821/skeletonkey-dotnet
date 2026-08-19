# Web Page Resource 0.1

`web.page` is a provider-neutral workflow resource kind. A page resource exposes web capabilities and a scoped `IWebPageAdapter` through the resource handle.

The runtime owns resource creation, reuse by workflow resource name, lease access, and deterministic disposal after workflow completion, failure, or cancellation.

Providers may advertise `web.network-interception`. When requested, the resource's closed `network` constraint defines an immutable, bounded request policy. See [Web Network Interception 0.1](web-network-interception-0.1.md).
