# SkeletonKey

SkeletonKey is an open-source automation framework under active development.

Current status:
- Pre-alpha
- Repository and architecture foundation only
- Immutable graph workflow document model exists
- Strict workflow JSON serialization exists
- Semantic workflow validation exists
- Normative Workflow Language 0.1 JSON Schema exists
- Reusable conformance fixture suite exists
- Workflow output declarations exist for final values and streamed records
- Workflow invocation contracts exist
- Structured data-binding contracts exist
- Safe expression contracts and pure expression evaluation exist
- Structured control-flow contracts exist
- Explicit iteration contracts exist
- Early return contracts exist
- Invocation stream policy contracts exist
- Workflow resource requirement contracts exist
- Explicit resource references and invocation resource mappings exist
- Versioned locator document contracts and semantic locator strategies exist
- Host-neutral human-interaction contracts exist
- Host-neutral execution result and event contracts exist
- Host-neutral node catalog contracts exist
- Strict node catalog JSON serialization exists
- Node catalog semantic validation exists
- Reserved built-in node catalog definitions exist
- Catalog-aware workflow analysis contracts exist
- Default catalog-aware analyzer is implemented
- Exact node-definition resolution is implemented
- Static and dynamic port analysis is implemented
- Resource and capability analysis is implemented
- Host-neutral execution planning contracts exist
- Default execution planner is implemented
- Control, data, binding, and expression dependencies are planned
- Loop and invocation boundaries are modeled
- Runtime lifecycle-state contracts exist
- Immutable workflow, invocation, and node execution snapshots exist
- Node execution request and context contracts exist
- Runtime-owned event-writer contracts exist
- Scoped runtime resource-access contracts exist
- Exact node handler contracts exist
- Binding resolution is implemented
- Safe expression evaluation is implemented
- Recursive workflow-value materialization is implemented
- Node parameter materialization is implemented
- Evaluation is pure and deterministic
- Core Workflow execution now exists
- Semantic validation, catalog-aware analysis, and execution planning run before execution
- Deterministic sequential dependency scheduling exists
- Parameter materialization occurs before handler execution
- Essential handlers for `core.start`, `core.return`, `flow.if`, `flow.switch`, `flow.foreach`, `flow.repeat`, and `flow.while` exist
- Runtime state snapshots and ordered runtime events exist
- Runtime cancellation exists
- Root execution identity and child invocation identity are separate
- Runtime activation identities and repeated loop body execution exist
- `workflow.invoke` execution through an explicit in-memory workflow repository exists
- Runtime resource provider, instance, slot-accessor, and lease contracts exist
- In-memory interaction sessions and continuations exist
- Locator runtime, exact Locator Catalog lookup, and resolved Locator plans exist
- Locator slots exist in node definitions
- Ordered Locator fallback exists
- Playwright-backed `web.page` resources exist
- Essential Web handlers exist
- A real Chromium Workflow smoke example exists
- Browser installation is an explicit step through `build\install-playwright-browsers.ps1`
- Strict JSON parsing, schema validation, and semantic validation are separate layers
- Loop execution is implemented for built-in loop boundaries
- Subworkflow invocation execution is implemented through explicit repository contracts
- Cross-workflow dependency validation is not implemented yet
- Frames, popups, uploads, downloads, dialogs, and network interception are not implemented yet
- Desktop automation is not implemented yet
- Execution-state persistence is not implemented yet
- Persistence and resume are not implemented yet
- Human interaction is supported through explicit non-durable host handlers or in-memory session continuations
- Catalog discovery and plugin loading are not implemented yet
- Node catalog JSON Schema and conformance fixtures exist
- Browser automation is implemented for the essential `web.page` path only
- Playwright integration is implemented for page-owned browser/context/page resources
- No FlaUI integration yet
- Parallel scheduling and retry execution are not implemented yet
- Plugin discovery and CLI are not implemented yet

Planned direction:
- Graph-based JSON workflow language
- Visual workflow builder support
- Web automation through Playwright
- Windows desktop automation through FlaUI
- Extensible node and plugin architecture

Development requirements:
- .NET 10 SDK

Build:
```powershell
dotnet build SkeletonKey.sln
```

Test:
```powershell
dotnet test SkeletonKey.sln
```

Minimal deserialize and validate example:
```csharp
var serializer = new WorkflowJsonSerializer();
var validator = new WorkflowSemanticValidator();

var workflow = serializer.Deserialize(json);
var result = validator.Validate(workflow);

if (!result.IsValid)
{
    foreach (var issue in result.Errors)
    {
        Console.WriteLine($"{issue.Code}: {issue.Message}");
    }
}
```

The workflow specification is unstable before version 1.0.

Language assets:
- [Workflow 0.1 JSON Schema](schemas/workflow/0.1/schema.json)
- [Workflow Expressions 0.1](docs/specifications/workflow-expressions-0.1.md)
- [Workflow Expression Evaluation 0.1](docs/specifications/workflow-expression-evaluation-0.1.md)
- [Workflow Binding Resolution 0.1](docs/specifications/workflow-binding-resolution-0.1.md)
- [Workflow Value Resolution Context 0.1](docs/specifications/workflow-value-resolution-context-0.1.md)
- [Workflow Value Materialization 0.1](docs/specifications/workflow-value-materialization-0.1.md)
- [Node Parameter Materialization 0.1](docs/specifications/node-parameter-materialization-0.1.md)
- [Workflow Runtime 0.1](docs/specifications/workflow-runtime-0.1.md)
- [Runtime State Transitions 0.1](docs/specifications/runtime-state-transitions-0.1.md)
- [Runtime Scheduling 0.1](docs/specifications/runtime-scheduling-0.1.md)
- [Runtime Output Propagation 0.1](docs/specifications/runtime-output-propagation-0.1.md)
- [Runtime Events 0.1](docs/specifications/runtime-events-0.1.md)
- [Essential Built-In Handlers 0.1](docs/specifications/essential-built-in-handlers-0.1.md)
- [Workflow Control Flow 0.1](docs/specifications/workflow-control-flow-0.1.md)
- [Workflow Resources 0.1](docs/specifications/workflow-resources-0.1.md)
- [Locator Document 0.1](docs/specifications/locator-document-0.1.md)
- [Locator Strategies 0.1](docs/specifications/locator-strategies-0.1.md)
- [Runtime Locator Resolution 0.1](docs/specifications/runtime-locator-resolution-0.1.md)
- [Node Locator Slots 0.1](docs/specifications/node-locator-slots-0.1.md)
- [Web Page Resource 0.1](docs/specifications/web-page-resource-0.1.md)
- [Playwright Page Provider 0.1](docs/specifications/playwright-page-provider-0.1.md)
- [Web Locator Fallback 0.1](docs/specifications/web-locator-fallback-0.1.md)
- [Essential Web Handlers 0.1](docs/specifications/essential-web-handlers-0.1.md)
- [Web Navigation Policy 0.1](docs/specifications/web-navigation-policy-0.1.md)
- [Workflow Human Interaction 0.1](docs/specifications/workflow-human-interaction-0.1.md)
- [Node Catalog 0.1](docs/specifications/node-catalog-0.1.md)
- [Node Definition 0.1](docs/specifications/node-definition-0.1.md)
- [Built-in Node Catalog 0.1](docs/specifications/built-in-node-catalog-0.1.md)
- [Workflow Analysis 0.1](docs/specifications/workflow-analysis-0.1.md)
- [Execution Planning 0.1](docs/specifications/execution-planning-0.1.md)
- [Execution Lifecycle State 0.1](docs/specifications/execution-lifecycle-state-0.1.md)
- [Node Execution Request 0.1](docs/specifications/node-execution-request-0.1.md)
- [Node Execution Context 0.1](docs/specifications/node-execution-context-0.1.md)
- [Node Handler Contracts 0.1](docs/specifications/node-handler-contracts-0.1.md)
- [Node Runtime Resource Access 0.1](docs/specifications/node-runtime-resource-access-0.1.md)
- [Workflow Iteration 0.1](docs/specifications/workflow-iteration-0.1.md)
- [Conformance Suite 0.1](docs/specifications/conformance-suite-0.1.md)
- [Conformance Manifest](tests/fixtures/conformance/manifest.json)
