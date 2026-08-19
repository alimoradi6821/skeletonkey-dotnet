# Desktop Automation 0.1

## Resource

The resource kind is `desktop.application`. A host implementation must provide these capabilities as applicable:

- `desktop.application-lifecycle`
- `desktop.locators`
- `desktop.actions`
- `desktop.forms`
- `desktop.text`

The FlaUI provider accepts a closed `constraints` object. `mode` is `launch` or `attach`.

Launch mode requires `executable`, permits bounded `arguments`, and forbids `processId` and `processName`. Attach mode requires exactly one of positive `processId` or exact `processName`, and forbids `executable`. `closeOnDispose` defaults to `true` for launch and `false` for attach. `mainWindowTimeoutMilliseconds` and `defaultTimeoutMilliseconds` default to 30000 and must be from 1 through 300000.

## Nodes

All nodes require an `application` `$resource` wrapper and a `target` `$locator` wrapper.

- `desktop.click`: optional `button` (`left` or `right`), `clickCount` (1 or 2), `elementIndex`, and `timeoutMilliseconds`.
- `desktop.fill`: required string `value`; optional `elementIndex` and timeout.
- `desktop.press`: required bounded key name; optional `elementIndex` and timeout.
- `desktop.getText`: emits the `result` collection while preserving explicit null values.
- `desktop.getCount`: emits integer `count`.

Supported key names are Backspace, Delete, arrow directions, End, Enter, Escape, Home, PageDown, PageUp, Space, and Tab. Names are case-insensitive.

## Locator mapping

- `test-id` maps to UI Automation `AutomationId`.
- exact `text`, `title`, and `label` map to UI Automation `Name`.
- exact `placeholder` and `alt-text` map to UI Automation `HelpText`.
- `role` maps to a bounded allowlist of UI Automation control types and can additionally constrain `Name`.
- non-exact text strategies perform an ordinal substring comparison over the corresponding UI Automation property.

Strategies run in declared order. A later strategy is considered only when an earlier supported strategy does not satisfy the declared cardinality. Scoped locators resolve each parent scope to exactly one element. `elementIndex` is zero-based and must be within the resolved match set.

## Stable errors

Desktop failures use the `SKR23xx` family for platform, application, window, resource, locator, timeout, action, and cancellation errors. Provider exceptions are converted by built-in handlers into structured workflow errors and do not expose arbitrary process output or secret parameters.

## Runner

`--locator-directory <path>` loads at most 256 top-level `*.locators.json` documents. Each document must be a non-empty regular file no larger than 4 MiB. Symbolic links and duplicate exact catalog identities are rejected. Analysis, planning, and execution use the same immutable resolver.
