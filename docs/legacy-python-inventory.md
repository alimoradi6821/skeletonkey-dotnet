# Legacy Python Inventory

This inventory summarizes the legacy Python SkeletonKey concepts that matter for the new open-source C#/.NET architecture. It is intentionally conceptual rather than a file-by-file narration.

## 1. Existing capabilities

- Executes ordered scenario steps against a Playwright page through action classes registered by action type.
- Supports scenario-level `elements`, initial `context`, and sequential `steps`.
- Provides web automation actions for navigation, clicking, typing, waiting, scrolling, scrolling to elements, hovering, extracting text/html/structured fields, reading cookies/local storage, executing JavaScript, printing values, and removing DOM elements.
- Provides flow actions for conditions, fixed/count-based loops, foreach loops, recording values, and an attempted continue-loop action.
- Stores action outputs into a shared execution context through `save_to`.
- Resolves string templates using `{{ variable }}` syntax with dotted/member-style access and list indexes.
- Resolves named elements from a scenario element registry, including selector strategy, timeout, wait strategy, and optional element indexes such as `messages[3]` or `messages[{{loop.index}}]`.
- Evaluates conditions with helpers such as `exists(...)`, `count(...)`, and `text(...) contains "..."`.
- Manages browser resources with shared and isolated modes, named tabs, named browser contexts, profile paths, runtime profile copies, profile cleanup, storage-state persistence, CDP reconnect, executable discovery, window-state handling, optional stealth settings, and proxy/user-agent/locale configuration knobs.

## 2. Existing architecture

- The workflow unit is a scenario: either a list of action dictionaries or a dictionary containing `elements`, `context`, and `steps`.
- `ActionModel` is a broad Pydantic model shared by all actions. It contains common fields such as `type`, `element`, `value`, `timeout`, `save_to`, `condition`, `params`, nested `actions`, `else_actions`, `error_strategy`, and `meta`.
- `ActionResult` standardizes action output with `type`, `data`, `success`, `error`, and `meta`.
- `AutomationEngine` prepares a scenario and delegates the ordered step list to `ActionExecutor`.
- `ActionExecutor` validates each step as `ActionModel`, resolves the implementation from a module-level `ACTION_REGISTRY`, constructs the action, executes it with the current Playwright page, stores `save_to` data into `Context`, and applies per-action error strategy.
- `BaseAction` owns recursive template resolution for strings, dictionaries, and lists.
- `Context` is a mutable dictionary wrapper with set/get/update/to_dict/clear operations.
- `ElementRegistry` is a context-local class registry loaded from scenario `elements` before execution.
- `TemplateResolver`, `ElementResolver`, and `ConditionResolver` are separate helpers, but they are still tied to the sequential action executor and Playwright page model.
- Browser management is centralized in `CustomBrowser`, which owns Playwright startup, contexts, tabs, persistent profiles, runtime profile lifecycle, storage persistence, and scenario execution.

## 3. Known design limitations

- The scenario model is strictly sequential and action-list based; it does not model nodes, ports, graph connections, branches as first-class graph structures, or designer metadata.
- The single `ActionModel` is too broad and weakly typed for node-specific parameter schemas.
- Action implementations mix workflow semantics, template binding, Playwright locators, browser page operations, logging, and error handling.
- The core engine depends directly on Playwright concepts such as page, locator, browser, and browser context.
- `ACTION_REGISTRY` is a static module-level mapping, which limits plugin discovery, versioning, dependency injection, and test isolation.
- `ElementRegistry` is a class-level registry with context-local storage; it is better than a plain global but still hides dependencies and lifecycle from callers.
- Conditions are string expressions evaluated by generated Python code, which is difficult to validate, secure, serialize, or represent in a future visual editor.
- Loops and condition branches are nested action lists rather than graph constructs, making future visual editing, validation, and deterministic traversal harder.
- Error handling is per-action and executor-local; there is no separate execution policy model for timeout, retry, or on-error behavior.
- Browser/profile management is large and operationally useful, but it is coupled to the runtime engine rather than isolated behind driver/resource abstractions.
- Designer/runtime separation does not exist in the legacy model.

## 4. Known bugs

- `GetCookiesAction` attempts `resolver.set(variable, value)`, but `TemplateResolver` has no `set` method. Storing cookie/local-storage values through this path will fail.
- `ExecuteScriptAction` falls back to `self.data.script`, but `ActionModel` has no `script` field. A script action without `value` can raise `AttributeError`.
- `ForEachAction` stores the index as the literal key `loop.index`; `TemplateResolver` interprets `{{loop.index}}` as nested `loop` then `index`, so foreach loop indexes are not resolved consistently with `LoopAction`.
- `ContinueLoopAction` raises `ContinueLoop`, but the executor does not handle that exception as loop control. It is treated as a normal action failure according to error strategy.
- `ActionExecutor` references `model` inside its exception handler even when model construction itself fails before assignment.
- Unknown action types are logged and skipped, which can make invalid scenarios appear partially successful.
- Invalid element indexes are logged but silently ignored by `ElementResolver`, which can target the first/all matching element instead of failing early.
- No legacy tests or examples were found in the inspected package tree, so these behaviors are not protected by deterministic regression tests.

## 5. Concepts worth preserving

- A compact JSON/YAML-friendly automation language.
- Named elements/selectors separate from step/action definitions.
- Shared execution context and explicit storage of action outputs.
- Template substitution for variables and extracted data.
- Standard action/node result shape with success, data, errors, and metadata.
- Explicit error strategies such as fail, continue, and stop, redesigned as execution policies.
- Conditions, loops, foreach, and recording as user-facing workflow concepts, redesigned as graph/runtime features in later phases.
- Browser profile concepts: named profiles, runtime copies, storage-state persistence, cleanup modes, and executable discovery.
- Human-like typing and typing verification as future web-node behavior, not foundation-layer behavior.
- Driver-specific capabilities such as DOM extraction, cookie/local-storage access, JavaScript evaluation, and element removal as future node handlers outside the core workflow language.

## 6. Concepts that must be redesigned

- Replace sequential scenarios with graph-based workflow documents containing nodes, connections, ports, inputs, variables, execution policies, and optional designer metadata.
- Replace action-type enums with namespace-style node type identifiers such as `core.start`, `core.log`, and future `web.navigate`.
- Replace the broad `ActionModel` with immutable workflow document models plus node definitions and node-specific parameter schemas.
- Replace static registries with explicit catalogs/providers that can support versioned built-in and plugin nodes.
- Replace generated-code condition evaluation with a validated expression or node model in a later phase.
- Separate workflow serialization/validation from execution.
- Separate designer coordinates from runtime semantics.
- Move Playwright, FlaUI, browser resources, profiles, and desktop resources behind driver abstractions in future projects.
- Model timeout, retry, and on-error behavior as explicit execution policies rather than ad hoc action fields.
- Define deterministic validation errors and JSON Pointer-style paths instead of relying on runtime exceptions.

## 7. Mapping from legacy Python concepts to the new architecture

| Legacy Python concept | New architecture direction |
| --- | --- |
| Scenario dictionary/list | `WorkflowDocument` with `nodes` and `connections` |
| `steps` ordered action list | Graph connections between node endpoints |
| `ActionModel.type` enum | Namespace-style node type identifier |
| `ActionModel` shared fields | Immutable workflow node plus extensible `parameters` object |
| Nested `actions` / `else_actions` | Future graph branching and traversal semantics |
| `ActionResult` | `NodeExecutionResult` and `WorkflowExecutionResult` contracts |
| `ACTION_REGISTRY` | `INodeCatalog` and `INodeDefinitionProvider` |
| `BaseAction.execute(page, ...)` | Driver-agnostic `INodeHandler.ExecuteAsync(context, cancellationToken)` |
| Mutable `Context` | `WorkflowExecutionContext`, `NodeExecutionContext`, inputs, and variables |
| `save_to` | Future explicit output-to-variable binding or graph data mapping |
| `TemplateResolver` | Future parameter/input binding and expression resolution layer |
| `ElementRegistry` and `ElementResolver` | Future web/desktop selector resources and driver-specific node parameters |
| `ConditionResolver` | Future validated condition node/expression support |
| `LoopAction` / `ForEachAction` | Future deterministic graph traversal, loop, and collection semantics |
| `error_strategy` | `WorkflowExecutionPolicy.OnError` |
| Action `timeout` fields | `WorkflowExecutionPolicy.Timeout` |
| Browser config/manager/profiles | Future Playwright driver and browser resource packages outside foundation core |
| Web action classes | Future `web.*` node definitions and Playwright-backed handlers |
| Flow action classes | Future `core.*` flow/control node definitions and runtime traversal features |
