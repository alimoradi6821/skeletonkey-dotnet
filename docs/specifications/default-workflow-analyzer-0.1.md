# Default Workflow Analyzer 0.1

`DefaultWorkflowAnalyzer` implements `IWorkflowAnalyzer` as a deterministic, thread-safe, directly constructible analyzer.

Inputs:

- `WorkflowDocument`
- `IWorkflowNodeDefinitionCatalog`
- immutable `WorkflowAnalysisOptions`

The analyzer first observes semantic validation errors, then resolves every workflow node by exact catalog `type` and `typeVersion`. It never chooses latest versions, migrates nodes, scans assemblies, loads plugins, executes handlers, evaluates expressions, resolves locators, or touches live resources.

Diagnostics are sorted deterministically by JSON path, issue code, node ID, and node type. `MaximumIssues` bounds diagnostic growth. Unexpected implementation defects may throw; invalid workflow input is reported as structured analysis issues.

Parameter analysis is bounded to accepted contract metadata such as required properties. Arbitrary plugin JSON Schema evaluation is not implemented in production code.
