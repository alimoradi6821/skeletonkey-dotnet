# Workflow Analysis 0.1

Workflow analysis is a static layer after workflow semantic validation.

`IWorkflowAnalyzer` analyzes a `WorkflowDocument` against an explicit `IWorkflowNodeDefinitionCatalog`. `DefaultWorkflowAnalyzer` provides the default deterministic implementation.

`WorkflowAnalysisResult` reports workflow identity, optional catalog identity and version context, node analysis, connection analysis, and deterministic issues. `CanPlanExecution` is true only when the analysis contains no error issues.

Catalog-aware analysis may report unknown node types, unavailable node versions, invalid node parameters, unknown ports, invalid port directions, incompatible port roles, invalid dynamic ports, missing required resources, resource kind mismatches, missing capabilities, invalid resource references, and catalog definition conflicts.

Node analysis preserves node identity, resolved definition identity when available, parameter status, resource requirement status, capability compatibility status, effective static and dynamic ports, resource-slot analysis, and node-specific issues.

Connection analysis preserves endpoint identity, connection index when known, source and target port status, resolved effective ports when available, dynamic port status, role compatibility status, and connection-specific issues.

Semantic validation does not require a catalog. Catalog-aware analysis begins after document-local validation and adds exact node-definition, port, resource, and capability checks.

Analysis does not execute workflows, resolve resources, resolve locators, evaluate expressions, evaluate bindings, launch browsers, invoke human handlers, discover plugins, or mutate the workflow.

Analysis results are static observations for future planning. They are not runtime state snapshots and do not contain live handler, resource, or execution context objects.
