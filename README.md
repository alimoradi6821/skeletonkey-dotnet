# SkeletonKey

SkeletonKey is an open-source automation framework under active development.

Current status:
- General-availability release `0.1.0` is frozen by Phase 30 for the verified Windows x64 support contract
- Executable Windows Runner and reusable .NET automation libraries
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
- Essential handlers for `core.start`, `core.end`, `core.return`, `flow.if`, `flow.switch`, `flow.foreach`, `flow.repeat`, and `flow.while` exist
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
- Advanced Web automation supports multiple pages, popups, nested frames, uploads, downloads, dialogs, cookies, storage-state transfer, and advanced waits
- Filesystem artifacts are confined to a host-owned root and use opaque references with SHA-256 metadata
- The `skeletonkey` Runner provides version, validation, analysis, planning, execution, and browser-install commands
- Runner output supports a single JSON envelope or event/result NDJSON records; optional bounded diagnostics are isolated on stderr
- Windows publish scripts emit a package manifest and SHA-256 checksums
- Ctrl+C is translated into cooperative Runner cancellation
- Browser installation is an explicit step through `build\install-playwright-browsers.ps1`
- Versioned durable execution checkpoints exist through a provider-neutral host store contract
- Atomic integrity-protected filesystem checkpoint persistence exists
- Safe-boundary process-restart resume preserves completed node outputs and activation/event counters
- The Runner provides `resume` and opt-in `--checkpoint-directory` execution persistence
- Runtime-owned handler timeout, retry, bounded exponential backoff, and `onError` execution policies exist
- Retry attempts use distinct node identities, ordered events, and individual terminal results
- Checkpoint format 0.2 preserves safe retry boundaries without replaying the persisted failed attempt
- Checkpoint format 0.3 carries provider-versioned runtime-resource reconstruction state
- Ephemeral Playwright resources can restore bounded storage state, page identities, active page, and open-page URLs before resume continues
- Unsupported resource states fail closed instead of silently creating an empty replacement
- Bounded in-process scheduling executes eligible independent handler steps concurrently
- `flow.foreach` honors bounded parallel execution for eligible single-handler loop bodies
- Runtime events remain serialized and result collections remain plan ordered during parallel execution
- Strict JSON parsing, schema validation, and semantic validation are separate layers
- Loop execution is implemented for built-in loop boundaries
- Subworkflow invocation execution is implemented through explicit repository contracts
- Reachable cross-workflow dependency validation is implemented with exact-version resolution, input and stream compatibility checks, cycle detection, and bounded depth
- The Runner loads explicit local child workflow repositories through `--workflow-directory`
- Bounded Playwright network interception supports deterministic allow, block, request-header modification, and synthetic response fulfillment policies
- Explicit local plugin packages can contribute node definitions, exact handlers, and runtime resource providers through hash-verified closed manifests
- The Runner provides `plugins` inspection and repeatable `--plugin-directory` composition without global assembly scanning
- Windows desktop automation is implemented through an explicit FlaUI UIA3-backed `desktop.application` resource
- Essential `desktop.click`, `desktop.fill`, `desktop.press`, `desktop.getText`, and `desktop.getCount` handlers exist
- The Runner loads bounded explicit Locator Catalog directories through `--locator-directory`
- Interrupted running nodes require explicit recovery and are never automatically replayed
- Persistent Playwright profiles, pending browser dialogs, and desktop application handles are not resumable
- Human interaction is supported through explicit non-durable host handlers or in-memory session continuations
- Remote plugin discovery, package registries, dependency injection, and sandboxed plugin execution are not implemented yet
- Node catalog JSON Schema and conformance fixtures exist
- Browser automation is implemented for the essential `web.page` path only
- Playwright integration is implemented for page-owned browser/context/page resources
- FlaUI integration currently covers the essential UI Automation action, form, text, and count path only
- Durable parallel-frontier and distributed scheduling are not implemented yet

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

Phase 0-25 production verification in an interactive Windows session:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\verify-phase-025.ps1
```

Phase 25 reruns the full Phase 0-24 acceptance gate, verifies the real-pilot `core.end` regression, audits top-level and transitive NuGet packages for known vulnerabilities, validates release manifest/SHA-256 integrity, verifies the self-contained published Runner, and produces the versioned Windows release archive. Do not weaken CET, CFG, or Exploit Protection to make an apphost pass.

Phase 26 adds a GitHub-hosted clean-Windows production gate and destructive checks proving that tampered checkpoints, plugin assemblies, and release payloads fail closed before execution/trust.

Phase 27 adds a CycloneDX SBOM, an in-toto/SLSA provenance statement, cross-hash supply-chain verification, and a production Authenticode signing entry point without embedding signing secrets in the repository.

Phase 28 adds in-process Runner soak, repeated published-binary execution, real Chromium lifecycle churn, resource-growth thresholds, orphan-browser detection, and a machine-readable soak report.

Phase 29 adds a hash-closed Agent runtime bundle, blue/green staging and pointer promotion, active-slot task routing, rollback, published-binary safe-boundary crash recovery with Playwright page reconstruction, and explicit `SKR3006` fail-closed behavior for an interrupted in-flight handler.

Phase 0-28 stress/soak verification in an interactive Windows session:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\verify-phase-028.ps1
```

Phase 0-29 deployment/Agent canary verification in an interactive Windows session:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\verify-phase-029.ps1
```

Phase 30 freezes the first GA release identity (`0.1.0`), reruns Phase 0-29 against the final version, verifies stable checkpoint/artifact storage-failure codes, finalizes the GA evidence record, and freezes the `0.1.x` compatibility/support contract.

Phase 0-30 Final GA verification in an interactive Windows session:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\verify-phase-030.ps1
```

For public signed distribution, rerun the final gate with `-RequireSignedRelease` after signing the release with a production-trusted Authenticode certificate and timestamp service.

Final GA acceptance is documented in [Phase 30 Verification](docs/development/phase-030-verification.md), with the operational procedure in [GA Release Runbook](docs/development/ga-release-runbook.md) and the frozen contract in [Support Policy](docs/development/support-policy.md). Deployment and recovery acceptance rules are documented in [Phase 29 Verification](docs/development/phase-029-verification.md). The Windows host contract is documented in [Agent Host Integration 0.1](docs/development/agent-host-integration.md). Reliability rules remain documented in [Phase 28 Verification](docs/development/phase-028-verification.md), and supply-chain rules remain documented in [Phase 27 Verification](docs/development/phase-027-verification.md). GA readiness and external signing requirements are tracked in [Production Readiness](docs/development/production-readiness.md).

## Documentation

SkeletonKey uses DocFX as its documentation site generator. The exact DocFX version is pinned as a local .NET tool, so contributors do not need a global installation.

Build the documentation site:

```powershell
dotnet tool restore
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\verify-docs.ps1
```

Build and serve it locally:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\serve-docs.ps1
```

The static site is generated under `artifacts/docs/site`. The site includes conceptual documentation, versioned specifications, ADRs, configuration/environment-variable documentation, development/release records, and generated .NET API reference.

Architecture decisions are maintained under [`docs/architecture`](docs/architecture/index.md). New ADRs use [`adr-template.md`](docs/architecture/adr-template.md). Repository environment variables are inventoried in [`docs/configuration/environment-variables.md`](docs/configuration/environment-variables.md).

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
- [Compatibility Policy 0.1](docs/specifications/compatibility-policy-0.1.md)
- [Desktop Automation 0.1](docs/specifications/desktop-automation-0.1.md)
- [Local Plugin Package 0.1](docs/specifications/local-plugin-package-0.1.md)
- [Web Network Interception 0.1](docs/specifications/web-network-interception-0.1.md)
- [Workflow Invocation Analysis 0.1](docs/specifications/workflow-invocation-analysis-0.1.md)
- [Runtime Parallel Scheduling 0.1](docs/specifications/runtime-parallel-scheduling-0.1.md)
- [Runtime Execution Policies 0.1](docs/specifications/runtime-execution-policies-0.1.md)
- [Workflow 0.1 JSON Schema](schemas/workflow/0.1/schema.json)
- [Workflow Expressions 0.1](docs/specifications/workflow-expressions-0.1.md)
- [Workflow Expression Evaluation 0.1](docs/specifications/workflow-expression-evaluation-0.1.md)
- [Workflow Binding Resolution 0.1](docs/specifications/workflow-binding-resolution-0.1.md)
- [Workflow Value Resolution Context 0.1](docs/specifications/workflow-value-resolution-context-0.1.md)
- [Workflow Value Materialization 0.1](docs/specifications/workflow-value-materialization-0.1.md)
- [Node Parameter Materialization 0.1](docs/specifications/node-parameter-materialization-0.1.md)
- [Workflow Runtime 0.1](docs/specifications/workflow-runtime-0.1.md)
- [Durable Workflow Checkpoints 0.1](docs/specifications/durable-workflow-checkpoints-0.1.md)
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
