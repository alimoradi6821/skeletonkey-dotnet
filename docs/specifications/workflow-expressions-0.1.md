# Workflow Expressions 0.1

Expressions are safe deterministic workflow-value wrappers. They are parsed and inspected but not evaluated in this phase.

## Wrapper

```json
{
  "$expression": "size(inputs.items) > 0"
}
```

The wrapper has exactly one property. `$expression` must be a non-empty string. `$literal` prevents expression interpretation.

Reserved workflow-value names are `$binding`, `$expression`, and `$literal`.

## Roots

Allowed roots are `inputs`, `variables`, `nodes`, and `iterations`.

Unsupported roots include environment, secrets, resources, browser, page, filesystem, clock, random, execution, and host.

## Grammar

Version 0.1 supports `null`, booleans, integer and decimal numbers, single-quoted strings, grouping, unary `!`, `-`, `+`, arithmetic, comparisons, `&&`, `||`, `??`, conditional `? :`, member access, string and integer index access, and allowlisted function calls.

String escapes are `\\`, `\'`, `\n`, `\r`, `\t`, and `\uXXXX`. Double-quoted expression strings, interpolation, comments, assignment, increment, decrement, bitwise operators, lambdas, method calls, object literals, and array literals are unsupported.

## Precedence

| Level | Operators | Associativity |
| --- | --- | --- |
| 1 | Member and index access, function call | Left |
| 2 | `!`, `-`, `+` unary | Right |
| 3 | `*`, `/`, `%` | Left |
| 4 | `+`, `-` binary | Left |
| 5 | `<`, `<=`, `>`, `>=` | Left |
| 6 | `==`, `!=` | Left |
| 7 | `&&` | Left |
| 8 | `||` | Left |
| 9 | `??` | Right |
| 10 | `? :` | Right |

## Functions

Allowed pure functions are `size`, `isEmpty`, `contains`, `startsWith`, `endsWith`, `trim`, `lower`, `upper`, `coalesce`, `toString`, `toNumber`, and `toBoolean`.

Parsing is separate from evaluation. `WorkflowExpressionParser` validates syntax and discovers references. `WorkflowExpressionEvaluator` performs deterministic, side-effect-free evaluation over `WorkflowValueResolutionContext`.

Evaluation is culture-invariant, strict about operand types, and does not expose user-defined functions or an arbitrary function registry. Short-circuit operators do not evaluate unselected branches.

Expression evaluation does not execute workflows, nodes, handlers, resources, locators, host services, filesystem, network, browser automation, or AI services.

Unknown function names and invalid static arity are rejected. User-defined functions and method calls are not supported.

## Reference Discovery

Parsing exposes immutable references for inputs, variables, nodes, and iterations with deterministic source spans. Member paths are not evaluated and are not converted to JSON Pointer.

## Diagnostics

Diagnostics include source offset and length. Malformed user input never exposes internal parser exceptions.

## Deferred Behavior

Expression evaluation exists as a pure value operation, but planning does not evaluate expression values, access runtime data, or contact host services.

The default execution planner uses expression reference analysis to record data-read dependencies for `nodes['node-id'].outputs['port-id']` references.
