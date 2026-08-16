# Execution Plan Boundaries 0.1

Execution-plan boundaries mark graph structure that a future runtime must understand without executing it during planning.

Branch boundaries preserve branch-capable node type and available output ports for `flow.if` and `flow.switch`.

Loop boundaries preserve loop controller step, body output, completed output, continue input, break input, and iteration ID for `flow.foreach`, `flow.repeat`, and `flow.while`. Loops are not unrolled.

Invocation boundaries preserve opaque `workflow.invoke` metadata: referenced workflow ID, optional exact version, stream policy shape, fixed result port, and child invocation marker. Child workflow loading and cross-workflow planning are deferred.

Return boundaries mark `core.return` as terminal for the current invocation. Outcome values are not evaluated.

Interaction boundaries mark `interaction.request` as a step that may suspend. No interaction handler is called.
