# Web Page Resource 0.1

`web.page` is a provider-neutral workflow resource kind. A page resource exposes web capabilities and a scoped `IWebPageAdapter` through the resource handle.

The runtime coordinates resource creation and lease access. Invocation/execution-lifetime resources are disposed after workflow completion, failure, or cancellation; host-lifetime resources are owned by the host registry and survive ordinary execution boundaries until recycle or host shutdown.

Providers may advertise `web.network-interception`. When requested, the resource's closed `network` constraint defines an immutable, bounded request policy. See [Web Network Interception 0.1](web-network-interception-0.1.md).
