using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Inputs;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Outputs;
using SkeletonKey.Workflow.References;

namespace SkeletonKey.Runtime.Invocation;

/// <summary>Resolves and validates the complete reachable workflow invocation graph.</summary>
public sealed class WorkflowInvocationGraphAnalyzer
{
    /// <summary>Analyzes reachable invocation dependencies from one root workflow.</summary>
    public async ValueTask<WorkflowInvocationAnalysisResult> AnalyzeAsync(
        WorkflowDocument root,
        IWorkflowRepository repository,
        WorkflowInvocationAnalysisOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(repository);
        WorkflowInvocationAnalysisOptions effectiveOptions = options ?? new WorkflowInvocationAnalysisOptions();
        List<WorkflowInvocationDependency> dependencies = [];
        List<WorkflowInvocationAnalysisIssue> issues = [];
        HashSet<string> expanded = new(StringComparer.Ordinal);
        List<string> activePath = [root.Id];
        expanded.Add(root.Id);

        await VisitAsync(root, 0, repository, effectiveOptions, dependencies, issues, expanded, activePath, cancellationToken).ConfigureAwait(false);
        return new WorkflowInvocationAnalysisResult(dependencies, issues);
    }

    private static async ValueTask VisitAsync(
        WorkflowDocument workflow,
        int depth,
        IWorkflowRepository repository,
        WorkflowInvocationAnalysisOptions options,
        List<WorkflowInvocationDependency> dependencies,
        List<WorkflowInvocationAnalysisIssue> issues,
        HashSet<string> expanded,
        List<string> activePath,
        CancellationToken cancellationToken)
    {
        HashSet<string> reachable = ReachableNodeIds(workflow);
        for (int index = 0; index < workflow.Nodes.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WorkflowNode node = workflow.Nodes[index];
            if (node.Disabled || !reachable.Contains(node.Id) || !string.Equals(node.Type, "workflow.invoke", StringComparison.Ordinal))
            {
                continue;
            }

            string basePath = "/nodes/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/parameters";
            JsonObject parameters = node.Parameters;
            WorkflowReference? reference = ReadReference(parameters["workflow"]);
            if (reference is null)
            {
                continue;
            }

            WorkflowRepositoryLookupResult lookup;
            try
            {
                lookup = await repository.LookupAsync(reference, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                issues.Add(new WorkflowInvocationAnalysisIssue(
                    WorkflowInvocationAnalysisCodes.RepositoryFailure,
                    "The workflow repository failed while resolving an invocation dependency.",
                    workflow.Id,
                    node.Id,
                    basePath + "/workflow"));
                continue;
            }

            if (!lookup.Found || lookup.Workflow is null)
            {
                issues.Add(new WorkflowInvocationAnalysisIssue(
                    WorkflowInvocationAnalysisCodes.WorkflowNotFound,
                    lookup.Diagnostic ?? "The referenced workflow was not found.",
                    workflow.Id,
                    node.Id,
                    basePath + "/workflow"));
                continue;
            }

            WorkflowDocument child = lookup.Workflow;
            int childDepth = depth + 1;
            dependencies.Add(new WorkflowInvocationDependency(workflow.Id, node.Id, reference, child.Id, childDepth));
            if (!string.Equals(child.Id, reference.Id, StringComparison.Ordinal))
            {
                issues.Add(new WorkflowInvocationAnalysisIssue(
                    WorkflowInvocationAnalysisCodes.WorkflowIdentityMismatch,
                    "The resolved workflow identifier does not match the declared reference.",
                    workflow.Id,
                    node.Id,
                    basePath + "/workflow/id"));
                continue;
            }

            ValidateInputs(workflow, node, child, parameters["inputs"] as JsonObject, basePath, issues);
            ValidateStreamMappings(workflow, node, child, parameters["streams"] as JsonObject, basePath, issues);

            if (childDepth > options.MaximumDepth)
            {
                issues.Add(new WorkflowInvocationAnalysisIssue(
                    WorkflowInvocationAnalysisCodes.InvocationDepthExceeded,
                    "The workflow invocation graph exceeds the configured maximum depth.",
                    workflow.Id,
                    node.Id,
                    basePath + "/workflow"));
                continue;
            }

            if (activePath.Contains(child.Id, StringComparer.Ordinal))
            {
                issues.Add(new WorkflowInvocationAnalysisIssue(
                    WorkflowInvocationAnalysisCodes.InvocationCycle,
                    "The workflow invocation graph contains a recursion cycle.",
                    workflow.Id,
                    node.Id,
                    basePath + "/workflow"));
                continue;
            }

            if (!expanded.Add(child.Id))
            {
                continue;
            }

            activePath.Add(child.Id);
            await VisitAsync(child, childDepth, repository, options, dependencies, issues, expanded, activePath, cancellationToken).ConfigureAwait(false);
            activePath.RemoveAt(activePath.Count - 1);
        }
    }

    private static HashSet<string> ReachableNodeIds(WorkflowDocument workflow)
    {
        HashSet<string> reachable = new(StringComparer.Ordinal);
        Queue<string> pending = new(workflow.Nodes
            .Where(static node => !node.Disabled && string.Equals(node.Type, "core.start", StringComparison.Ordinal))
            .Select(static node => node.Id));
        while (pending.TryDequeue(out string? nodeId))
        {
            if (!reachable.Add(nodeId))
            {
                continue;
            }

            foreach (string target in workflow.Connections
                .Where(connection => string.Equals(connection.From.Node, nodeId, StringComparison.Ordinal))
                .Select(static connection => connection.To.Node))
            {
                pending.Enqueue(target);
            }
        }

        return reachable;
    }

    private static WorkflowReference? ReadReference(JsonNode? value)
    {
        if (value is not JsonObject reference ||
            reference["id"]?.GetValueKind() != JsonValueKind.String)
        {
            return null;
        }

        string id = reference["id"]!.GetValue<string>();
        string? version = reference["version"]?.GetValueKind() == JsonValueKind.String
            ? reference["version"]!.GetValue<string>()
            : null;
        return new WorkflowReference(id, version);
    }

    private static void ValidateInputs(
        WorkflowDocument parent,
        WorkflowNode node,
        WorkflowDocument child,
        JsonObject? supplied,
        string basePath,
        List<WorkflowInvocationAnalysisIssue> issues)
    {
        JsonObject inputs = supplied ?? [];
        foreach (KeyValuePair<string, WorkflowInputDefinition> input in child.Inputs)
        {
            if (input.Value.Required && !input.Value.HasDefault && !inputs.ContainsKey(input.Key))
            {
                issues.Add(new WorkflowInvocationAnalysisIssue(
                    WorkflowInvocationAnalysisCodes.RequiredChildInputMissing,
                    "A required child workflow input is not supplied.",
                    parent.Id,
                    node.Id,
                    basePath + "/inputs/" + Escape(input.Key)));
            }
        }

        foreach (KeyValuePair<string, JsonNode?> input in inputs)
        {
            string path = basePath + "/inputs/" + Escape(input.Key);
            if (!child.Inputs.TryGetValue(input.Key, out WorkflowInputDefinition? definition))
            {
                issues.Add(new WorkflowInvocationAnalysisIssue(
                    WorkflowInvocationAnalysisCodes.UnknownChildInput,
                    "The invocation supplies an input not declared by the child workflow.",
                    parent.Id,
                    node.Id,
                    path));
                continue;
            }

            if (TryGetStaticValue(input.Value, out JsonNode? staticValue) && !MatchesType(staticValue, definition.Type))
            {
                issues.Add(new WorkflowInvocationAnalysisIssue(
                    WorkflowInvocationAnalysisCodes.ChildInputTypeMismatch,
                    "The static invocation input is incompatible with the declared child input type.",
                    parent.Id,
                    node.Id,
                    path));
            }
        }
    }

    private static void ValidateStreamMappings(
        WorkflowDocument parent,
        WorkflowNode node,
        WorkflowDocument child,
        JsonObject? streams,
        string basePath,
        List<WorkflowInvocationAnalysisIssue> issues)
    {
        if (streams?["mode"]?.GetValueKind() != JsonValueKind.String ||
            !string.Equals(streams["mode"]!.GetValue<string>(), "map", StringComparison.Ordinal) ||
            streams["mappings"] is not JsonObject mappings)
        {
            return;
        }

        var childChannels = child.Outputs.Values
            .Where(static output => output.Mode == WorkflowOutputMode.Stream && output.Channel is not null)
            .Select(static output => output.Channel!)
            .ToHashSet(StringComparer.Ordinal);
        foreach (string source in mappings.Select(static mapping => mapping.Key))
        {
            if (!childChannels.Contains(source))
            {
                issues.Add(new WorkflowInvocationAnalysisIssue(
                    WorkflowInvocationAnalysisCodes.UnknownChildStreamChannel,
                    "The mapped child stream channel is not declared by the child workflow.",
                    parent.Id,
                    node.Id,
                    basePath + "/streams/mappings/" + Escape(source)));
            }
        }
    }

    private static bool TryGetStaticValue(JsonNode? value, out JsonNode? staticValue)
    {
        if (value is JsonObject wrapper && wrapper.Count == 1)
        {
            if (wrapper.ContainsKey("$binding") || wrapper.ContainsKey("$expression"))
            {
                staticValue = null;
                return false;
            }

            if (wrapper.TryGetPropertyValue("$literal", out JsonNode? literal))
            {
                staticValue = literal;
                return true;
            }
        }

        staticValue = value;
        return true;
    }

    private static bool MatchesType(JsonNode? value, WorkflowInputType type)
    {
        if (value is null)
        {
            return false;
        }

        JsonValueKind kind = value.GetValueKind();
        return type switch
        {
            WorkflowInputType.String => kind == JsonValueKind.String,
            WorkflowInputType.Integer => IsInteger(value),
            WorkflowInputType.Number => kind == JsonValueKind.Number && IsFiniteNumber(value),
            WorkflowInputType.Boolean => kind is JsonValueKind.True or JsonValueKind.False,
            WorkflowInputType.Object => kind == JsonValueKind.Object,
            WorkflowInputType.Array => kind == JsonValueKind.Array,
            _ => false,
        };
    }

    private static bool IsInteger(JsonNode value)
    {
        if (value.GetValueKind() != JsonValueKind.Number || value is not JsonValue jsonValue)
        {
            return false;
        }

        if (jsonValue.TryGetValue<JsonElement>(out JsonElement element))
        {
            return element.TryGetInt64(out _);
        }

        if (jsonValue.TryGetValue<long>(out _) ||
            jsonValue.TryGetValue<int>(out _) ||
            jsonValue.TryGetValue<short>(out _) ||
            jsonValue.TryGetValue<byte>(out _))
        {
            return true;
        }

        if (jsonValue.TryGetValue<decimal>(out decimal decimalValue))
        {
            return decimal.Truncate(decimalValue) == decimalValue;
        }

        return jsonValue.TryGetValue<double>(out double doubleValue) &&
            double.IsFinite(doubleValue) &&
            Math.Truncate(doubleValue) == doubleValue;
    }

    private static bool IsFiniteNumber(JsonNode value)
    {
        if (value.GetValueKind() != JsonValueKind.Number || value is not JsonValue jsonValue)
        {
            return false;
        }

        if (jsonValue.TryGetValue<double>(out double doubleValue))
        {
            return double.IsFinite(doubleValue);
        }

        return jsonValue.TryGetValue<decimal>(out _) ||
            jsonValue.TryGetValue<long>(out _) ||
            jsonValue.TryGetValue<int>(out _);
    }

    private static string Escape(string value)
    {
        return value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
    }
}
