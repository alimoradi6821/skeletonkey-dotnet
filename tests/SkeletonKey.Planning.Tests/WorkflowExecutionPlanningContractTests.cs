using SkeletonKey.Planning;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Planning.Tests;

/// <summary>
/// Covers host-neutral execution planning contracts.
/// </summary>
public sealed class WorkflowExecutionPlanningContractTests
{
    /// <summary>
    /// Verifies plan contracts defensively copy steps, dependencies, resources, and capabilities.
    /// </summary>
    [Fact]
    public void ExecutionPlanDefensivelyCopiesCollections()
    {
        List<WorkflowExecutionPlanDependency> dependencies = [new("start", WorkflowExecutionPlanDependencyKind.Control)];
        List<WorkflowExecutionPlanResourceUse> uses = [new("browser", "browser", WorkflowResourceAccessMode.Exclusive)];
        WorkflowExecutionPlanStep step = new("step-log", "log", "core.log", 1, dependencies, uses);
        dependencies.Add(new("other"));
        uses.Clear();

        List<string> capabilities = [StandardWorkflowResourceCapabilities.WebFrames];
        WorkflowExecutionPlanResource resource = new(
            "browser",
            StandardWorkflowResourceKinds.WebBrowser,
            WorkflowResourceLifetime.Invocation,
            WorkflowResourceAccessMode.Exclusive,
            true,
            capabilities);
        capabilities.Add(StandardWorkflowResourceCapabilities.WebPersistentProfile);

        List<WorkflowExecutionPlanStep> steps = [step];
        List<WorkflowExecutionPlanResource> resources = [resource];
        Dictionary<string, string> nodeStepMap = new(StringComparer.Ordinal)
        {
            ["log"] = "step-log",
        };
        List<string> entrySteps = ["step-start"];
        List<string> terminalSteps = ["step-log"];
        WorkflowExecutionPlan plan = new("plan", "workflow", "0.1.0", "catalog", "1.0.0", steps, resources, nodeStepMap, entrySteps, terminalSteps);
        steps.Clear();
        resources.Clear();
        nodeStepMap.Clear();
        entrySteps.Clear();
        terminalSteps.Clear();

        Assert.Equal("start", step.DependsOn[0].StepId);
        Assert.Single(step.Resources);
        Assert.Single(plan.Steps);
        Assert.Single(plan.Resources);
        Assert.Equal("catalog", plan.CatalogId);
        Assert.Equal("1.0.0", plan.CatalogVersion);
        Assert.Equal("step-log", plan.NodeStepMap["log"]);
        Assert.Equal(["step-start"], plan.EntryStepIds);
        Assert.Equal(["step-log"], plan.TerminalStepIds);
        Assert.Equal([StandardWorkflowResourceCapabilities.WebFrames], resource.Capabilities);
    }

    /// <summary>
    /// Verifies blocked planning results can carry issues without a plan.
    /// </summary>
    [Fact]
    public void BlockedPlanningResultCanCarryIssuesWithoutPlan()
    {
        WorkflowExecutionPlanResult result = new(
            "workflow",
            WorkflowExecutionPlanStatus.Blocked,
            issues:
            [
                new(WorkflowExecutionPlanCodes.AnalysisErrors, WorkflowExecutionPlanIssueSeverity.Error, "Analysis failed.", ""),
            ]);

        Assert.False(result.IsReady);
        Assert.Null(result.Plan);
        Assert.Single(result.Errors);
    }

    /// <summary>
    /// Verifies ready planning results expose the generated plan.
    /// </summary>
    [Fact]
    public void ReadyPlanningResultExposesPlan()
    {
        WorkflowExecutionPlan plan = new("plan", "workflow", "0.1.0");
        WorkflowExecutionPlanResult result = new("workflow", WorkflowExecutionPlanStatus.Ready, plan);

        Assert.True(result.IsReady);
        Assert.Same(plan, result.Plan);
    }

    /// <summary>
    /// Verifies plan steps preserve definition keys and boundary metadata.
    /// </summary>
    [Fact]
    public void PlanStepPreservesDefinitionKeyAndBoundaryMetadata()
    {
        WorkflowExecutionPlanBoundary boundary = new("loop-1", "loop");
        WorkflowExecutionPlanStep step = new(
            "step",
            "request",
            "interaction.request",
            1,
            kind: WorkflowExecutionPlanStepKind.Interaction,
            maySuspend: true,
            terminal: false,
            loopBoundary: boundary);

        Assert.Equal("interaction.request", step.DefinitionKey.Type);
        Assert.Equal(1, step.DefinitionKey.Version);
        Assert.Equal(WorkflowExecutionPlanStepKind.Interaction, step.Kind);
        Assert.True(step.MaySuspend);
        Assert.Same(boundary, step.LoopBoundary);
    }
}
