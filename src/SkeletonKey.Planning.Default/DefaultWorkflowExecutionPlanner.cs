using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Analysis;
using SkeletonKey.Binding;
using SkeletonKey.Catalog;
using SkeletonKey.Expressions;
using SkeletonKey.Workflow.Bindings;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Planning.Default;

/// <summary>
/// Converts catalog-aware workflow analysis into immutable host-neutral execution-plan metadata.
/// </summary>
/// <remarks>
/// The planner is deterministic, thread-safe, and directly constructible. It records steps,
/// dependencies, resource uses, and boundary metadata only; it never executes workflows, materializes
/// parameter values, resolves live resources, dispatches events, or invokes handlers.
/// </remarks>
public sealed class DefaultWorkflowExecutionPlanner : IWorkflowExecutionPlanner
{
    private readonly ExecutionPlanningOptions _options;
    private readonly WorkflowBindingReader _bindingReader = new();
    private readonly WorkflowExpressionReader _expressionReader = new();
    private readonly WorkflowExpressionParser _expressionParser = new();

    /// <summary>
    /// Initializes the default planner.
    /// </summary>
    /// <param name="options">Optional immutable deterministic planning options.</param>
    public DefaultWorkflowExecutionPlanner(ExecutionPlanningOptions? options = null)
    {
        _options = options ?? ExecutionPlanningOptions.Default;
    }

    /// <inheritdoc />
    public WorkflowExecutionPlanResult Plan(WorkflowDocument workflow, WorkflowAnalysisResult analysis)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(analysis);

        IssueSink issues = new(_options.MaximumIssues);
        if (!string.Equals(workflow.Id, analysis.WorkflowId, StringComparison.Ordinal))
        {
            issues.Add(new(
                WorkflowExecutionPlanCodes.AnalysisWorkflowMismatch,
                WorkflowExecutionPlanIssueSeverity.Error,
                "The analysis result does not describe the supplied workflow.",
                "/id"));
        }

        if (!analysis.CanPlanExecution)
        {
            issues.Add(new(
                WorkflowExecutionPlanCodes.AnalysisErrors,
                WorkflowExecutionPlanIssueSeverity.Error,
                "Planning requires a catalog-aware analysis result without errors.",
                string.Empty));
        }

        IReadOnlyList<WorkflowNodeAnalysis> enabledAnalyses = analysis.Nodes
            .Where(static node => !node.Disabled)
            .ToArray();

        if (enabledAnalyses.Count > _options.MaximumSteps)
        {
            issues.Add(new(
                WorkflowExecutionPlanCodes.PlanLimitExceeded,
                WorkflowExecutionPlanIssueSeverity.Error,
                "The planned workflow exceeds the configured step limit.",
                "/nodes"));
        }

        var workflowNodes = workflow.Nodes.ToDictionary(static node => node.Id, static node => node, StringComparer.Ordinal);
        var analysisByNode = analysis.Nodes.ToDictionary(static node => node.NodeId, static node => node, StringComparer.Ordinal);
        Dictionary<string, string> nodeStepMap = new(StringComparer.Ordinal);
        foreach (WorkflowNodeAnalysis node in enabledAnalyses)
        {
            if (node.CatalogStatus != WorkflowNodeCatalogStatus.Known || node.Definition is null || node.EffectivePorts.Count == 0)
            {
                issues.Add(new(
                    WorkflowExecutionPlanCodes.MissingNodeDefinition,
                    WorkflowExecutionPlanIssueSeverity.Error,
                    $"Node '{node.NodeId}' is not fully resolved by analysis.",
                    NodePath(workflow, node.NodeId),
                    node.NodeId));
            }

            nodeStepMap[node.NodeId] = StepId(node.NodeId);
        }

        DependencyBuilder dependencies = new(_options.MaximumDependencies, issues);
        AddConnectionDependencies(analysis, nodeStepMap, dependencies);
        AddBindingDependencies(workflow, analysisByNode, nodeStepMap, dependencies, issues);
        AddExpressionDependencies(workflow, analysisByNode, nodeStepMap, dependencies, issues);
        AnalyzeCycles(analysisByNode, dependencies.Items, issues);

        List<WorkflowExecutionPlanStep> steps = BuildSteps(workflow, enabledAnalyses, nodeStepMap, dependencies.Items);
        IReadOnlyList<WorkflowExecutionPlanResource> resources = BuildPlanResources(workflow);
        IReadOnlyList<string> entryStepIds = steps
            .Where(static step => step.Kind == WorkflowExecutionPlanStepKind.Entry)
            .Select(static step => step.StepId)
            .ToArray();
        IReadOnlyList<string> terminalStepIds = steps
            .Where(static step => step.Terminal)
            .Select(static step => step.StepId)
            .ToArray();

        if (entryStepIds.Count == 0)
        {
            issues.Add(new(WorkflowExecutionPlanCodes.MissingEntryStep, WorkflowExecutionPlanIssueSeverity.Error, "No core workflow entry step was identified.", "/nodes"));
        }

        if (terminalStepIds.Count == 0)
        {
            issues.Add(new(WorkflowExecutionPlanCodes.MissingTerminalPath, WorkflowExecutionPlanIssueSeverity.Warning, "No explicit terminal step was identified.", "/nodes"));
        }

        IReadOnlyList<WorkflowExecutionPlanIssue> orderedIssues = OrderIssues(issues.Issues);
        if (orderedIssues.Any(static issue => issue.Severity == WorkflowExecutionPlanIssueSeverity.Error))
        {
            return new WorkflowExecutionPlanResult(workflow.Id, WorkflowExecutionPlanStatus.Blocked, issues: orderedIssues);
        }

        WorkflowExecutionPlan plan = new(
            PlanId(workflow, analysis),
            workflow.Id,
            workflow.SpecVersion,
            analysis.CatalogId,
            analysis.CatalogVersion,
            steps,
            resources,
            nodeStepMap,
            entryStepIds,
            terminalStepIds,
            dependencies.Items);

        return new WorkflowExecutionPlanResult(workflow.Id, WorkflowExecutionPlanStatus.Ready, plan, orderedIssues);
    }

    private static List<WorkflowExecutionPlanStep> BuildSteps(
        WorkflowDocument workflow,
        IReadOnlyList<WorkflowNodeAnalysis> analyses,
        IReadOnlyDictionary<string, string> nodeStepMap,
        IReadOnlyList<WorkflowExecutionPlanDependency> dependencies)
    {
        Dictionary<string, List<WorkflowExecutionPlanDependency>> byTarget = new(StringComparer.Ordinal);
        foreach (WorkflowExecutionPlanDependency dependency in dependencies)
        {
            if (dependency.TargetStepId is null)
            {
                continue;
            }

            if (!byTarget.TryGetValue(dependency.TargetStepId, out List<WorkflowExecutionPlanDependency>? list))
            {
                list = [];
                byTarget.Add(dependency.TargetStepId, list);
            }

            list.Add(dependency);
        }

        var workflowNodes = workflow.Nodes.ToDictionary(static node => node.Id, static node => node, StringComparer.Ordinal);
        List<WorkflowExecutionPlanStep> steps = [];
        foreach (WorkflowNodeAnalysis analysis in analyses)
        {
            string stepId = nodeStepMap[analysis.NodeId];
            WorkflowNode node = workflowNodes[analysis.NodeId];
            WorkflowNodeDefinition definition = analysis.Definition!;
            IReadOnlyList<WorkflowExecutionPlanDependency> dependsOn = byTarget.TryGetValue(stepId, out List<WorkflowExecutionPlanDependency>? stepDependencies)
                ? Array.AsReadOnly([.. stepDependencies.OrderBy(static item => item.StepId, StringComparer.Ordinal).ThenBy(static item => item.Kind).ThenBy(static item => item.SourcePort, StringComparer.Ordinal)])
                : Array.AsReadOnly(Array.Empty<WorkflowExecutionPlanDependency>());
            IReadOnlyList<WorkflowExecutionPlanResourceUse> resourceUses = analysis.ResourceSlots
                .Where(static slot => slot.WorkflowResourceName is not null && slot.Status == WorkflowResourceRequirementAnalysisStatus.Satisfied)
                .Select(static slot => new WorkflowExecutionPlanResourceUse(
                    slot.WorkflowResourceName!,
                    slot.SlotName,
                    slot.Access ?? WorkflowResourceAccessMode.Exclusive,
                    slot.RequiredKind,
                    slot.Required,
                    slot.RequiredCapabilities))
                .ToArray();
            IReadOnlyList<NodeLocatorUse> locatorUses = analysis.LocatorSlots
                .Where(static slot => slot.Reference is not null && slot.Status == WorkflowLocatorSlotAnalysisStatus.Satisfied)
                .Select(slot => new NodeLocatorUse(stepId, analysis.NodeId, slot.SlotName, slot.Reference!, slot.ResolvedLocator, slot.Usage, slot.Required))
                .ToArray();

            steps.Add(new(
                stepId,
                analysis.NodeId,
                analysis.NodeType,
                analysis.TypeVersion,
                dependsOn,
                resourceUses,
                StepKind(definition.Behavior.Kind),
                definition.Behavior.MaySuspend,
                definition.Behavior.Terminal,
                ControlBoundary(node, analysis),
                InvocationBoundary(node, analysis),
                LoopBoundary(node, analysis),
                locatorUses));
        }

        return steps;
    }

    private static WorkflowExecutionPlanStepKind StepKind(WorkflowNodeBehaviorKind kind)
    {
        return kind switch
        {
            WorkflowNodeBehaviorKind.Entry => WorkflowExecutionPlanStepKind.Entry,
            WorkflowNodeBehaviorKind.Terminal => WorkflowExecutionPlanStepKind.Terminal,
            WorkflowNodeBehaviorKind.Branch => WorkflowExecutionPlanStepKind.Control,
            WorkflowNodeBehaviorKind.Loop => WorkflowExecutionPlanStepKind.Loop,
            WorkflowNodeBehaviorKind.Invocation => WorkflowExecutionPlanStepKind.Invocation,
            WorkflowNodeBehaviorKind.Interaction => WorkflowExecutionPlanStepKind.Interaction,
            _ => WorkflowExecutionPlanStepKind.Action,
        };
    }

    private static WorkflowExecutionPlanBoundary? ControlBoundary(WorkflowNode node, WorkflowNodeAnalysis analysis)
    {
        if (analysis.Definition?.Behavior.Kind is not (WorkflowNodeBehaviorKind.Branch or WorkflowNodeBehaviorKind.Terminal or WorkflowNodeBehaviorKind.Interaction))
        {
            return null;
        }

        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["nodeType"] = analysis.NodeType,
            ["outputs"] = string.Join(",", analysis.EffectivePorts.Where(static port => port.Direction == WorkflowPortDirection.Output).Select(static port => port.Id)),
        };
        if (analysis.Definition.Behavior.Terminal)
        {
            metadata["terminal"] = "true";
        }

        if (analysis.Definition.Behavior.MaySuspend)
        {
            metadata["maySuspend"] = "true";
        }

        return new($"boundary:{node.Id}", analysis.Definition.Behavior.Kind.ToString(), metadata: metadata);
    }

    private static WorkflowExecutionPlanBoundary? LoopBoundary(WorkflowNode node, WorkflowNodeAnalysis analysis)
    {
        if (analysis.Definition?.Behavior.Kind != WorkflowNodeBehaviorKind.Loop)
        {
            return null;
        }

        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["iterationId"] = node.Id,
            ["bodyOutput"] = "body",
            ["completedOutput"] = "completed",
            ["continueInput"] = "continue",
            ["breakInput"] = "break",
        };
        return new($"loop:{node.Id}", "loop", metadata: metadata);
    }

    private static WorkflowExecutionPlanBoundary? InvocationBoundary(WorkflowNode node, WorkflowNodeAnalysis analysis)
    {
        if (!string.Equals(analysis.NodeType, "workflow.invoke", StringComparison.Ordinal))
        {
            return null;
        }

        JsonObject parameters = node.Parameters;
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["resultPort"] = "result",
            ["opaqueChildWorkflow"] = "true",
        };

        if (parameters.TryGetPropertyValue("workflow", out JsonNode? workflowNode) && workflowNode is JsonObject workflowRef)
        {
            if (workflowRef.TryGetPropertyValue("id", out JsonNode? idNode) && idNode is not null && idNode.GetValueKind() == JsonValueKind.String)
            {
                metadata["workflowId"] = idNode.GetValue<string>();
            }

            if (workflowRef.TryGetPropertyValue("version", out JsonNode? versionNode) && versionNode is not null && versionNode.GetValueKind() == JsonValueKind.String)
            {
                metadata["workflowVersion"] = versionNode.GetValue<string>();
            }
        }

        if (parameters.TryGetPropertyValue("streams", out JsonNode? streamsNode) && streamsNode is JsonObject streams &&
            streams.TryGetPropertyValue("mode", out JsonNode? modeNode) && modeNode is not null && modeNode.GetValueKind() == JsonValueKind.String)
        {
            metadata["streamMode"] = modeNode.GetValue<string>();
        }

        return new($"invoke:{node.Id}", "invocation", metadata: metadata);
    }

    private static IReadOnlyList<WorkflowExecutionPlanResource> BuildPlanResources(WorkflowDocument workflow)
    {
        return workflow.Resources
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => new WorkflowExecutionPlanResource(
                pair.Key,
                pair.Value.Kind,
                pair.Value.Lifetime,
                pair.Value.Access,
                pair.Value.Required,
                pair.Value.Capabilities))
            .ToArray();
    }

    private static void AddConnectionDependencies(
        WorkflowAnalysisResult analysis,
        IReadOnlyDictionary<string, string> nodeStepMap,
        DependencyBuilder dependencies)
    {
        foreach (WorkflowConnectionAnalysis connection in analysis.Connections)
        {
            if (connection.RoleCompatibilityStatus != WorkflowConnectionRoleCompatibilityStatus.Compatible ||
                connection.SourcePort is null ||
                connection.TargetPort is null ||
                !nodeStepMap.TryGetValue(connection.FromNode, out string? sourceStep) ||
                !nodeStepMap.TryGetValue(connection.ToNode, out string? targetStep))
            {
                continue;
            }

            WorkflowExecutionPlanDependencyKind kind = connection.SourcePort.Roles.Contains("control", StringComparer.Ordinal) &&
                connection.TargetPort.Roles.Contains("control", StringComparer.Ordinal)
                    ? WorkflowExecutionPlanDependencyKind.Control
                    : WorkflowExecutionPlanDependencyKind.Data;
            dependencies.Add(new(sourceStep, kind, connection.FromPort, connection.ToPort, targetStep, ConnectionPath(connection.ConnectionIndex)));
        }
    }

    private void AddBindingDependencies(
        WorkflowDocument workflow,
        IReadOnlyDictionary<string, WorkflowNodeAnalysis> analysisByNode,
        IReadOnlyDictionary<string, string> nodeStepMap,
        DependencyBuilder dependencies,
        IssueSink issues)
    {
        for (int index = 0; index < workflow.Nodes.Count; index++)
        {
            WorkflowNode consumer = workflow.Nodes[index];
            if (!nodeStepMap.TryGetValue(consumer.Id, out string? targetStep))
            {
                continue;
            }

            foreach (WorkflowBindingOccurrence occurrence in _bindingReader.FindBindings(consumer.Parameters))
            {
                if (occurrence.Binding.Source != WorkflowBindingSource.Node)
                {
                    continue;
                }

                AddDataDependency(
                    analysisByNode,
                    nodeStepMap,
                    dependencies,
                    issues,
                    occurrence.Binding.Node,
                    occurrence.Binding.Port,
                    targetStep,
                    Pointer("nodes", index, "parameters") + occurrence.Path,
                    consumer.Id);
            }
        }
    }

    private void AddExpressionDependencies(
        WorkflowDocument workflow,
        IReadOnlyDictionary<string, WorkflowNodeAnalysis> analysisByNode,
        IReadOnlyDictionary<string, string> nodeStepMap,
        DependencyBuilder dependencies,
        IssueSink issues)
    {
        for (int index = 0; index < workflow.Nodes.Count; index++)
        {
            WorkflowNode consumer = workflow.Nodes[index];
            if (!nodeStepMap.TryGetValue(consumer.Id, out string? targetStep))
            {
                continue;
            }

            foreach (WorkflowExpressionOccurrence occurrence in _expressionReader.FindExpressions(consumer.Parameters))
            {
                WorkflowExpressionDocument document = _expressionParser.Parse(occurrence.Text);
                foreach (WorkflowExpressionReference reference in document.References.Where(static reference => reference.Kind == WorkflowExpressionReferenceKind.Node))
                {
                    AddDataDependency(
                        analysisByNode,
                        nodeStepMap,
                        dependencies,
                        issues,
                        reference.NodeId,
                        reference.Port,
                        targetStep,
                        Pointer("nodes", index, "parameters") + occurrence.Path,
                        consumer.Id);
                }
            }
        }
    }

    private static void AddDataDependency(
        IReadOnlyDictionary<string, WorkflowNodeAnalysis> analysisByNode,
        IReadOnlyDictionary<string, string> nodeStepMap,
        DependencyBuilder dependencies,
        IssueSink issues,
        string? sourceNode,
        string? sourcePort,
        string targetStep,
        string sourcePath,
        string consumerNodeId)
    {
        if (sourceNode is null || sourcePort is null ||
            !analysisByNode.TryGetValue(sourceNode, out WorkflowNodeAnalysis? sourceAnalysis) ||
            !nodeStepMap.TryGetValue(sourceNode, out string? sourceStep) ||
            !sourceAnalysis.EffectivePorts.Any(port => port.Direction == WorkflowPortDirection.Output && string.Equals(port.Id, sourcePort, StringComparison.Ordinal)))
        {
            issues.Add(new(
                WorkflowExecutionPlanCodes.UnresolvedDataDependency,
                WorkflowExecutionPlanIssueSeverity.Error,
                "A binding or expression dependency references an unresolved node output.",
                sourcePath,
                consumerNodeId));
            return;
        }

        dependencies.Add(new(sourceStep, WorkflowExecutionPlanDependencyKind.Data, sourcePort, null, targetStep, sourcePath));
    }

    private static void AnalyzeCycles(
        IReadOnlyDictionary<string, WorkflowNodeAnalysis> analyses,
        IReadOnlyList<WorkflowExecutionPlanDependency> dependencies,
        IssueSink issues)
    {
        Dictionary<string, List<string>> adjacency = new(StringComparer.Ordinal);
        foreach (WorkflowNodeAnalysis analysis in analyses.Values)
        {
            adjacency[StepId(analysis.NodeId)] = [];
        }

        foreach (WorkflowExecutionPlanDependency dependency in dependencies)
        {
            if (dependency.TargetStepId is null || IsStructuredLoopBackEdge(analyses, dependency))
            {
                continue;
            }

            if (adjacency.TryGetValue(dependency.StepId, out List<string>? edges))
            {
                edges.Add(dependency.TargetStepId);
            }
        }

        Dictionary<string, VisitState> state = new(StringComparer.Ordinal);
        foreach (string step in adjacency.Keys.OrderBy(static value => value, StringComparer.Ordinal))
        {
            if (DetectCycle(step, adjacency, state))
            {
                issues.Add(new(
                    WorkflowExecutionPlanCodes.DependencyCycle,
                    WorkflowExecutionPlanIssueSeverity.Error,
                    "An unstructured dependency cycle was detected.",
                    "/connections",
                    step));
                return;
            }
        }
    }

    private static bool IsStructuredLoopBackEdge(
        IReadOnlyDictionary<string, WorkflowNodeAnalysis> analyses,
        WorkflowExecutionPlanDependency dependency)
    {
        if (dependency.TargetStepId is null || dependency.Kind != WorkflowExecutionPlanDependencyKind.Control)
        {
            return false;
        }

        string targetNodeId = dependency.TargetStepId.StartsWith("node:", StringComparison.Ordinal)
            ? dependency.TargetStepId["node:".Length..]
            : dependency.TargetStepId;
        return analyses.TryGetValue(targetNodeId, out WorkflowNodeAnalysis? target) &&
            target.Definition?.Behavior.Kind == WorkflowNodeBehaviorKind.Loop &&
            dependency.TargetPort is "continue" or "break";
    }

    private static bool DetectCycle(
        string start,
        IReadOnlyDictionary<string, List<string>> adjacency,
        Dictionary<string, VisitState> state)
    {
        Stack<(string Step, int Index)> stack = [];
        stack.Push((start, 0));
        while (stack.Count > 0)
        {
            (string step, int index) = stack.Pop();
            if (!state.TryGetValue(step, out VisitState current))
            {
                state[step] = VisitState.Visiting;
                stack.Push((step, 0));
                foreach (string next in adjacency[step].OrderByDescending(static value => value, StringComparer.Ordinal))
                {
                    if (state.TryGetValue(next, out VisitState nextState) && nextState == VisitState.Visiting)
                    {
                        return true;
                    }

                    if (!state.ContainsKey(next))
                    {
                        stack.Push((next, 0));
                    }
                }
            }
            else if (current == VisitState.Visiting && index == 0)
            {
                state[step] = VisitState.Visited;
            }
        }

        return false;
    }

    private static IReadOnlyList<WorkflowExecutionPlanIssue> OrderIssues(IReadOnlyList<WorkflowExecutionPlanIssue> issues)
    {
        return Array.AsReadOnly([.. issues
            .OrderBy(static issue => issue.Path, StringComparer.Ordinal)
            .ThenBy(static issue => issue.Code, StringComparer.Ordinal)
            .ThenBy(static issue => issue.NodeId, StringComparer.Ordinal)]);
    }

    private static string StepId(string nodeId)
    {
        return "node:" + nodeId;
    }

    private static string PlanId(WorkflowDocument workflow, WorkflowAnalysisResult analysis)
    {
        return $"plan:{workflow.Id}:{workflow.SpecVersion}:{analysis.CatalogId ?? "catalog"}:{analysis.CatalogVersion ?? "unversioned"}";
    }

    private static string NodePath(WorkflowDocument workflow, string nodeId)
    {
        for (int index = 0; index < workflow.Nodes.Count; index++)
        {
            if (string.Equals(workflow.Nodes[index].Id, nodeId, StringComparison.Ordinal))
            {
                return Pointer("nodes", index);
            }
        }

        return "/nodes";
    }

    private static string ConnectionPath(int? connectionIndex)
    {
        return connectionIndex is null ? "/connections" : Pointer("connections", connectionIndex.Value);
    }

    private static string Pointer(params object[] parts)
    {
        return "/" + string.Join("/", parts.Select(static part => Escape(Convert.ToString(part, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)));
    }

    private static string Escape(string value)
    {
        return value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
    }

    private enum VisitState
    {
        Visiting,
        Visited,
    }

    private sealed class DependencyBuilder
    {
        private readonly HashSet<DependencyKey> _keys = [];
        private readonly int _maximumDependencies;
        private readonly IssueSink _issues;

        public DependencyBuilder(int maximumDependencies, IssueSink issues)
        {
            _maximumDependencies = maximumDependencies;
            _issues = issues;
        }

        public List<WorkflowExecutionPlanDependency> Items { get; } = [];

        public void Add(WorkflowExecutionPlanDependency dependency)
        {
            DependencyKey key = new(dependency.StepId, dependency.TargetStepId, dependency.Kind, dependency.SourcePort, dependency.TargetPort);
            if (!_keys.Add(key))
            {
                return;
            }

            if (Items.Count >= _maximumDependencies)
            {
                _issues.Add(new(
                    WorkflowExecutionPlanCodes.PlanLimitExceeded,
                    WorkflowExecutionPlanIssueSeverity.Error,
                    "The planned workflow exceeds the configured dependency limit.",
                    string.Empty));
                return;
            }

            Items.Add(dependency);
        }
    }

    private sealed class IssueSink
    {
        private readonly int _maximumIssues;
        private bool _limitReported;

        public IssueSink(int maximumIssues)
        {
            _maximumIssues = maximumIssues;
        }

        public List<WorkflowExecutionPlanIssue> Issues { get; } = [];

        public void Add(WorkflowExecutionPlanIssue issue)
        {
            if (Issues.Count < _maximumIssues)
            {
                Issues.Add(issue);
                return;
            }

            if (_limitReported)
            {
                return;
            }

            _limitReported = true;
            Issues.Add(new(
                WorkflowExecutionPlanCodes.PlanLimitExceeded,
                WorkflowExecutionPlanIssueSeverity.Error,
                "The planner reached the configured diagnostic limit.",
                string.Empty));
        }
    }

    private readonly record struct DependencyKey(
        string SourceStepId,
        string? TargetStepId,
        WorkflowExecutionPlanDependencyKind Kind,
        string? SourcePort,
        string? TargetPort);
}
