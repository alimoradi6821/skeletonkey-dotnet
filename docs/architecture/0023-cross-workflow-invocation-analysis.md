# Phase 0-20 Cross-Workflow Invocation Analysis

Status: implemented; repository verification required before release tagging.

Phase 0-20 adds a deterministic preflight pass over the reachable `workflow.invoke` dependency graph. The pass resolves child documents through the host-supplied `IWorkflowRepository`, preserves exact-version lookup semantics, and reports stable `SKD1xxx` issues before runtime state is created.

The analyzer walks invocation nodes reachable from enabled `core.start` nodes. Disconnected and disabled invocation nodes do not create repository requirements. Resolved dependencies are visited in workflow document order, direct and indirect recursion is rejected, and traversal is bounded by the host's maximum invocation depth.

For each resolved child, the analyzer verifies the resolved workflow identity, required child inputs, unknown supplied input names, statically known input types, and mapped child stream source channels. `$binding` and `$expression` values defer type compatibility until runtime materialization; `$literal` values are checked as their enclosed JSON value.

`DefaultWorkflowRuntime` runs this analysis after root semantic validation and before catalog analysis, planning, state creation, or handler execution. A reachable invocation without a repository or any cross-workflow issue fails with `SKR1025`, retaining the first deterministic `SKD1xxx` diagnostic in the error message.

The Runner accepts `--workflow-directory <path>`. It loads at most 1024 top-level `*.workflow.json` files, caps each at 16 MiB, registers ordinary files by document ID, and registers `<workflow-id>@<exact-version>.workflow.json` by exact reference. Versioned lookup never falls back to an unversioned registration.

This phase does not add remote workflow discovery, package registries, version ranges, plugin loading, child resource compatibility, child output value type analysis, distributed scheduling, or live resource recovery.
