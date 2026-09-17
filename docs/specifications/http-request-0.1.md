# HTTP Request 0.1

## Status

Preview implementation contract for the Phase 1 generic HTTP capability defined by ADR 0030.

## Purpose

`http.request` performs bounded outbound HTTP I/O without introducing service-, AI-, or application-specific semantics into SkeletonKey. The node is implemented over the provider-neutral `IHttpTransport` contract. Concrete transport technology is selected by the host.

## Package Boundary

```text
SkeletonKey.Http.Abstractions
SkeletonKey.Http.BuiltIns
SkeletonKey.Http.HttpClient
```

`SkeletonKey.Http.Abstractions` must not expose `HttpClient`, `HttpRequestMessage`, `HttpResponseMessage`, or transport-handler types in its public API. `SkeletonKey.Http.HttpClient` is the concrete System.Net.Http provider and may use those implementation types internally.

## Node Identity

```text
type: http.request
typeVersion: 1
```

The node has one control input named `main` and activates `continue` on success.

## Parameters

- `url` — required absolute `http` or `https` URI.
- `method` — optional HTTP method, default `GET`; normalized to uppercase by the transport request contract.
- `headers` — optional object whose values are strings.
- `body` — optional text request body.
- `json` — optional JSON request body. `body` and `json` are mutually exclusive.
- `contentType` — optional content type for a text body. JSON bodies use `application/json`.
- `timeoutMilliseconds` — optional per-request timeout; default `30000`, bounded to `1..300000`.
- `maximumResponseBytes` — optional response-body bound; default 1 MiB, maximum 16 MiB.
- `followRedirects` — optional boolean, default `true`.
- `parseJson` — optional boolean, default `true`.

Headers containing CR or LF in names or values are rejected by the provider-neutral request contract.

TLS certificate validation is not disableable through this node contract.

## Outputs

- `status` — numeric HTTP status code.
- `text` — bounded response body decoded as text by the concrete provider.
- `json` — parsed JSON when parsing is enabled and the body is valid JSON; otherwise null.
- `headers` — response headers represented as an object of arrays of strings.
- `finalUrl` — final absolute URI after provider-managed redirects.

An HTTP 4xx or 5xx status is a valid HTTP response and is returned through `status`; it is not automatically converted into a workflow failure. Workflows decide whether a status is acceptable.

## Failure Codes

- `SKH1001` — invalid request contract.
- `SKH1002` — transport failure before a valid response.
- `SKH1003` — response body exceeded the configured bound.
- `SKH1004` — invalid provider response representation.
- `SKH1005` — request timeout.
- `SKH1006` — caller cancellation.

Existing workflow retry/timeout/error policies remain responsible for node retry behavior. The built-in HTTP handler does not silently add an independent retry loop.

## Concrete HttpClient Provider

`SystemHttpTransport`:

- uses `ResponseHeadersRead` so response bounds can be enforced while streaming;
- maintains separate redirect-enabled and redirect-disabled clients;
- disables cookie persistence;
- enables standard automatic decompression;
- applies a linked per-request timeout token rather than an unbounded client timeout;
- rejects declared or streamed response bodies larger than `maximumResponseBytes`;
- wraps transport failures in stable provider-neutral error codes.

## Security Boundary

The HTTP capability does not intentionally emit request headers or bodies as handler diagnostics. In particular, authorization header values are not copied into handler metadata or workflow errors. Secret resolution itself belongs to Phase 4 and is not part of this contract.

## Composition

A consuming host composes the definitions and handlers explicitly:

```csharp
IHttpTransport transport = new SystemHttpTransport();
IReadOnlyList<INodeHandler> handlers = HttpBuiltInRuntimeHandlers.Create(transport);
IReadOnlyList<WorkflowNodeDefinition> definitions = HttpBuiltInWorkflowNodeCatalog.Catalog.Definitions;
```

The host remains responsible for the transport lifetime.
