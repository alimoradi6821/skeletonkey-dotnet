# Node Capabilities and Behavior 0.1

Node definitions may declare ordered capability identifiers and high-level behavior metadata. Capability strings are provider-neutral identifiers and do not imply hidden runtime behavior.

Behavior metadata classifies nodes as action, entry, terminal, branch, loop, invocation, or interaction. It can also mark a node as terminal or as potentially suspending.

The default analyzer reports deprecated definitions and resource capability mismatches. The default planner maps behavior metadata to step kinds and boundary metadata.

Behavior metadata is not handler logic. It does not execute branches, loops, invocations, interactions, retries, timeouts, resources, or browser automation.
