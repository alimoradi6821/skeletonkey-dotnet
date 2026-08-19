# 0026: Windows Desktop Automation through FlaUI UIA3

## Status

Accepted for Phase 23.

## Decision

SkeletonKey exposes Windows desktop automation through the existing workflow resource, locator, catalog, handler, and runtime boundaries. Provider-neutral contracts live in `SkeletonKey.Desktop.Abstractions`; built-in node definitions and handlers live in `SkeletonKey.Desktop.BuiltIns`; the Windows implementation lives in `SkeletonKey.Desktop.FlaUI` and uses FlaUI UIA3.

The Windows Runner host explicitly injects one `desktop.application` provider into the platform-neutral Runner Core. `SkeletonKey.Desktop.FlaUI` and the executable host target `net10.0-windows`; contracts, catalogs, handlers, and Runner Core remain platform-neutral. A resource either launches one executable or attaches to exactly one process selected by ID or exact process name. The provider owns its UIA3 automation object and main-window handle for the execution lifetime. Attach mode does not close the external process unless requested.

Desktop nodes consume resolved Locator plans. The UIA3 adapter supports ordered fallback for `role`, `test-id`, `text`, `title`, `label`, `placeholder`, and `alt-text`. CSS and XPath are intentionally unsupported for desktop resources. Locator scopes must resolve to exactly one element, and every operation is bounded by a timeout and cancellation token.

Launch mode records the existing process identities for the requested executable before starting it. During bounded main-window discovery, the provider accepts either the initial process or a newly-created process with the same executable name. This handles executables such as modern Windows Notepad that delegate their window to a successor process without attaching to an unrelated pre-existing instance.

The first node set is `desktop.click`, `desktop.fill`, `desktop.press`, `desktop.getText`, and `desktop.getCount`. No process discovery is exposed to workflows; screen-coordinate scripting, arbitrary shell execution, accessibility-tree mutation, elevation bypass, and cross-session automation are also excluded.

## Consequences

- Workflow JSON remains provider-neutral.
- Desktop execution is supported only on Windows and requires an interactive user session.
- UI Automation role/name/automation-ID semantics can vary between application versions; ordered locator fallbacks are required for resilient workflows.
- Durable resume does not preserve a live desktop application handle.
