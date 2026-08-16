# Workflow Expression Evaluation 0.1

`WorkflowExpressionEvaluator` evaluates the existing parsed workflow expression language.

The evaluator validates expression text with `WorkflowExpressionParser` before evaluation. Parsing remains separate from evaluation.

Values are JSON-compatible:

- null
- boolean
- number
- string
- array
- object

Numbers use `System.Decimal` for deterministic arithmetic and culture-invariant parsing. Non-finite values are not supported. Numeric overflow returns `SKV1014`.

Operators are strict:

- `!` requires boolean.
- unary `+` and `-` require number.
- arithmetic requires numbers.
- `+` may concatenate two strings.
- equality is type-aware and structural for arrays and objects.
- relational comparisons support number/number and string/string with ordinal string comparison.
- logical operators require booleans and short-circuit.
- `??` evaluates the right side only when the left side is explicit JSON null.
- conditionals require a boolean condition and evaluate only the selected branch.

Member and index access are strict. Object member and string-index property lookup are ordinal and case-sensitive. Integer indexes access arrays only and must be non-negative and in range.

Supported roots are `inputs`, `variables`, `nodes`, and `iterations`.

Supported built-in functions are `size`, `isEmpty`, `contains`, `startsWith`, `endsWith`, `trim`, `lower`, `upper`, `coalesce`, `toString`, `toNumber`, and `toBoolean`.

`size` counts strings by Unicode scalar values. `trim` uses .NET deterministic Unicode whitespace trimming. `lower` and `upper` use invariant casing. `toString(null)` returns `"null"`. `toNumber` accepts numbers and invariant numeric strings. `toBoolean` accepts booleans, true/false strings using ordinal-ignore-case, and numbers `0` or `1`.

The evaluator is pure, deterministic, stateless, thread-safe, and side-effect free. It exposes no arbitrary function registry and performs no I/O, host access, reflection execution, dynamic invocation, current-time access, randomness, resource access, locator access, workflow execution, or node execution.
