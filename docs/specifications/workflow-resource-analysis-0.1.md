# Workflow Resource Analysis 0.1

Resource analysis matches catalog-declared node resource slots to workflow resource declarations.

The default analyzer:

- reads the node parameter slot as a `$resource` wrapper
- preserves the slot name and parameter path
- resolves the referenced workflow resource declaration by name
- checks exact resource kind
- checks required capabilities
- records declared access mode
- allows missing optional slots
- reports missing required slots

The analyzer does not resolve provider instances, acquire resources, schedule locks, inspect hosts, or contact networks. The result is static metadata suitable for execution planning.

Resource capability strings are treated as declared identifiers. Provider availability is not verified.
