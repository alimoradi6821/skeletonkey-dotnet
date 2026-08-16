# Locator Strategies 0.1

Locator strategies are provider-neutral descriptions of semantic UI targets. Strategy order is significant: the first strategy is preferred, and later strategies are deterministic fallbacks.

Version 0.1 strategy kinds are `role`, `label`, `placeholder`, `text`, `test-id`, `title`, `alt-text`, `css`, and `xpath`.

Role strategies declare `role` and optional `name`, `match`, and `caseSensitive`. Label, placeholder, text, title, and alt-text strategies declare `value`, optional `match`, and optional `caseSensitive`. Test-ID strategies declare `value`. CSS and XPath strategies declare selector text.

Text matching supports `exact` and `contains`; regular expressions are intentionally deferred. Empty semantic text and empty selectors are semantically invalid. CSS and XPath are not parsed or executed by the locator validator.

Semantic-first strategies are recommended for stable automation. Provider-specific execution, accessibility role catalogs, browser lookup behavior, waiting, and selector matching are deferred to future provider packages.
