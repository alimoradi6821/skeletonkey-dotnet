# Structured Web Primitives 0.1

## Status

Implemented Phase 3 contract for provider-neutral structured browser automation.

## Purpose

Modern inboxes, SPAs, and virtualized lists need more than click/fill/query primitives. Phase 3 adds bounded collection extraction, observable scrolling, distinct text-input semantics, and condition waits without exposing Playwright objects or arbitrary page scripts to workflows.

## Nodes

Phase 3 adds version-one nodes:

```text
web.extractCollection
web.scroll
web.scrollIntoView
web.type
web.insertText
web.clear
web.focus
web.waitForCondition
```

Existing `web.fill` remains unchanged.

## Structured Collection Extraction

`web.extractCollection` takes a parent/item locator and up to sixteen declared fields. Each field contains:

- `name`;
- a locator reference;
- `mode`: `text`, `attribute`, or `value`;
- `attribute` when mode is `attribute`;
- optional `required`;
- optional `elementIndex`.

Field locators are resolved relative to the current parent match. They are never resolved globally and zipped after the fact.

The operation is bounded by:

- `maximumItems`, default 100 and maximum 1000;
- `maximumOutputCharacters`, default 262144 and maximum 4194304;
- ordinary operation timeout and cancellation.

Outputs are ordered `items`, `totalMatchedCount`, and `truncated`.

Nested field locator wrappers use catalog slots `field1` through `field16` at JSON pointers such as `/fields/0/locator`. Runtime locator-wrapper stripping therefore supports array traversal while preserving the remaining field metadata.

## Scrolling

`web.scroll` supports page/frame-root scrolling when no target is supplied and element scrolling when a target is supplied. It returns:

- current X/Y offsets;
- scroll width/height;
- viewport width/height;
- start/end flags for both axes.

`web.scrollIntoView` scrolls one selected locator match into view.

The Playwright provider uses fixed provider-owned evaluation for metrics and element scrolling. Workflow authors cannot supply arbitrary JavaScript.

## Text Input Semantics

The contracts intentionally distinguish:

- `web.fill`: set/fill semantics suitable for ordinary form input;
- `web.type`: key-by-key input with keyboard events;
- `web.insertText`: direct text insertion after focus;
- `web.clear`: provider-native clear semantics;
- `web.focus`: explicit focus.

These operations do not return the supplied text.

## Observable Waits

`web.waitForCondition` supports:

```text
exists
visible
hidden
textEquals
textContains
attributeEquals
countEquals
countGreaterThan
valueEquals
```

Waits are bounded by timeout and a polling interval constrained to 10-1000 ms. Value conditions require exactly one match unless `elementIndex` is supplied. Count conditions operate on the collection.

## Provider Boundary

`IWebPageAdapter` exposes only SkeletonKey contracts. No Microsoft.Playwright public type crosses the abstraction boundary.

The Playwright implementation scopes child collection locators to each parent `ILocator`, uses native locator actions for input/focus/clear/scroll-into-view, and uses fixed internal evaluation only for scroll metrics.

## Verification

Phase 3 verification includes:

- exact catalog/handler coverage for all eight nodes;
- nested array locator wrapper analysis and runtime materialization;
- parent-relative field alignment with two rows containing different names and links;
- a real Chromium virtualized-list loop that extracts, scrolls, waits for count growth, and continues to a deterministic terminal count;
- distinct event behavior for fill, sequential type, and insertText;
- value-condition waiting driven by an asynchronous page event without sleeps in the test;
- provider-neutral contract tests and the existing no-Playwright-public-type boundary test.
