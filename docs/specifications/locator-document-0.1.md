# Locator Document 0.1

Locator documents are standalone artifacts with `specVersion`, `id`, optional `$schema`, optional `name`, optional `description`, and `locators`.

Document IDs and locator IDs use the same local identifier grammar as workflow IDs. Locator IDs are local to one locator document. Locator references use `catalog`, optional exact Semantic Version 2.0 `version`, and local `id`; no version ranges, file paths, URLs, or runtime locator objects are allowed.

Each locator definition contains optional `description`, optional `within`, `cardinality`, and ordered `strategies`. `within` references another locator in the same document. Direct self-scope, unknown scopes, and scope cycles are semantically invalid.

Generic JSON materialization rejects `$locator` references because locators require specialized future locator-aware preparation or provider resolution. No locator resolution or browser object creation is implemented.

Cardinality values are `one`, `zero-or-one`, `one-or-more`, and `many`. Cardinality is an expectation for future diagnostics and does not perform waiting or retries.

Workflow values may contain `{ "$locator": { "catalog": "contacts", "version": "1.0.0", "id": "saveButton" } }`. The catalog is not resolved in version 0.1.
