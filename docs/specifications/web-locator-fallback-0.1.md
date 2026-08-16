# Web Locator Fallback 0.1

The Playwright adapter applies scopes outermost to innermost, then tries locator strategies in declaration order. Supported strategy kinds are `role`, `label`, `placeholder`, `text`, `test-id`, `title`, `alt-text`, `css`, and `xpath`.

Cardinality semantics are `one`, `zero-or-one`, `one-or-more`, and `many`. Collection queries preserve DOM order.
