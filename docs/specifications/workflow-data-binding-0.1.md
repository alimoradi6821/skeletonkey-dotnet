# Workflow Data Binding 0.1

Workflow data binding provides explicit structured references inside workflow values.

No binding evaluation is implemented in this phase.

## Workflow Values

A workflow value can be:

- `null`
- boolean
- number
- string
- array of workflow values
- object containing workflow values
- `$binding` wrapper
- `$expression` wrapper
- `$literal` wrapper

Ordinary JSON is literal by default.

## Reserved Wrappers

The reserved wrapper names are `$binding`, `$expression`, and `$literal`.

An ordinary workflow-value object must not directly contain reserved property names. Use `$literal` when application data must contain reserved names.

## Binding Wrapper

```json
{
  "$binding": {
    "source": "input",
    "name": "account",
    "path": "/id",
    "onMissing": "error"
  }
}
```

Canonical binding property order is:

1. `source`
2. `name`
3. `node`
4. `port`
5. `iteration`
6. `path`
7. `onMissing`
8. `default`

## Binding Sources

`input` bindings require `name` and bind to a declared workflow input.

`variable` bindings require `name` and bind to a declared workflow variable.

`node` bindings require `node` and `port` and bind to a node output port.

`iteration` bindings require `iteration` and bind to an explicit loop node context. They forbid `name`, `node`, and `port`.

Secrets, environment variables, files, resources, and execution metadata are not binding sources in this phase.

## JSON Pointer Paths

Binding paths use read-only RFC 6901 JSON Pointer syntax.

The empty string selects the whole source value. Non-empty paths must start with `/`. Tokens use `~0` for `~` and `~1` for `/`.

URI fragments, JSONPath, dot paths, invalid escape sequences, and array append token `-` are rejected.

## Missing Values

`onMissing` values are:

- `error`: missing data causes future binding resolution to fail.
- `null`: missing data resolves to JSON null.
- `default`: missing data resolves to the explicit `default` value.

`default` is allowed only when `onMissing` is `default`. Explicit JSON null defaults are preserved. Defaults are literal JSON; bindings inside defaults are not evaluated.

Binding syntax is separate from binding resolution. `WorkflowBindingReader` parses binding wrappers, while `WorkflowBindingResolver` resolves parsed bindings against `WorkflowValueResolutionContext`.

When a node output port contains multiple values, binding resolution projects those values to a JSON array preserving order. Empty node output sets are missing. One explicit null value resolves to JSON null.

Defaults remain literal during resolution and are not recursively materialized.

## Literal Wrapper

```json
{
  "$literal": {
    "$binding": {
      "this": "is literal application data"
    }
  }
}
```

The wrapper object contains exactly `$literal`. Binding scanning does not recurse into `$literal` content.

Binding resolution does not execute workflows, nodes, handlers, resources, locators, or browser automation.

## Deferred Behavior

This phase does not evaluate bindings, coerce types, execute expressions, resolve node outputs, materialize literal wrappers, or apply missing-value behavior at runtime.

The default execution planner statically records data-read dependencies for `$binding` values with `source = node`. It ignores bindings inside `$literal` and does not resolve or materialize binding values during planning.
## Phase 0-7D Addendum

Workflow-value wrapper names reserved by the language are `$binding`, `$expression`, `$resource`, `$locator`, and `$literal`. Binding semantics are unchanged; resource and locator wrappers are inspected but not resolved.
