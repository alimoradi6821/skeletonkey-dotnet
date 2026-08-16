# ADR 0011: Declarative Resources, Locators, and Human Interaction

Status: Accepted for workflow language 0.1 pre-release.

The primary goal is a complete, stable, professional automation library.

Workflow documents declare resource requirements; hosts provide resource instances.

Locator documents describe semantic UI targets independently from workflow control logic.

Resource, locator, and interaction contracts remain provider-neutral.

Workflows declare what resources they require instead of storing live browser objects, UI handles, dependency injection containers, service locators, API clients, or secret values. Resource resolution belongs to the Host because only the Host knows available providers, credentials, machine configuration, installed browser engines, and security boundaries.

Resource mapping across subworkflows is explicit. Child workflow invocations do not inherit resources automatically, and name-based coupling is rejected so graph behavior remains inspectable and portable. Exclusive and shared access are declarative contracts for future planning and scheduling; this phase does not implement locking or runtime synchronization.

Locator documents are separate artifacts because selector changes should not require workflow graph changes when the semantic target remains the same. Strategies are semantic-first with ordered fallbacks: role, label, placeholder, text, test ID, title, and alt text should be preferred, while CSS and XPath remain supported fallback mechanisms for provider-specific edge cases. Locator execution is provider-specific and deferred.

Browser requirements are not Playwright launch options. Engine, profile, and visibility constraints describe host-neutral preferences, not executable paths, profile directories, browser arguments, credentials, extensions, or environment-specific configuration.

Manual login and other human actions are modeled as host-neutral interaction requests. Secret interaction values require redaction from logs, traces, and ordinary diagnostics. Suspension, resumption, event delivery, and handler implementations are deferred.

Legacy global locator registries are rejected because they hide dependencies and couple execution state to mutable process-wide data. AI convenience does not control the language design; humans, visual editors, external tools, and AI systems consume the same stable contracts.
