# Web Network Interception 0.1

## Scope

`web.network-interception` is an optional capability of a `web.page` resource. A provider that advertises this capability must apply the validated policy to every network request in the resource's browser context.

## Resource constraint

The `network` constraint is a closed object:

```json
{
  "defaultAction": "block",
  "maximumInterceptions": 10000,
  "rules": [
    {
      "id": "mock-config",
      "urlPattern": "https://example.test/api/config",
      "action": "fulfill",
      "methods": ["GET"],
      "resourceTypes": ["xhr", "fetch"],
      "status": 200,
      "contentType": "application/json",
      "body": "{\"enabled\":true}",
      "responseHeaders": {
        "cache-control": "no-store"
      }
    }
  ]
}
```

`defaultAction` defaults to `allow` and may be only `allow` or `block`. `maximumInterceptions` defaults to 10000 and must be between 1 and 100000. `rules` defaults to an empty ordered array and contains at most 128 entries with unique identifiers.

Unknown properties are rejected on both the policy and every rule.

## Matching

Rules are evaluated in declaration order. The first rule whose URL pattern, optional method filter, and optional resource-type filter match is selected. If no rule matches, the default action is used.

URL patterns use ordinal wildcard matching: `*` matches zero or more characters and `?` matches exactly one character. Methods are normalized to uppercase HTTP tokens. Resource types are normalized to lowercase and are limited to `document`, `stylesheet`, `image`, `media`, `font`, `script`, `texttrack`, `xhr`, `fetch`, `eventsource`, `websocket`, `manifest`, and `other`.

## Actions

- `allow` continues the request unchanged.
- `block` aborts the request.
- `modify` continues the request after applying `removeRequestHeaders` and then `setRequestHeaders`. At least one header change is required.
- `fulfill` returns a synthetic response using `status`, `contentType`, `body`, and `responseHeaders`. Status defaults to 200 and must be between 200 and 599.

Request mutation properties are invalid on actions other than `modify`. Synthetic response properties are invalid on actions other than `fulfill`. `contentType` and a `content-type` response header cannot both be declared.

## Safety bounds

Rule identifiers are limited to 64 ASCII letters, digits, dots, underscores, and hyphens. URL patterns are limited to 2048 characters. A rule accepts at most 32 methods, 64 request headers, 64 removed request-header names, and 64 response headers. Header names must be HTTP tokens of at most 128 characters, header values are limited to 8192 characters, and carriage returns or line feeds are rejected. Synthetic response bodies are limited to 1 MiB in UTF-8.

Declarative request mutation cannot set `authorization`, `content-length`, `cookie`, `host`, or `proxy-authorization`. Synthetic responses cannot set `content-length` or `set-cookie`. A resource aborts routed requests after `maximumInterceptions` is exceeded.

## Lifecycle

The policy applies before the first page is created and is retained across pages, popups, and ephemeral context replacement during storage-state import. Enabling interception blocks service-worker registration so context routing cannot be bypassed.
