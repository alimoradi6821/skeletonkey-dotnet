# Built-in Node Catalog 0.1

SkeletonKey built-in node definitions are contract-only catalog metadata for reserved workflow language nodes.

The built-in catalog defines exactly one version 1 definition for:

- `core.start`
- `core.end`
- `core.return`
- `workflow.invoke`
- `flow.if`
- `flow.switch`
- `flow.foreach`
- `flow.repeat`
- `flow.while`
- `interaction.request`

Static ports match the semantic workflow validation contract. `workflow.invoke` exposes output port `result`. `interaction.request` exposes output port `result` and may suspend. `core.return` is terminal. Loop nodes expose input ports `main`, `continue`, and `break`, and output ports `body` and `completed`.

`flow.switch` declares static output port `default` and a dynamic output-port rule deriving case ports from `/cases` item `/id` values.

Built-in definitions do not store runtime implementation types, executable delegates, handlers, service providers, plugin references, browser types, or host-specific state.
