# ADR 0012: Catalog-Aware Analysis and Execution Planning Contracts

Status: Accepted for workflow language 0.1 pre-release.

Workflow semantic validation remains catalog-free. It proves that a workflow document is internally well-formed without requiring node packages, plugins, hosts, handlers, resource providers, or network access.

Catalog-aware workflow analysis is a later static layer. It receives an explicit node definition catalog and may report unknown node types, unavailable node versions, unknown ports, and declared resource mismatches. Catalog analysis still does not execute nodes, evaluate expressions, validate secrets, resolve locators, launch browsers, discover plugins, or contact providers.

Execution planning is also a contract layer. A plan describes ordered node steps, graph dependencies, and declared resource use so future hosts can schedule work consistently. It does not allocate live resources, lock anything, invoke handlers, evaluate bindings, or run workflows.

Node catalogs are explicit inputs instead of global registries. This keeps analysis deterministic, testable, and portable across CLI, service, desktop, and embedded hosts.
