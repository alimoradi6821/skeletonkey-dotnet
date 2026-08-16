# Effective Node Ports 0.1

An effective port is the analyzer-owned view of a node port after static catalog ports and deterministic dynamic ports are combined.

Effective port fields:

- ID
- direction
- required flag
- multiple-connection flag
- roles
- static or dynamic origin
- origin metadata

Port IDs are ordinal and case-sensitive. Duplicate effective IDs are analysis errors.

Static ports come from the exact resolved node definition. Dynamic ports come only from literal workflow node parameters using catalog `WorkflowDynamicPortRule` metadata. Version 0.1 supports `flow.switch` case outputs derived from `/cases` item `/id` values. `$literal` prevents reserved wrapper interpretation; `$binding`, `$expression`, `$resource`, and `$locator` are not executed for dynamic-port discovery.

Role compatibility is catalog-driven. A connection is valid when source and target share at least one role, such as `control` or `data`. Port names alone do not imply compatibility.
