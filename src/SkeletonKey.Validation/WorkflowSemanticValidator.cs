using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Binding;
using SkeletonKey.Expressions;
using SkeletonKey.Locators;
using SkeletonKey.Resources;
using SkeletonKey.Validation.Internal;
using SkeletonKey.Workflow.Bindings;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Designer;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Inputs;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Outputs;
using SkeletonKey.Workflow.Policies;
using SkeletonKey.Workflow.Resources;
using SkeletonKey.Workflow.Specification;

namespace SkeletonKey.Validation;

/// <summary>
/// Performs deterministic semantic validation for SkeletonKey workflow language 0.1 documents.
/// </summary>
/// <remarks>
/// This validator is stateless, does not mutate supplied workflows, and is safe for concurrent use by
/// multiple threads. It validates semantic structure after parsing; successful JSON deserialization does
/// not imply semantic validity.
/// </remarks>
public sealed class WorkflowSemanticValidator : IWorkflowValidator
{
    private const string _startNodeType = "core.start";
    private const string _endNodeType = "core.end";
    private const string _invokeNodeType = "workflow.invoke";
    private const string _ifNodeType = "flow.if";
    private const string _switchNodeType = "flow.switch";
    private const string _forEachNodeType = "flow.foreach";
    private const string _repeatNodeType = "flow.repeat";
    private const string _whileNodeType = "flow.while";
    private const string _returnNodeType = "core.return";
    private const string _interactionRequestNodeType = "interaction.request";
    private readonly WorkflowBindingReader _bindingReader = new();
    private readonly WorkflowExpressionReader _expressionReader = new();
    private readonly WorkflowExpressionParser _expressionParser = new();
    private readonly WorkflowResourceReferenceReader _resourceReferenceReader = new();
    private readonly LocatorReferenceReader _locatorReferenceReader = new();

    /// <summary>
    /// Validates a workflow document and returns deterministic semantic issues.
    /// </summary>
    /// <param name="workflow">The workflow document to validate.</param>
    /// <returns>A semantic validation result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="workflow" /> is <see langword="null" />.</exception>
    public WorkflowValidationResult Validate(WorkflowDocument workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        List<WorkflowValidationIssue> issues = [];
        IReadOnlyDictionary<string, JsonNode?> variables = workflow.Variables;

        ValidateRoot(workflow, issues);
        ValidateInputs(workflow.Inputs, issues);
        ValidateVariables(variables, issues);
        ValidateResources(workflow.Resources, issues);
        NodeAnalysis nodeAnalysis = ValidateNodes(workflow.Nodes, issues);
        ValidateConnections(workflow.Connections, nodeAnalysis, issues);
        ValidateReachability(workflow.Nodes, workflow.Connections, nodeAnalysis, issues);
        ValidateOutputs(workflow.Outputs, nodeAnalysis, issues);
        ValidateInvocationsAndBindings(workflow, variables, nodeAnalysis, issues);
        ValidateControlFlowIterationAndExpressions(workflow, variables, nodeAnalysis, issues);
        ValidateResourceLocatorAndInteractionReferences(workflow, variables, nodeAnalysis, issues);
        ValidatePolicies(workflow.Nodes, issues);
        ValidateDesigner(workflow.Designer, nodeAnalysis.NodeIds, issues);

        return new WorkflowValidationResult(issues);
    }

    private static void ValidateRoot(WorkflowDocument workflow, List<WorkflowValidationIssue> issues)
    {
        if (!string.Equals(workflow.Schema, WorkflowSpecification.CurrentSchemaUri, StringComparison.Ordinal))
        {
            AddError(
                issues,
                WorkflowValidationCodes.InvalidSchemaUri,
                "Workflow schema URI must match the current SkeletonKey workflow schema URI.",
                "/$schema");
        }

        if (!string.Equals(workflow.SpecVersion, WorkflowSpecification.CurrentVersion, StringComparison.Ordinal))
        {
            AddError(
                issues,
                WorkflowValidationCodes.InvalidSpecificationVersion,
                "Workflow specification version must match the current SkeletonKey workflow version.",
                "/specVersion");
        }

        if (string.IsNullOrWhiteSpace(workflow.Id))
        {
            AddError(issues, WorkflowValidationCodes.WorkflowIdRequired, "Workflow ID is required.", "/id");
        }
        else if (!WorkflowValidationPatterns.IsWorkflowOrNodeId(workflow.Id))
        {
            AddError(issues, WorkflowValidationCodes.InvalidWorkflowId, "Workflow ID has an invalid format.", "/id");
        }

        if (string.IsNullOrWhiteSpace(workflow.Name))
        {
            AddError(issues, WorkflowValidationCodes.WorkflowNameRequired, "Workflow name is required.", "/name");
        }
    }

    private static void ValidateInputs(
        IReadOnlyDictionary<string, WorkflowInputDefinition> inputs,
        List<WorkflowValidationIssue> issues)
    {
        foreach (KeyValuePair<string, WorkflowInputDefinition> input in inputs)
        {
            string inputPath = JsonPointer.Combine("inputs", input.Key);

            if (!WorkflowValidationPatterns.IsInputOrVariableName(input.Key))
            {
                AddError(issues, WorkflowValidationCodes.InvalidInputName, "Input name has an invalid format.", inputPath);
            }

            WorkflowInputDefinition? definition = input.Value;
            if (definition is null)
            {
                continue;
            }

            if (definition.Required && definition.HasDefault)
            {
                AddError(
                    issues,
                    WorkflowValidationCodes.RequiredInputDeclaresDefault,
                    "Required input declarations must not declare a default because required means the caller must explicitly supply the value.",
                    JsonPointer.Combine("inputs", input.Key, "default"));
            }

            JsonNode? defaultValue = definition.Default;
            if (defaultValue is not null && !DefaultMatchesType(defaultValue, definition.Type))
            {
                AddError(
                    issues,
                    WorkflowValidationCodes.InputDefaultTypeMismatch,
                    "Input default value does not match the declared input type.",
                    JsonPointer.Combine("inputs", input.Key, "default"));
            }
        }
    }

    private static void ValidateVariables(IReadOnlyDictionary<string, JsonNode?> variables, List<WorkflowValidationIssue> issues)
    {
        foreach (KeyValuePair<string, JsonNode?> variable in variables)
        {
            if (!WorkflowValidationPatterns.IsInputOrVariableName(variable.Key))
            {
                AddError(
                    issues,
                    WorkflowValidationCodes.InvalidVariableName,
                    "Variable name has an invalid format.",
                    JsonPointer.Combine("variables", variable.Key));
            }
        }
    }

    private static void ValidateResources(
        IReadOnlyDictionary<string, WorkflowResourceDefinition> resources,
        List<WorkflowValidationIssue> issues)
    {
        foreach (KeyValuePair<string, WorkflowResourceDefinition> resource in resources)
        {
            string resourcePath = JsonPointer.Combine("resources", resource.Key);
            if (!WorkflowValidationPatterns.IsResourceName(resource.Key))
            {
                AddError(issues, WorkflowValidationCodes.InvalidWorkflowResourceName, "Workflow resource name has an invalid format.", resourcePath);
            }

            if (!WorkflowValidationPatterns.IsDottedResourceIdentifier(resource.Value.Kind))
            {
                AddError(issues, WorkflowValidationCodes.InvalidWorkflowResourceKind, "Workflow resource kind has an invalid format.", JsonPointer.Combine(resourcePath, "kind"));
            }

            HashSet<string> capabilities = new(StringComparer.Ordinal);
            for (int index = 0; index < resource.Value.Capabilities.Count; index++)
            {
                string capability = resource.Value.Capabilities[index];
                string capabilityPath = JsonPointer.Combine(resourcePath, "capabilities", index.ToString());
                if (!WorkflowValidationPatterns.IsDottedResourceIdentifier(capability))
                {
                    AddError(issues, WorkflowValidationCodes.InvalidResourceCapabilityId, "Resource capability ID has an invalid format.", capabilityPath);
                }

                if (!capabilities.Add(capability))
                {
                    AddError(issues, WorkflowValidationCodes.DuplicateResourceCapability, "Duplicate resource capability is not allowed.", capabilityPath);
                }
            }

            if (string.Equals(resource.Value.Kind, StandardWorkflowResourceKinds.WebBrowser, StringComparison.Ordinal))
            {
                ValidateWebBrowserConstraints(resource.Value.Constraints, JsonPointer.Combine(resourcePath, "constraints"), issues);
            }
        }
    }

    private static void ValidateWebBrowserConstraints(JsonObject? constraints, string constraintsPath, List<WorkflowValidationIssue> issues)
    {
        if (constraints is null)
        {
            return;
        }

        foreach (KeyValuePair<string, JsonNode?> property in constraints)
        {
            if (property.Key is not ("engine" or "profile" or "visibility"))
            {
                AddError(issues, WorkflowValidationCodes.InvalidStandardResourceConstraints, "Unknown web browser constraint property is not allowed.", JsonPointer.Combine(constraintsPath, property.Key));
            }
        }

        ValidateOptionalConstraintEnum(constraints, "engine", ["any", "chromium", "firefox", "webkit"], constraintsPath, issues);
        ValidateOptionalConstraintEnum(constraints, "profile", ["any", "ephemeral", "persistent"], constraintsPath, issues);
        ValidateOptionalConstraintEnum(constraints, "visibility", ["any", "headless", "headful"], constraintsPath, issues);
    }

    private static void ValidateOptionalConstraintEnum(
        JsonObject constraints,
        string propertyName,
        string[] allowed,
        string constraintsPath,
        List<WorkflowValidationIssue> issues)
    {
        if (!constraints.TryGetPropertyValue(propertyName, out JsonNode? value))
        {
            return;
        }

        if (value is null || value.GetValueKind() is not JsonValueKind.String || !allowed.Contains(value.GetValue<string>(), StringComparer.Ordinal))
        {
            AddError(issues, WorkflowValidationCodes.InvalidStandardResourceConstraints, "Web browser constraint value is invalid.", JsonPointer.Combine(constraintsPath, propertyName));
        }
    }

    private static NodeAnalysis ValidateNodes(IReadOnlyList<WorkflowNode> nodes, List<WorkflowValidationIssue> issues)
    {
        Dictionary<string, int> firstNodeIndexes = new(StringComparer.Ordinal);
        HashSet<string> duplicateNodeIds = new(StringComparer.Ordinal);
        List<int> startNodeIndexes = [];

        if (nodes.Count == 0)
        {
            AddError(issues, WorkflowValidationCodes.WorkflowHasNoNodes, "Workflow must declare at least one node.", "/nodes");
        }

        for (int index = 0; index < nodes.Count; index++)
        {
            WorkflowNode? node = nodes[index];

            if (node is null)
            {
                AddError(issues, WorkflowValidationCodes.NodeIdRequired, "Node ID is required.", JsonPointer.Combine("nodes", index.ToString(), "id"));
                AddError(issues, WorkflowValidationCodes.NodeTypeRequired, "Node type is required.", JsonPointer.Combine("nodes", index.ToString(), "type"));
                continue;
            }

            if (string.IsNullOrWhiteSpace(node.Id))
            {
                AddError(issues, WorkflowValidationCodes.NodeIdRequired, "Node ID is required.", JsonPointer.Combine("nodes", index.ToString(), "id"));
            }
            else
            {
                if (!WorkflowValidationPatterns.IsWorkflowOrNodeId(node.Id))
                {
                    AddError(issues, WorkflowValidationCodes.InvalidNodeId, "Node ID has an invalid format.", JsonPointer.Combine("nodes", index.ToString(), "id"));
                }

                if (!firstNodeIndexes.TryAdd(node.Id, index))
                {
                    duplicateNodeIds.Add(node.Id);
                    AddError(
                        issues,
                        WorkflowValidationCodes.DuplicateNodeId,
                        $"Duplicate node ID '{node.Id}' is not allowed.",
                        JsonPointer.Combine("nodes", index.ToString(), "id"));
                }
            }

            if (string.IsNullOrWhiteSpace(node.Type))
            {
                AddError(issues, WorkflowValidationCodes.NodeTypeRequired, "Node type is required.", JsonPointer.Combine("nodes", index.ToString(), "type"));
            }
            else
            {
                if (!WorkflowValidationPatterns.IsNodeType(node.Type))
                {
                    AddError(issues, WorkflowValidationCodes.InvalidNodeType, "Node type has an invalid format.", JsonPointer.Combine("nodes", index.ToString(), "type"));
                }

                if (string.Equals(node.Type, _startNodeType, StringComparison.Ordinal))
                {
                    startNodeIndexes.Add(index);
                }
            }

            if (node.TypeVersion < 1)
            {
                AddError(
                    issues,
                    WorkflowValidationCodes.InvalidNodeTypeVersion,
                    "Node type version must be at least 1.",
                    JsonPointer.Combine("nodes", index.ToString(), "typeVersion"));
            }
        }

        if (startNodeIndexes.Count != 1)
        {
            AddError(
                issues,
                WorkflowValidationCodes.InvalidStartNodeCount,
                $"Workflow must declare exactly one core.start node; found {startNodeIndexes.Count}.",
                "/nodes");
        }
        else
        {
            WorkflowNode startNode = nodes[startNodeIndexes[0]];
            if (startNode.Disabled)
            {
                AddError(
                    issues,
                    WorkflowValidationCodes.StartNodeIsDisabled,
                    "The core.start node must not be disabled.",
                    JsonPointer.Combine("nodes", startNodeIndexes[0].ToString(), "disabled"));
            }
        }

        return new NodeAnalysis(firstNodeIndexes, duplicateNodeIds, startNodeIndexes)
        {
            Nodes = nodes,
        };
    }

    private static void ValidateConnections(
        IReadOnlyList<WorkflowConnection> connections,
        NodeAnalysis nodeAnalysis,
        List<WorkflowValidationIssue> issues)
    {
        HashSet<ConnectionKey> connectionKeys = [];

        for (int index = 0; index < connections.Count; index++)
        {
            WorkflowConnection connection = connections[index];
            bool hasSourceNode = !string.IsNullOrWhiteSpace(connection.From.Node);
            bool hasTargetNode = !string.IsNullOrWhiteSpace(connection.To.Node);

            if (!hasSourceNode)
            {
                AddError(issues, WorkflowValidationCodes.SourceNodeRequired, "Connection source node is required.", JsonPointer.Combine("connections", index.ToString(), "from", "node"));
            }
            else if (!nodeAnalysis.NodeIds.ContainsKey(connection.From.Node))
            {
                AddError(issues, WorkflowValidationCodes.UnknownSourceNode, "Connection source node does not reference an existing node.", JsonPointer.Combine("connections", index.ToString(), "from", "node"));
            }

            if (!hasTargetNode)
            {
                AddError(issues, WorkflowValidationCodes.TargetNodeRequired, "Connection target node is required.", JsonPointer.Combine("connections", index.ToString(), "to", "node"));
            }
            else if (!nodeAnalysis.NodeIds.ContainsKey(connection.To.Node))
            {
                AddError(issues, WorkflowValidationCodes.UnknownTargetNode, "Connection target node does not reference an existing node.", JsonPointer.Combine("connections", index.ToString(), "to", "node"));
            }

            if (!WorkflowValidationPatterns.IsPortName(connection.From.Port))
            {
                AddError(issues, WorkflowValidationCodes.InvalidSourcePort, "Connection source port has an invalid format.", JsonPointer.Combine("connections", index.ToString(), "from", "port"));
            }

            if (!WorkflowValidationPatterns.IsPortName(connection.To.Port))
            {
                AddError(issues, WorkflowValidationCodes.InvalidTargetPort, "Connection target port has an invalid format.", JsonPointer.Combine("connections", index.ToString(), "to", "port"));
            }

            ConnectionKey key = new(connection.From.Node, connection.From.Port, connection.To.Node, connection.To.Port);
            if (!connectionKeys.Add(key))
            {
                AddError(issues, WorkflowValidationCodes.DuplicateConnection, "Duplicate connection is not allowed.", JsonPointer.Combine("connections", index.ToString()));
            }

            if (hasTargetNode && TryGetNode(nodeAnalysis, connection.To.Node, out WorkflowNode? targetNode) && string.Equals(targetNode.Type, _startNodeType, StringComparison.Ordinal))
            {
                AddError(issues, WorkflowValidationCodes.IncomingConnectionToStartNode, "The core.start node must not have incoming connections.", JsonPointer.Combine("connections", index.ToString(), "to", "node"));
            }

            if (hasSourceNode && TryGetNode(nodeAnalysis, connection.From.Node, out WorkflowNode? sourceNode) && string.Equals(sourceNode.Type, _endNodeType, StringComparison.Ordinal))
            {
                AddError(issues, WorkflowValidationCodes.OutgoingConnectionFromEndNode, "The core.end node must not have outgoing connections.", JsonPointer.Combine("connections", index.ToString(), "from", "node"));
            }

            ValidateReservedControlPorts(connection, index, nodeAnalysis, issues);
        }
    }

    private static void ValidateReachability(
        IReadOnlyList<WorkflowNode> nodes,
        IReadOnlyList<WorkflowConnection> connections,
        NodeAnalysis nodeAnalysis,
        List<WorkflowValidationIssue> issues)
    {
        if (!CanAnalyzeReachability(nodes, connections, nodeAnalysis, out int startIndex))
        {
            return;
        }

        string startId = nodes[startIndex].Id;
        Dictionary<string, List<string>> adjacency = new(StringComparer.Ordinal);
        foreach (WorkflowNode node in nodes)
        {
            adjacency[node.Id] = [];
        }

        foreach (WorkflowConnection connection in connections)
        {
            adjacency[connection.From.Node].Add(connection.To.Node);
        }

        HashSet<string> reachable = new(StringComparer.Ordinal);
        Queue<string> queue = new();
        reachable.Add(startId);
        queue.Enqueue(startId);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            foreach (string next in adjacency[current])
            {
                if (reachable.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        for (int index = 0; index < nodes.Count; index++)
        {
            WorkflowNode node = nodes[index];
            if (!node.Disabled &&
                !string.Equals(node.Id, startId, StringComparison.Ordinal) &&
                !reachable.Contains(node.Id))
            {
                AddWarning(
                    issues,
                    WorkflowValidationCodes.UnreachableNode,
                    "Enabled node is not reachable from the core.start node.",
                    JsonPointer.Combine("nodes", index.ToString()));
            }
        }
    }

    private static void ValidatePolicies(IReadOnlyList<WorkflowNode> nodes, List<WorkflowValidationIssue> issues)
    {
        for (int index = 0; index < nodes.Count; index++)
        {
            WorkflowNode? node = nodes[index];
            WorkflowExecutionPolicy? policy = node?.Policy;
            if (policy is null)
            {
                continue;
            }

            TimeSpan? delay = null;
            TimeSpan? maxDelay = null;

            if (policy.Timeout is not null &&
                (!WorkflowDurationParser.TryParse(policy.Timeout, out TimeSpan timeout) || timeout <= TimeSpan.Zero))
            {
                AddError(issues, WorkflowValidationCodes.InvalidTimeout, "Node policy timeout must be a valid duration greater than zero.", JsonPointer.Combine("nodes", index.ToString(), "policy", "timeout"));
            }

            WorkflowRetryPolicy? retry = policy.Retry;
            if (retry is null)
            {
                continue;
            }

            if (retry.MaxAttempts < 1)
            {
                AddError(issues, WorkflowValidationCodes.InvalidRetryAttemptCount, "Retry maxAttempts must be at least 1.", JsonPointer.Combine("nodes", index.ToString(), "policy", "retry", "maxAttempts"));
            }

            if (retry.Delay is not null)
            {
                if (!WorkflowDurationParser.TryParse(retry.Delay, out TimeSpan parsedDelay) || parsedDelay < TimeSpan.Zero)
                {
                    AddError(issues, WorkflowValidationCodes.InvalidRetryDelay, "Retry delay must be a valid duration greater than or equal to zero.", JsonPointer.Combine("nodes", index.ToString(), "policy", "retry", "delay"));
                }
                else
                {
                    delay = parsedDelay;
                }
            }

            if (!double.IsFinite(retry.Backoff) || retry.Backoff < 1.0)
            {
                AddError(issues, WorkflowValidationCodes.InvalidRetryBackoff, "Retry backoff must be finite and greater than or equal to 1.0.", JsonPointer.Combine("nodes", index.ToString(), "policy", "retry", "backoff"));
            }

            if (retry.MaxDelay is not null)
            {
                if (!WorkflowDurationParser.TryParse(retry.MaxDelay, out TimeSpan parsedMaxDelay) || parsedMaxDelay < TimeSpan.Zero)
                {
                    AddError(issues, WorkflowValidationCodes.InvalidRetryMaximumDelay, "Retry maxDelay must be a valid duration greater than or equal to zero.", JsonPointer.Combine("nodes", index.ToString(), "policy", "retry", "maxDelay"));
                }
                else
                {
                    maxDelay = parsedMaxDelay;
                }
            }

            if (delay.HasValue && maxDelay.HasValue && maxDelay.Value < delay.Value)
            {
                AddError(issues, WorkflowValidationCodes.MaximumDelayLessThanDelay, "Retry maxDelay must be greater than or equal to retry delay.", JsonPointer.Combine("nodes", index.ToString(), "policy", "retry", "maxDelay"));
            }
        }
    }

    private static void ValidateOutputs(
        IReadOnlyDictionary<string, WorkflowOutputDefinition> outputs,
        NodeAnalysis nodeAnalysis,
        List<WorkflowValidationIssue> issues)
    {
        foreach (KeyValuePair<string, WorkflowOutputDefinition> output in outputs)
        {
            string outputPath = JsonPointer.Combine("outputs", output.Key);

            if (!WorkflowValidationPatterns.IsInputOrVariableName(output.Key))
            {
                AddError(issues, WorkflowValidationCodes.InvalidWorkflowOutputName, "Workflow output name has an invalid format.", outputPath);
            }

            WorkflowOutputDefinition? definition = output.Value;
            if (definition is null)
            {
                continue;
            }

            if (definition.Mode is WorkflowOutputMode.Single or WorkflowOutputMode.Collection)
            {
                if (!definition.From.HasValue)
                {
                    AddError(issues, WorkflowValidationCodes.ValueOutputRequiresSourceEndpoint, "Single and collection outputs require a source endpoint.", JsonPointer.Combine("outputs", output.Key, "from"));
                }
                else
                {
                    ValidateOutputSource(output.Key, definition.From.Value, nodeAnalysis, issues);
                }

                if (definition.Channel is not null)
                {
                    AddError(issues, WorkflowValidationCodes.OutputIncompatibleProperties, "Single and collection outputs must not declare a channel.", outputPath);
                }
            }
            else if (definition.Mode is WorkflowOutputMode.Stream)
            {
                if (string.IsNullOrWhiteSpace(definition.Channel))
                {
                    AddError(issues, WorkflowValidationCodes.StreamOutputRequiresChannel, "Stream outputs require a channel.", JsonPointer.Combine("outputs", output.Key, "channel"));
                }
                else if (!WorkflowValidationPatterns.IsOutputChannelName(definition.Channel))
                {
                    AddError(issues, WorkflowValidationCodes.InvalidOutputChannelName, "Stream output channel has an invalid format.", JsonPointer.Combine("outputs", output.Key, "channel"));
                }

                if (definition.From.HasValue)
                {
                    AddError(issues, WorkflowValidationCodes.OutputIncompatibleProperties, "Stream outputs must not declare a source endpoint.", outputPath);
                }
            }
        }
    }

    private static void ValidateOutputSource(
        string outputName,
        WorkflowEndpoint source,
        NodeAnalysis nodeAnalysis,
        List<WorkflowValidationIssue> issues)
    {
        if (!nodeAnalysis.NodeIds.ContainsKey(source.Node))
        {
            AddError(
                issues,
                WorkflowValidationCodes.OutputUnknownSourceNode,
                "Workflow output source node does not reference an existing node.",
                JsonPointer.Combine("outputs", outputName, "from", "node"));
        }

        if (!WorkflowValidationPatterns.IsPortName(source.Port))
        {
            AddError(
                issues,
                WorkflowValidationCodes.InvalidOutputSourcePort,
                "Workflow output source port has an invalid format.",
                JsonPointer.Combine("outputs", outputName, "from", "port"));
        }
    }

    private void ValidateInvocationsAndBindings(
        WorkflowDocument workflow,
        IReadOnlyDictionary<string, JsonNode?> variables,
        NodeAnalysis nodeAnalysis,
        List<WorkflowValidationIssue> issues)
    {
        HashSet<string> parentStreamChannels = new(StringComparer.Ordinal);
        foreach (WorkflowOutputDefinition output in workflow.Outputs.Values)
        {
            if (output.Mode is WorkflowOutputMode.Stream && output.Channel is not null)
            {
                parentStreamChannels.Add(output.Channel);
            }
        }

        for (int index = 0; index < workflow.Nodes.Count; index++)
        {
            WorkflowNode? node = workflow.Nodes[index];
            if (node is null || !string.Equals(node.Type, _invokeNodeType, StringComparison.Ordinal))
            {
                continue;
            }

            string parametersPath = JsonPointer.Combine("nodes", index.ToString(), "parameters");
            JsonObject parameters = node.Parameters;

            if (node.TypeVersion != 1)
            {
                AddError(
                    issues,
                    WorkflowValidationCodes.UnsupportedInvocationNodeVersion,
                    "workflow.invoke nodes must declare typeVersion 1.",
                    JsonPointer.Combine("nodes", index.ToString(), "typeVersion"));
            }

            ValidateInvocationParameters(parameters, parametersPath, issues);
            ValidateInvocationWorkflowReference(parameters, parametersPath, issues);
            ValidateInvocationInputs(parameters, parametersPath, workflow.Inputs, variables, nodeAnalysis, node, issues);
            ValidateInvocationStreamPolicy(parameters, parametersPath, parentStreamChannels, issues);
        }
    }

    private static void ValidateInvocationParameters(JsonObject parameters, string parametersPath, List<WorkflowValidationIssue> issues)
    {
        string[] known = ["workflow", "inputs", "resources", "streams"];
        foreach (KeyValuePair<string, JsonNode?> parameter in parameters)
        {
            if (!known.Contains(parameter.Key, StringComparer.Ordinal))
            {
                AddError(
                    issues,
                    WorkflowValidationCodes.MalformedBindingWrapper,
                    "Unknown workflow.invoke parameter property is not allowed.",
                    JsonPointer.Combine(parametersPath, parameter.Key));
            }
        }
    }

    private static void ValidateInvocationWorkflowReference(JsonObject parameters, string parametersPath, List<WorkflowValidationIssue> issues)
    {
        string workflowPath = JsonPointer.Combine(parametersPath, "workflow");
        if (!parameters.TryGetPropertyValue("workflow", out JsonNode? workflowNode) || workflowNode is null)
        {
            AddError(issues, WorkflowValidationCodes.MissingInvocationWorkflowReference, "workflow.invoke nodes must declare parameters.workflow.", workflowPath);
            return;
        }

        if (workflowNode is not JsonObject workflowReference)
        {
            AddError(issues, WorkflowValidationCodes.MissingInvocationWorkflowReference, "parameters.workflow must be an object.", workflowPath);
            return;
        }

        foreach (KeyValuePair<string, JsonNode?> property in workflowReference)
        {
            if (property.Key is not ("id" or "version"))
            {
                AddError(issues, WorkflowValidationCodes.MissingInvocationWorkflowReference, "Unknown workflow reference property is not allowed.", JsonPointer.Combine(workflowPath, property.Key));
            }
        }

        if (!workflowReference.TryGetPropertyValue("id", out JsonNode? idNode) || idNode is null || idNode.GetValueKind() is not JsonValueKind.String)
        {
            AddError(issues, WorkflowValidationCodes.InvalidReferencedWorkflowId, "Referenced workflow ID is required.", JsonPointer.Combine(workflowPath, "id"));
        }
        else
        {
            string id = idNode.GetValue<string>();
            if (!WorkflowValidationPatterns.IsWorkflowOrNodeId(id))
            {
                AddError(issues, WorkflowValidationCodes.InvalidReferencedWorkflowId, "Referenced workflow ID has an invalid format.", JsonPointer.Combine(workflowPath, "id"));
            }
        }

        if (workflowReference.TryGetPropertyValue("version", out JsonNode? versionNode) && versionNode is not null)
        {
            if (versionNode.GetValueKind() is not JsonValueKind.String || !SemanticVersionValidator.IsExactSemanticVersion(versionNode.GetValue<string>()))
            {
                AddError(issues, WorkflowValidationCodes.InvalidReferencedWorkflowVersion, "Referenced workflow version must be an exact Semantic Version 2.0 value.", JsonPointer.Combine(workflowPath, "version"));
            }
        }
    }

    private void ValidateInvocationInputs(
        JsonObject parameters,
        string parametersPath,
        IReadOnlyDictionary<string, WorkflowInputDefinition> workflowInputs,
        IReadOnlyDictionary<string, JsonNode?> variables,
        NodeAnalysis nodeAnalysis,
        WorkflowNode invocationNode,
        List<WorkflowValidationIssue> issues)
    {
        if (!parameters.TryGetPropertyValue("inputs", out JsonNode? inputsNode))
        {
            return;
        }

        string inputsPath = JsonPointer.Combine(parametersPath, "inputs");
        if (inputsNode is not JsonObject inputs)
        {
            AddError(issues, WorkflowValidationCodes.InvalidInvocationInputName, "workflow.invoke inputs must be an object.", inputsPath);
            return;
        }

        foreach (KeyValuePair<string, JsonNode?> input in inputs)
        {
            string inputPath = JsonPointer.Combine(inputsPath, input.Key);
            if (!WorkflowValidationPatterns.IsInputOrVariableName(input.Key))
            {
                AddError(issues, WorkflowValidationCodes.InvalidInvocationInputName, "Invocation input name has an invalid format.", inputPath);
            }

            ValidateWorkflowValue(input.Value, inputPath, workflowInputs, variables, nodeAnalysis, invocationNode, issues);
        }
    }

    private void ValidateResourceLocatorAndInteractionReferences(
        WorkflowDocument workflow,
        IReadOnlyDictionary<string, JsonNode?> variables,
        NodeAnalysis nodeAnalysis,
        List<WorkflowValidationIssue> issues)
    {
        for (int index = 0; index < workflow.Nodes.Count; index++)
        {
            WorkflowNode? node = workflow.Nodes[index];
            if (node is null)
            {
                continue;
            }

            string parametersPath = JsonPointer.Combine("nodes", index.ToString(), "parameters");
            JsonObject parameters = node.Parameters;
            ValidateResourceReferences(parameters, parametersPath, workflow.Resources, issues);
            ValidateLocatorReferences(parameters, parametersPath, issues);

            if (string.Equals(node.Type, _invokeNodeType, StringComparison.Ordinal))
            {
                ValidateInvocationResourceMappings(parameters, parametersPath, workflow.Resources, issues);
            }
            else if (string.Equals(node.Type, _interactionRequestNodeType, StringComparison.Ordinal))
            {
                ValidateInteractionNode(node, index, parameters, parametersPath, workflow.Inputs, variables, nodeAnalysis, issues);
            }
        }
    }

    private void ValidateInteractionNode(
        WorkflowNode node,
        int nodeIndex,
        JsonObject parameters,
        string parametersPath,
        IReadOnlyDictionary<string, WorkflowInputDefinition> workflowInputs,
        IReadOnlyDictionary<string, JsonNode?> variables,
        NodeAnalysis nodeAnalysis,
        List<WorkflowValidationIssue> issues)
    {
        if (node.TypeVersion != 1)
        {
            AddError(issues, WorkflowValidationCodes.UnsupportedInteractionNodeVersion, "interaction.request nodes must declare typeVersion 1.", JsonPointer.Combine("nodes", nodeIndex.ToString(), "typeVersion"));
        }

        ValidateKnownParameters(parameters, parametersPath, ["kind", "prompt", "description", "options", "default", "required", "timeout"], issues, WorkflowValidationCodes.InvalidInteractionKind);

        string? kind = null;
        if (!parameters.TryGetPropertyValue("kind", out JsonNode? kindNode) || kindNode is null || kindNode.GetValueKind() is not JsonValueKind.String)
        {
            AddError(issues, WorkflowValidationCodes.InvalidInteractionKind, "interaction.request requires string parameters.kind.", JsonPointer.Combine(parametersPath, "kind"));
        }
        else
        {
            kind = kindNode.GetValue<string>();
            if (kind is not ("confirmation" or "text" or "secret" or "choice" or "multiple-choice" or "manual-action"))
            {
                AddError(issues, WorkflowValidationCodes.InvalidInteractionKind, "Interaction kind is invalid.", JsonPointer.Combine(parametersPath, "kind"));
            }
        }

        if (!parameters.TryGetPropertyValue("prompt", out JsonNode? prompt))
        {
            AddError(issues, WorkflowValidationCodes.InvalidInteractionPrompt, "interaction.request requires parameters.prompt.", JsonPointer.Combine(parametersPath, "prompt"));
        }
        else
        {
            ValidateInteractionTextValue(prompt, JsonPointer.Combine(parametersPath, "prompt"), workflowInputs, variables, nodeAnalysis, node, issues);
        }

        if (parameters.TryGetPropertyValue("description", out JsonNode? description))
        {
            ValidateInteractionTextValue(description, JsonPointer.Combine(parametersPath, "description"), workflowInputs, variables, nodeAnalysis, node, issues);
        }

        if (parameters.TryGetPropertyValue("required", out JsonNode? required) &&
            (required is null || required.GetValueKind() is not JsonValueKind.True and not JsonValueKind.False))
        {
            AddError(issues, WorkflowValidationCodes.InvalidInteractionKind, "interaction.request required must be boolean.", JsonPointer.Combine(parametersPath, "required"));
        }

        HashSet<string> optionIds = ValidateInteractionOptions(parameters, parametersPath, kind, issues);
        ValidateInteractionDefault(parameters, parametersPath, kind, optionIds, issues);
        ValidateInteractionTimeout(parameters, parametersPath, issues);
    }

    private void ValidateInteractionTextValue(
        JsonNode? value,
        string path,
        IReadOnlyDictionary<string, WorkflowInputDefinition> workflowInputs,
        IReadOnlyDictionary<string, JsonNode?> variables,
        NodeAnalysis nodeAnalysis,
        WorkflowNode node,
        List<WorkflowValidationIssue> issues)
    {
        ValidateWorkflowValue(value, path, workflowInputs, variables, nodeAnalysis, node, issues);
        if (value is not null && (value.GetValueKind() is JsonValueKind.String || IsBindingWrapper(value) || IsExpressionWrapper(value) || IsLiteralString(value)))
        {
            return;
        }

        AddError(issues, WorkflowValidationCodes.InvalidInteractionPrompt, "Interaction prompt and description must be string literals, bindings, or expressions.", path);
    }

    private static HashSet<string> ValidateInteractionOptions(
        JsonObject parameters,
        string parametersPath,
        string? kind,
        List<WorkflowValidationIssue> issues)
    {
        HashSet<string> optionIds = new(StringComparer.Ordinal);
        bool hasOptions = parameters.TryGetPropertyValue("options", out JsonNode? optionsNode);
        bool requiresOptions = kind is "choice" or "multiple-choice";

        if (!requiresOptions && hasOptions)
        {
            AddError(issues, WorkflowValidationCodes.InvalidInteractionOptions, "Only choice interactions may declare options.", JsonPointer.Combine(parametersPath, "options"));
            return optionIds;
        }

        if (!hasOptions)
        {
            if (requiresOptions)
            {
                AddError(issues, WorkflowValidationCodes.InvalidInteractionOptions, "Choice interactions require options.", JsonPointer.Combine(parametersPath, "options"));
            }

            return optionIds;
        }

        if (optionsNode is not JsonArray options)
        {
            AddError(issues, WorkflowValidationCodes.InvalidInteractionOptions, "Interaction options must be an array.", JsonPointer.Combine(parametersPath, "options"));
            return optionIds;
        }

        if (requiresOptions && options.Count == 0)
        {
            AddError(issues, WorkflowValidationCodes.InvalidInteractionOptions, "Choice interactions require at least one option.", JsonPointer.Combine(parametersPath, "options"));
        }

        for (int index = 0; index < options.Count; index++)
        {
            string optionPath = JsonPointer.Combine(parametersPath, "options", index.ToString());
            if (options[index] is not JsonObject option)
            {
                AddError(issues, WorkflowValidationCodes.InvalidInteractionOptions, "Interaction options must be objects.", optionPath);
                continue;
            }

            ValidateKnownParameters(option, optionPath, ["id", "label", "description", "value"], issues, WorkflowValidationCodes.InvalidInteractionOptions);
            if (!option.TryGetPropertyValue("id", out JsonNode? idNode) || idNode is null || idNode.GetValueKind() is not JsonValueKind.String)
            {
                AddError(issues, WorkflowValidationCodes.InvalidInteractionOptions, "Interaction option ID is required.", JsonPointer.Combine(optionPath, "id"));
                continue;
            }

            string id = idNode.GetValue<string>();
            if (!WorkflowValidationPatterns.IsWorkflowOrNodeId(id))
            {
                AddError(issues, WorkflowValidationCodes.InvalidInteractionOptions, "Interaction option ID has an invalid format.", JsonPointer.Combine(optionPath, "id"));
            }

            if (!optionIds.Add(id))
            {
                AddError(issues, WorkflowValidationCodes.DuplicateInteractionOptionId, "Interaction option IDs must be unique.", JsonPointer.Combine(optionPath, "id"));
            }

            if (!option.TryGetPropertyValue("label", out JsonNode? labelNode) || labelNode is null || labelNode.GetValueKind() is not JsonValueKind.String)
            {
                AddError(issues, WorkflowValidationCodes.InvalidInteractionOptions, "Interaction option label is required.", JsonPointer.Combine(optionPath, "label"));
            }

            if (option.TryGetPropertyValue("description", out JsonNode? descriptionNode) &&
                (descriptionNode is null || descriptionNode.GetValueKind() is not JsonValueKind.String))
            {
                AddError(issues, WorkflowValidationCodes.InvalidInteractionOptions, "Interaction option description must be a string.", JsonPointer.Combine(optionPath, "description"));
            }
        }

        return optionIds;
    }

    private static void ValidateInteractionDefault(
        JsonObject parameters,
        string parametersPath,
        string? kind,
        HashSet<string> optionIds,
        List<WorkflowValidationIssue> issues)
    {
        if (!parameters.TryGetPropertyValue("default", out JsonNode? defaultValue))
        {
            return;
        }

        string defaultPath = JsonPointer.Combine(parametersPath, "default");
        if (kind == "secret")
        {
            AddError(issues, WorkflowValidationCodes.SecretInteractionContainsProhibitedDefault, "Secret interactions must not declare a default.", defaultPath);
            return;
        }

        if (kind == "manual-action")
        {
            AddError(issues, WorkflowValidationCodes.InvalidInteractionDefault, "Manual action interactions must not declare a default.", defaultPath);
            return;
        }

        if (kind == "confirmation")
        {
            if (!IsBooleanLiteral(defaultValue))
            {
                AddError(issues, WorkflowValidationCodes.InvalidInteractionDefault, "Confirmation defaults must be boolean.", defaultPath);
            }
        }
        else if (kind == "text")
        {
            if (defaultValue is not null && defaultValue.GetValueKind() is not JsonValueKind.String)
            {
                AddError(issues, WorkflowValidationCodes.InvalidInteractionDefault, "Text defaults must be strings or explicit JSON null.", defaultPath);
            }
        }
        else if (kind == "choice")
        {
            if (defaultValue is null || defaultValue.GetValueKind() is not JsonValueKind.String || !optionIds.Contains(defaultValue.GetValue<string>()))
            {
                AddError(issues, WorkflowValidationCodes.InvalidInteractionDefault, "Choice default must reference one declared option ID.", defaultPath);
            }
        }
        else if (kind == "multiple-choice")
        {
            if (defaultValue is not JsonArray array)
            {
                AddError(issues, WorkflowValidationCodes.InvalidInteractionDefault, "Multiple-choice default must be an array of option IDs.", defaultPath);
                return;
            }

            HashSet<string> selected = new(StringComparer.Ordinal);
            for (int index = 0; index < array.Count; index++)
            {
                JsonNode? item = array[index];
                if (item is null || item.GetValueKind() is not JsonValueKind.String || !optionIds.Contains(item.GetValue<string>()) || !selected.Add(item.GetValue<string>()))
                {
                    AddError(issues, WorkflowValidationCodes.InvalidInteractionDefault, "Multiple-choice default values must reference unique declared option IDs.", JsonPointer.Combine(defaultPath, index.ToString()));
                }
            }
        }
        else
        {
            AddError(issues, WorkflowValidationCodes.InvalidInteractionDefault, "Interaction default is not valid for this interaction kind.", defaultPath);
        }
    }

    private static void ValidateInteractionTimeout(JsonObject parameters, string parametersPath, List<WorkflowValidationIssue> issues)
    {
        if (!parameters.TryGetPropertyValue("timeout", out JsonNode? timeout))
        {
            return;
        }

        if (timeout is null ||
            timeout.GetValueKind() is not JsonValueKind.String ||
            !WorkflowDurationParser.TryParse(timeout.GetValue<string>(), out TimeSpan parsed) ||
            parsed <= TimeSpan.Zero)
        {
            AddError(issues, WorkflowValidationCodes.InvalidInteractionTimeout, "Interaction timeout must be a valid duration greater than zero.", JsonPointer.Combine(parametersPath, "timeout"));
        }
    }

    private void ValidateResourceReferences(
        JsonNode? value,
        string path,
        IReadOnlyDictionary<string, WorkflowResourceDefinition> resources,
        List<WorkflowValidationIssue> issues)
    {
        try
        {
            foreach (WorkflowResourceReferenceOccurrence occurrence in _resourceReferenceReader.FindResourceReferences(value))
            {
                string occurrencePath = CombinePointer(path, occurrence.Path);
                if (!resources.ContainsKey(occurrence.Reference.Name))
                {
                    AddError(issues, WorkflowValidationCodes.UnknownWorkflowResourceReference, "Resource reference targets an undeclared workflow resource.", CombinePointer(occurrencePath, "/$resource/name"));
                }
            }
        }
        catch (WorkflowResourceReferenceFormatException exception)
        {
            AddError(issues, WorkflowValidationCodes.MalformedResourceReferenceWrapper, exception.Message, CombinePointer(path, exception.JsonPath));
        }
    }

    private void ValidateLocatorReferences(JsonNode? value, string path, List<WorkflowValidationIssue> issues)
    {
        try
        {
            foreach (LocatorReferenceOccurrence occurrence in _locatorReferenceReader.FindLocatorReferences(value))
            {
                string occurrencePath = CombinePointer(path, occurrence.Path);
                LocatorReference reference = occurrence.Reference;
                string referencePath = CombinePointer(occurrencePath, "/$locator");
                if (!WorkflowValidationPatterns.IsWorkflowOrNodeId(reference.Catalog))
                {
                    AddError(issues, WorkflowValidationCodes.InvalidLocatorCatalogId, "Locator catalog ID has an invalid format.", CombinePointer(referencePath, "/catalog"));
                }

                if (!WorkflowValidationPatterns.IsWorkflowOrNodeId(reference.Id))
                {
                    AddError(issues, WorkflowValidationCodes.InvalidLocatorId, "Locator ID has an invalid format.", CombinePointer(referencePath, "/id"));
                }

                if (reference.Version is not null && !SemanticVersionValidator.IsExactSemanticVersion(reference.Version))
                {
                    AddError(issues, WorkflowValidationCodes.InvalidLocatorVersion, "Locator version must be an exact Semantic Version 2.0 value.", CombinePointer(referencePath, "/version"));
                }
            }
        }
        catch (LocatorReferenceFormatException exception)
        {
            AddError(issues, WorkflowValidationCodes.MalformedLocatorReferenceWrapper, exception.Message, CombinePointer(path, exception.JsonPath));
        }
    }

    private void ValidateInvocationResourceMappings(
        JsonObject parameters,
        string parametersPath,
        IReadOnlyDictionary<string, WorkflowResourceDefinition> resources,
        List<WorkflowValidationIssue> issues)
    {
        if (!parameters.TryGetPropertyValue("resources", out JsonNode? resourcesNode))
        {
            return;
        }

        string resourcesPath = JsonPointer.Combine(parametersPath, "resources");
        if (resourcesNode is not JsonObject mappings)
        {
            AddError(issues, WorkflowValidationCodes.InvalidInvocationResourceMappingValue, "workflow.invoke resources must be an object.", resourcesPath);
            return;
        }

        foreach (KeyValuePair<string, JsonNode?> mapping in mappings)
        {
            string mappingPath = JsonPointer.Combine(resourcesPath, mapping.Key);
            if (!WorkflowValidationPatterns.IsResourceName(mapping.Key))
            {
                AddError(issues, WorkflowValidationCodes.InvalidInvocationResourceMappingName, "Invocation resource mapping name has an invalid format.", mappingPath);
            }

            if (mapping.Value is not JsonObject wrapper || !wrapper.ContainsKey("$resource"))
            {
                AddError(issues, WorkflowValidationCodes.InvalidInvocationResourceMappingValue, "Invocation resource mapping values must be `$resource` wrappers.", mappingPath);
                continue;
            }

            try
            {
                WorkflowResourceReference reference = _resourceReferenceReader.Read(wrapper);
                if (!resources.ContainsKey(reference.Name))
                {
                    AddError(issues, WorkflowValidationCodes.UnknownWorkflowResourceReference, "Invocation resource mapping targets an undeclared parent resource.", JsonPointer.Combine(mappingPath, "$resource", "name"));
                }
            }
            catch (WorkflowResourceReferenceFormatException exception)
            {
                AddError(issues, WorkflowValidationCodes.InvalidInvocationResourceMappingValue, exception.Message, CombinePointer(mappingPath, exception.JsonPath));
            }
        }
    }

    private void ValidateWorkflowValue(
        JsonNode? value,
        string path,
        IReadOnlyDictionary<string, WorkflowInputDefinition> workflowInputs,
        IReadOnlyDictionary<string, JsonNode?> variables,
        NodeAnalysis nodeAnalysis,
        WorkflowNode invocationNode,
        List<WorkflowValidationIssue> issues)
    {
        try
        {
            foreach (WorkflowBindingOccurrence occurrence in _bindingReader.FindBindings(value))
            {
                string occurrencePath = CombinePointer(path, occurrence.Path);
                ValidateBinding(occurrence.Binding, occurrencePath, workflowInputs, variables, nodeAnalysis, invocationNode, issues);
            }
        }
        catch (WorkflowBindingFormatException exception)
        {
            string issuePath = CombinePointer(path, exception.JsonPath);
            string code = issuePath.EndsWith("$literal", StringComparison.Ordinal) || exception.Message.Contains("Literal", StringComparison.Ordinal)
                ? WorkflowValidationCodes.InvalidLiteralWrapper
                : exception.Message.Contains("Unknown binding source", StringComparison.Ordinal)
                    ? WorkflowValidationCodes.UnknownBindingSource
                    : exception.Message.Contains("Iteration", StringComparison.Ordinal) || exception.Message.Contains("iteration", StringComparison.Ordinal)
                        ? WorkflowValidationCodes.InvalidIterationBindingShape
                        : exception.Message.Contains("default", StringComparison.Ordinal)
                            ? WorkflowValidationCodes.InvalidBindingMissingValueConfiguration
                            : WorkflowValidationCodes.MalformedBindingWrapper;
            AddError(issues, code, exception.Message, issuePath);
        }

        try
        {
            foreach (WorkflowExpressionOccurrence occurrence in _expressionReader.FindExpressions(value))
            {
                string occurrencePath = CombinePointer(path, occurrence.Path);
                ValidateExpression(occurrence.Text, occurrencePath, workflowInputs, variables, nodeAnalysis, invocationNode, issues);
            }
        }
        catch (WorkflowExpressionFormatException exception)
        {
            string issuePath = CombinePointer(path, exception.JsonPath);
            string code = issuePath.EndsWith("$literal", StringComparison.Ordinal) || exception.Message.Contains("Literal", StringComparison.Ordinal)
                ? WorkflowValidationCodes.InvalidLiteralWrapper
                : WorkflowValidationCodes.MalformedExpressionWrapper;
            AddError(issues, code, exception.Message, issuePath);
        }
    }

    private void ValidateExpression(
        string expression,
        string occurrencePath,
        IReadOnlyDictionary<string, WorkflowInputDefinition> workflowInputs,
        IReadOnlyDictionary<string, JsonNode?> variables,
        NodeAnalysis nodeAnalysis,
        WorkflowNode currentNode,
        List<WorkflowValidationIssue> issues)
    {
        WorkflowExpressionDocument document = _expressionParser.Parse(expression);
        string expressionPath = CombinePointer(occurrencePath, "/$expression");

        foreach (WorkflowExpressionDiagnostic diagnostic in document.Diagnostics)
        {
            string code = string.Equals(diagnostic.Code, "Expression.UnknownFunction", StringComparison.Ordinal)
                ? WorkflowValidationCodes.UnknownExpressionFunction
                : WorkflowValidationCodes.ExpressionSyntaxError;
            AddError(issues, code, $"{diagnostic.Message} Offset {diagnostic.Offset}, length {diagnostic.Length}.", expressionPath);
        }

        if (!document.IsValid)
        {
            return;
        }

        foreach (WorkflowExpressionReference reference in document.References)
        {
            if (reference.Kind is WorkflowExpressionReferenceKind.Input)
            {
                if (reference.ReferencedName is not null && !workflowInputs.ContainsKey(reference.ReferencedName))
                {
                    AddError(issues, WorkflowValidationCodes.UnknownExpressionInput, "Expression references an undeclared workflow input.", expressionPath);
                }
            }
            else if (reference.Kind is WorkflowExpressionReferenceKind.Variable)
            {
                if (reference.ReferencedName is not null && !variables.ContainsKey(reference.ReferencedName))
                {
                    AddError(issues, WorkflowValidationCodes.UnknownExpressionVariable, "Expression references an undeclared workflow variable.", expressionPath);
                }
            }
            else if (reference.Kind is WorkflowExpressionReferenceKind.Node)
            {
                if (reference.NodeId is not null)
                {
                    if (!nodeAnalysis.NodeIds.ContainsKey(reference.NodeId))
                    {
                        AddError(issues, WorkflowValidationCodes.UnknownExpressionNode, "Expression references an unknown workflow node.", expressionPath);
                    }
                    else if (string.Equals(reference.NodeId, currentNode.Id, StringComparison.Ordinal))
                    {
                        AddError(issues, WorkflowValidationCodes.SelfReferencingExpressionNode, "Node parameter expression must not reference the same node.", expressionPath);
                    }
                }
            }
            else if (reference.Kind is WorkflowExpressionReferenceKind.Iteration)
            {
                ValidateIterationReference(reference.IterationId, expressionPath, nodeAnalysis, issues);
            }
        }
    }

    private static void ValidateBinding(
        WorkflowBinding binding,
        string occurrencePath,
        IReadOnlyDictionary<string, WorkflowInputDefinition> workflowInputs,
        IReadOnlyDictionary<string, JsonNode?> variables,
        NodeAnalysis nodeAnalysis,
        WorkflowNode invocationNode,
        List<WorkflowValidationIssue> issues)
    {
        string bindingPath = CombinePointer(occurrencePath, "/$binding");

        if (!JsonPointerIsValid(binding.Path))
        {
            AddError(issues, WorkflowValidationCodes.InvalidBindingJsonPointer, "Binding path must be an empty string or a valid read-only RFC 6901 JSON Pointer.", CombinePointer(bindingPath, "/path"));
        }

        if ((binding.OnMissing is WorkflowBindingMissingBehavior.Default) != binding.HasDefault)
        {
            AddError(issues, WorkflowValidationCodes.InvalidBindingMissingValueConfiguration, "Binding default configuration is invalid.", bindingPath);
        }

        if (binding.Source is WorkflowBindingSource.Input)
        {
            if (binding.Name is not null && !workflowInputs.ContainsKey(binding.Name))
            {
                AddError(issues, WorkflowValidationCodes.UnknownWorkflowInputBinding, "Binding references an unknown workflow input.", CombinePointer(bindingPath, "/name"));
            }
        }
        else if (binding.Source is WorkflowBindingSource.Variable)
        {
            if (binding.Name is not null && !variables.ContainsKey(binding.Name))
            {
                AddError(issues, WorkflowValidationCodes.UnknownWorkflowVariableBinding, "Binding references an unknown workflow variable.", CombinePointer(bindingPath, "/name"));
            }
        }
        else if (binding.Source is WorkflowBindingSource.Node)
        {
            if (binding.Node is not null)
            {
                if (!nodeAnalysis.NodeIds.ContainsKey(binding.Node))
                {
                    AddError(issues, WorkflowValidationCodes.UnknownNodeBinding, "Binding references an unknown workflow node.", CombinePointer(bindingPath, "/node"));
                }
                else if (string.Equals(binding.Node, invocationNode.Id, StringComparison.Ordinal))
                {
                    AddError(issues, WorkflowValidationCodes.SelfReferencingNodeBinding, "Node parameter binding must not reference the same node.", CombinePointer(bindingPath, "/node"));
                }
            }

            if (binding.Port is not null && !WorkflowValidationPatterns.IsPortName(binding.Port))
            {
                AddError(issues, WorkflowValidationCodes.InvalidNodeBindingPort, "Node binding port has an invalid format.", CombinePointer(bindingPath, "/port"));
            }
        }
        else if (binding.Source is WorkflowBindingSource.Iteration)
        {
            if (binding.Iteration is null || !WorkflowValidationPatterns.IsWorkflowOrNodeId(binding.Iteration))
            {
                AddError(issues, WorkflowValidationCodes.InvalidIterationBindingShape, "Iteration binding must declare a valid iteration node ID.", CombinePointer(bindingPath, "/iteration"));
            }
            else
            {
                ValidateIterationReference(binding.Iteration, CombinePointer(bindingPath, "/iteration"), nodeAnalysis, issues);
            }
        }
    }

    private void ValidateControlFlowIterationAndExpressions(
        WorkflowDocument workflow,
        IReadOnlyDictionary<string, JsonNode?> variables,
        NodeAnalysis nodeAnalysis,
        List<WorkflowValidationIssue> issues)
    {
        for (int index = 0; index < workflow.Nodes.Count; index++)
        {
            WorkflowNode? node = workflow.Nodes[index];
            if (node is null || !IsReservedControlNode(node.Type))
            {
                continue;
            }

            string nodePath = JsonPointer.Combine("nodes", index.ToString());
            string parametersPath = JsonPointer.Combine(nodePath, "parameters");
            JsonObject parameters = node.Parameters;

            if (node.TypeVersion != 1)
            {
                AddError(issues, WorkflowValidationCodes.UnsupportedControlNodeVersion, "Reserved control nodes must declare typeVersion 1.", JsonPointer.Combine(nodePath, "typeVersion"));
            }

            if (node.Type == _ifNodeType)
            {
                ValidateIfNode(parameters, parametersPath, workflow.Inputs, variables, nodeAnalysis, node, issues);
            }
            else if (node.Type == _switchNodeType)
            {
                ValidateSwitchNode(parameters, parametersPath, workflow.Inputs, variables, nodeAnalysis, node, issues);
            }
            else if (node.Type == _forEachNodeType)
            {
                ValidateForEachNode(parameters, parametersPath, workflow.Inputs, variables, nodeAnalysis, node, issues);
            }
            else if (node.Type == _repeatNodeType)
            {
                ValidateRepeatNode(parameters, parametersPath, workflow.Inputs, variables, nodeAnalysis, node, issues);
            }
            else if (node.Type == _whileNodeType)
            {
                ValidateWhileNode(parameters, parametersPath, workflow.Inputs, variables, nodeAnalysis, node, issues);
            }
            else if (node.Type == _returnNodeType)
            {
                ValidateReturnNode(parameters, parametersPath, workflow.Inputs, variables, nodeAnalysis, node, issues);
            }
        }
    }

    private void ValidateIfNode(
        JsonObject parameters,
        string parametersPath,
        IReadOnlyDictionary<string, WorkflowInputDefinition> workflowInputs,
        IReadOnlyDictionary<string, JsonNode?> variables,
        NodeAnalysis nodeAnalysis,
        WorkflowNode node,
        List<WorkflowValidationIssue> issues)
    {
        ValidateKnownParameters(parameters, parametersPath, ["condition"], issues);
        if (!parameters.TryGetPropertyValue("condition", out JsonNode? condition))
        {
            AddError(issues, WorkflowValidationCodes.InvalidControlNodeParameterShape, "flow.if requires parameters.condition.", JsonPointer.Combine(parametersPath, "condition"));
            return;
        }

        ValidateConditionValue(condition, JsonPointer.Combine(parametersPath, "condition"), workflowInputs, variables, nodeAnalysis, node, issues);
    }

    private void ValidateSwitchNode(
        JsonObject parameters,
        string parametersPath,
        IReadOnlyDictionary<string, WorkflowInputDefinition> workflowInputs,
        IReadOnlyDictionary<string, JsonNode?> variables,
        NodeAnalysis nodeAnalysis,
        WorkflowNode node,
        List<WorkflowValidationIssue> issues)
    {
        ValidateKnownParameters(parameters, parametersPath, ["cases"], issues);
        if (!parameters.TryGetPropertyValue("cases", out JsonNode? casesNode) || casesNode is not JsonArray cases)
        {
            AddError(issues, WorkflowValidationCodes.InvalidControlNodeParameterShape, "flow.switch requires parameters.cases as an array.", JsonPointer.Combine(parametersPath, "cases"));
            return;
        }

        if (cases.Count == 0)
        {
            AddError(issues, WorkflowValidationCodes.MissingSwitchCases, "flow.switch requires at least one case.", JsonPointer.Combine(parametersPath, "cases"));
            return;
        }

        HashSet<string> caseIds = new(StringComparer.Ordinal);
        for (int index = 0; index < cases.Count; index++)
        {
            string casePath = JsonPointer.Combine(parametersPath, "cases", index.ToString());
            if (cases[index] is not JsonObject switchCase)
            {
                AddError(issues, WorkflowValidationCodes.InvalidControlNodeParameterShape, "Switch cases must be objects.", casePath);
                continue;
            }

            ValidateKnownParameters(switchCase, casePath, ["id", "when", "description"], issues);
            string? id = null;
            if (!switchCase.TryGetPropertyValue("id", out JsonNode? idNode) || idNode is null || idNode.GetValueKind() is not JsonValueKind.String)
            {
                AddError(issues, WorkflowValidationCodes.InvalidSwitchCaseId, "Switch case ID is required.", JsonPointer.Combine(casePath, "id"));
            }
            else
            {
                id = idNode.GetValue<string>();
                if (!WorkflowValidationPatterns.IsPortName(id) || string.Equals(id, "default", StringComparison.Ordinal))
                {
                    AddError(issues, WorkflowValidationCodes.InvalidSwitchCaseId, "Switch case ID is invalid or reserved.", JsonPointer.Combine(casePath, "id"));
                }
                else if (!caseIds.Add(id))
                {
                    AddError(issues, WorkflowValidationCodes.DuplicateSwitchCaseId, "Switch case IDs must be unique.", JsonPointer.Combine(casePath, "id"));
                }
            }

            if (!switchCase.TryGetPropertyValue("when", out JsonNode? when))
            {
                AddError(issues, WorkflowValidationCodes.InvalidControlNodeParameterShape, "Switch cases require `when`.", JsonPointer.Combine(casePath, "when"));
            }
            else
            {
                ValidateConditionValue(when, JsonPointer.Combine(casePath, "when"), workflowInputs, variables, nodeAnalysis, node, issues);
            }

            if (switchCase.TryGetPropertyValue("description", out JsonNode? description) &&
                (description is null || description.GetValueKind() is not JsonValueKind.String))
            {
                AddError(issues, WorkflowValidationCodes.InvalidControlNodeParameterShape, "Switch case description must be a string.", JsonPointer.Combine(casePath, "description"));
            }
        }
    }

    private void ValidateForEachNode(
        JsonObject parameters,
        string parametersPath,
        IReadOnlyDictionary<string, WorkflowInputDefinition> workflowInputs,
        IReadOnlyDictionary<string, JsonNode?> variables,
        NodeAnalysis nodeAnalysis,
        WorkflowNode node,
        List<WorkflowValidationIssue> issues)
    {
        ValidateKnownParameters(parameters, parametersPath, ["items", "execution"], issues);
        if (!parameters.TryGetPropertyValue("items", out JsonNode? items))
        {
            AddError(issues, WorkflowValidationCodes.InvalidControlNodeParameterShape, "flow.foreach requires parameters.items.", JsonPointer.Combine(parametersPath, "items"));
        }
        else
        {
            ValidateWorkflowValue(items, JsonPointer.Combine(parametersPath, "items"), workflowInputs, variables, nodeAnalysis, node, issues);
        }

        if (parameters.TryGetPropertyValue("execution", out JsonNode? execution))
        {
            ValidateForEachExecution(execution, JsonPointer.Combine(parametersPath, "execution"), issues);
        }
    }

    private void ValidateRepeatNode(
        JsonObject parameters,
        string parametersPath,
        IReadOnlyDictionary<string, WorkflowInputDefinition> workflowInputs,
        IReadOnlyDictionary<string, JsonNode?> variables,
        NodeAnalysis nodeAnalysis,
        WorkflowNode node,
        List<WorkflowValidationIssue> issues)
    {
        ValidateKnownParameters(parameters, parametersPath, ["count"], issues);
        if (!parameters.TryGetPropertyValue("count", out JsonNode? count))
        {
            AddError(issues, WorkflowValidationCodes.InvalidControlNodeParameterShape, "flow.repeat requires parameters.count.", JsonPointer.Combine(parametersPath, "count"));
            return;
        }

        string countPath = JsonPointer.Combine(parametersPath, "count");
        ValidateWorkflowValue(count, countPath, workflowInputs, variables, nodeAnalysis, node, issues);
        if (IsDynamicWorkflowValue(count))
        {
            return;
        }

        if (count is null || !IsInteger(count) || ReadInt64(count) < 0)
        {
            AddError(issues, WorkflowValidationCodes.InvalidRepeatCount, "Repeat count must be a non-negative integer literal, binding, or expression.", countPath);
        }
    }

    private void ValidateWhileNode(
        JsonObject parameters,
        string parametersPath,
        IReadOnlyDictionary<string, WorkflowInputDefinition> workflowInputs,
        IReadOnlyDictionary<string, JsonNode?> variables,
        NodeAnalysis nodeAnalysis,
        WorkflowNode node,
        List<WorkflowValidationIssue> issues)
    {
        ValidateKnownParameters(parameters, parametersPath, ["condition", "maxIterations"], issues);
        if (!parameters.TryGetPropertyValue("condition", out JsonNode? condition))
        {
            AddError(issues, WorkflowValidationCodes.InvalidControlNodeParameterShape, "flow.while requires parameters.condition.", JsonPointer.Combine(parametersPath, "condition"));
        }
        else
        {
            ValidateConditionValue(condition, JsonPointer.Combine(parametersPath, "condition"), workflowInputs, variables, nodeAnalysis, node, issues);
        }

        if (parameters.TryGetPropertyValue("maxIterations", out JsonNode? maxIterations) &&
            (maxIterations is null || !IsInteger(maxIterations) || ReadInt64(maxIterations) < 1))
        {
            AddError(issues, WorkflowValidationCodes.InvalidWhileIterationLimit, "flow.while maxIterations must be an integer greater than or equal to 1.", JsonPointer.Combine(parametersPath, "maxIterations"));
        }
    }

    private void ValidateReturnNode(
        JsonObject parameters,
        string parametersPath,
        IReadOnlyDictionary<string, WorkflowInputDefinition> workflowInputs,
        IReadOnlyDictionary<string, JsonNode?> variables,
        NodeAnalysis nodeAnalysis,
        WorkflowNode node,
        List<WorkflowValidationIssue> issues)
    {
        ValidateKnownParameters(parameters, parametersPath, ["outcome"], issues);
        if (!parameters.TryGetPropertyValue("outcome", out JsonNode? outcomeNode) || outcomeNode is not JsonObject outcome)
        {
            AddError(issues, WorkflowValidationCodes.InvalidReturnOutcome, "core.return requires parameters.outcome as an object.", JsonPointer.Combine(parametersPath, "outcome"));
            return;
        }

        string outcomePath = JsonPointer.Combine(parametersPath, "outcome");
        ValidateKnownParameters(outcome, outcomePath, ["kind", "code", "message", "data"], issues);
        if (!outcome.TryGetPropertyValue("kind", out JsonNode? kindNode) ||
            kindNode is null ||
            kindNode.GetValueKind() is not JsonValueKind.String ||
            kindNode.GetValue<string>() is not ("success" or "partial" or "requires-action" or "no-results" or "skipped"))
        {
            AddError(issues, WorkflowValidationCodes.InvalidReturnOutcome, "Return outcome kind is invalid.", JsonPointer.Combine(outcomePath, "kind"));
        }

        if (!outcome.TryGetPropertyValue("code", out JsonNode? codeNode) ||
            codeNode is null ||
            codeNode.GetValueKind() is not JsonValueKind.String ||
            string.IsNullOrWhiteSpace(codeNode.GetValue<string>()) ||
            !WorkflowValidationPatterns.IsOutputChannelName(codeNode.GetValue<string>()))
        {
            AddError(issues, WorkflowValidationCodes.InvalidReturnOutcome, "Return outcome code is invalid.", JsonPointer.Combine(outcomePath, "code"));
        }

        if (outcome.TryGetPropertyValue("message", out JsonNode? message))
        {
            ValidateReturnMessage(message, JsonPointer.Combine(outcomePath, "message"), workflowInputs, variables, nodeAnalysis, node, issues);
        }

        if (outcome.TryGetPropertyValue("data", out JsonNode? data))
        {
            ValidateWorkflowValue(data, JsonPointer.Combine(outcomePath, "data"), workflowInputs, variables, nodeAnalysis, node, issues);
        }
    }

    private void ValidateConditionValue(
        JsonNode? value,
        string path,
        IReadOnlyDictionary<string, WorkflowInputDefinition> workflowInputs,
        IReadOnlyDictionary<string, JsonNode?> variables,
        NodeAnalysis nodeAnalysis,
        WorkflowNode node,
        List<WorkflowValidationIssue> issues)
    {
        ValidateWorkflowValue(value, path, workflowInputs, variables, nodeAnalysis, node, issues);
        if (IsBooleanLiteral(value) || IsBindingWrapper(value) || IsExpressionWrapper(value) || IsLiteralBoolean(value))
        {
            return;
        }

        AddError(issues, WorkflowValidationCodes.InvalidConditionValue, "Condition value must be a boolean literal, binding, or expression.", path);
    }

    private void ValidateReturnMessage(
        JsonNode? value,
        string path,
        IReadOnlyDictionary<string, WorkflowInputDefinition> workflowInputs,
        IReadOnlyDictionary<string, JsonNode?> variables,
        NodeAnalysis nodeAnalysis,
        WorkflowNode node,
        List<WorkflowValidationIssue> issues)
    {
        ValidateWorkflowValue(value, path, workflowInputs, variables, nodeAnalysis, node, issues);
        if (value is not null &&
            (value.GetValueKind() is JsonValueKind.String || IsBindingWrapper(value) || IsExpressionWrapper(value) || IsLiteralString(value)))
        {
            return;
        }

        AddError(issues, WorkflowValidationCodes.InvalidReturnOutcome, "Return outcome message must be a string literal, binding, or expression.", path);
    }

    private static void ValidateForEachExecution(JsonNode? executionNode, string executionPath, List<WorkflowValidationIssue> issues)
    {
        if (executionNode is not JsonObject execution)
        {
            AddError(issues, WorkflowValidationCodes.InvalidForEachExecutionPolicy, "Foreach execution policy must be an object.", executionPath);
            return;
        }

        ValidateKnownParameters(execution, executionPath, ["mode", "maxConcurrency"], issues, WorkflowValidationCodes.InvalidForEachExecutionPolicy);

        string mode = "sequential";
        if (execution.TryGetPropertyValue("mode", out JsonNode? modeNode))
        {
            if (modeNode is null || modeNode.GetValueKind() is not JsonValueKind.String)
            {
                AddError(issues, WorkflowValidationCodes.InvalidForEachExecutionPolicy, "Foreach execution mode must be a string.", JsonPointer.Combine(executionPath, "mode"));
            }
            else
            {
                mode = modeNode.GetValue<string>();
            }
        }

        if (mode is not ("sequential" or "parallel"))
        {
            AddError(issues, WorkflowValidationCodes.InvalidForEachExecutionPolicy, "Foreach execution mode is invalid.", JsonPointer.Combine(executionPath, "mode"));
        }

        bool hasMaxConcurrency = execution.TryGetPropertyValue("maxConcurrency", out JsonNode? maxConcurrency);
        if (mode == "sequential" && hasMaxConcurrency)
        {
            AddError(issues, WorkflowValidationCodes.InvalidForEachExecutionPolicy, "Sequential foreach execution must not declare maxConcurrency.", executionPath);
        }

        if (mode == "parallel" && !hasMaxConcurrency)
        {
            AddError(issues, WorkflowValidationCodes.InvalidForEachExecutionPolicy, "Parallel foreach execution requires maxConcurrency.", JsonPointer.Combine(executionPath, "maxConcurrency"));
        }

        if (hasMaxConcurrency && (maxConcurrency is null || !IsInteger(maxConcurrency) || ReadInt64(maxConcurrency) < 1))
        {
            AddError(issues, WorkflowValidationCodes.InvalidForEachExecutionPolicy, "maxConcurrency must be an integer greater than or equal to 1.", JsonPointer.Combine(executionPath, "maxConcurrency"));
        }
    }

    private static void ValidateKnownParameters(
        JsonObject parameters,
        string path,
        string[] known,
        List<WorkflowValidationIssue> issues,
        string code = WorkflowValidationCodes.InvalidControlNodeParameterShape)
    {
        foreach (KeyValuePair<string, JsonNode?> parameter in parameters)
        {
            if (!known.Contains(parameter.Key, StringComparer.Ordinal))
            {
                AddError(issues, code, "Unknown reserved parameter property is not allowed.", JsonPointer.Combine(path, parameter.Key));
            }
        }
    }

    private static void ValidateReservedControlPorts(
        WorkflowConnection connection,
        int connectionIndex,
        NodeAnalysis nodeAnalysis,
        List<WorkflowValidationIssue> issues)
    {
        if (TryGetNode(nodeAnalysis, connection.From.Node, out WorkflowNode? sourceNode))
        {
            string sourcePath = JsonPointer.Combine("connections", connectionIndex.ToString(), "from", "port");
            if (sourceNode.Type == _returnNodeType)
            {
                AddError(issues, WorkflowValidationCodes.OutgoingConnectionFromReturn, "core.return nodes must not have outgoing connections.", JsonPointer.Combine("connections", connectionIndex.ToString(), "from", "node"));
            }
            else if (sourceNode.Type == _ifNodeType && connection.From.Port is not ("true" or "false"))
            {
                AddError(issues, WorkflowValidationCodes.InvalidConditionalOutputPort, "flow.if output ports are true and false.", sourcePath);
            }
            else if (sourceNode.Type == _switchNodeType)
            {
                if (!IsValidSwitchOutputPort(sourceNode, connection.From.Port))
                {
                    AddError(issues, WorkflowValidationCodes.InvalidConditionalOutputPort, "flow.switch output ports are declared case IDs and default.", sourcePath);
                }
            }
            else if (IsIterationNodeType(sourceNode.Type) && connection.From.Port is not ("body" or "completed"))
            {
                AddError(issues, WorkflowValidationCodes.InvalidLoopControlPort, "Loop output ports are body and completed.", sourcePath);
            }
            else if (sourceNode.Type == _interactionRequestNodeType && connection.From.Port != "result")
            {
                AddError(issues, WorkflowValidationCodes.InvalidInteractionPort, "interaction.request output port must be result.", sourcePath);
            }
        }

        if (TryGetNode(nodeAnalysis, connection.To.Node, out WorkflowNode? targetNode))
        {
            string targetPath = JsonPointer.Combine("connections", connectionIndex.ToString(), "to", "port");
            if (IsIterationNodeType(targetNode.Type))
            {
                if (connection.To.Port is not ("main" or "continue" or "break"))
                {
                    AddError(issues, WorkflowValidationCodes.InvalidLoopControlPort, "Loop input ports are main, continue, and break.", targetPath);
                }
            }
            else if ((targetNode.Type is _ifNodeType or _switchNodeType or _returnNodeType or _interactionRequestNodeType) && connection.To.Port != "main")
            {
                string code = targetNode.Type == _interactionRequestNodeType
                    ? WorkflowValidationCodes.InvalidInteractionPort
                    : WorkflowValidationCodes.InvalidReservedControlInputPort;
                AddError(issues, code, "Reserved input port must be main.", targetPath);
            }
        }
    }

    private static bool IsValidSwitchOutputPort(WorkflowNode switchNode, string port)
    {
        if (port == "default")
        {
            return true;
        }

        if (!switchNode.Parameters.TryGetPropertyValue("cases", out JsonNode? casesNode) || casesNode is not JsonArray cases)
        {
            return false;
        }

        foreach (JsonNode? item in cases)
        {
            if (item is JsonObject switchCase &&
                switchCase.TryGetPropertyValue("id", out JsonNode? idNode) &&
                idNode is not null &&
                idNode.GetValueKind() is JsonValueKind.String &&
                string.Equals(idNode.GetValue<string>(), port, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidateIterationReference(string? iterationId, string path, NodeAnalysis nodeAnalysis, List<WorkflowValidationIssue> issues)
    {
        if (iterationId is null)
        {
            return;
        }

        if (!TryGetNode(nodeAnalysis, iterationId, out WorkflowNode? iterationNode) || !IsIterationNodeType(iterationNode.Type))
        {
            AddError(issues, WorkflowValidationCodes.UnknownIterationReference, "Iteration reference must target an existing iteration node.", path);
        }
    }

    private static bool IsReservedControlNode(string nodeType)
    {
        return nodeType is _ifNodeType or _switchNodeType or _forEachNodeType or _repeatNodeType or _whileNodeType or _returnNodeType;
    }

    private static bool IsIterationNodeType(string nodeType)
    {
        return nodeType is _forEachNodeType or _repeatNodeType or _whileNodeType;
    }

    private static bool IsDynamicWorkflowValue(JsonNode? value)
    {
        return IsBindingWrapper(value) || IsExpressionWrapper(value);
    }

    private static bool IsBindingWrapper(JsonNode? value)
    {
        return value is JsonObject jsonObject && jsonObject.ContainsKey("$binding");
    }

    private static bool IsExpressionWrapper(JsonNode? value)
    {
        return value is JsonObject jsonObject && jsonObject.ContainsKey("$expression");
    }

    private static bool IsLiteralBoolean(JsonNode? value)
    {
        return value is JsonObject { Count: 1 } jsonObject &&
            jsonObject.TryGetPropertyValue("$literal", out JsonNode? literal) &&
            IsBooleanLiteral(literal);
    }

    private static bool IsLiteralString(JsonNode? value)
    {
        return value is JsonObject { Count: 1 } jsonObject &&
            jsonObject.TryGetPropertyValue("$literal", out JsonNode? literal) &&
            literal is not null &&
            literal.GetValueKind() is JsonValueKind.String;
    }

    private static bool IsBooleanLiteral(JsonNode? value)
    {
        return value is not null && value.GetValueKind() is JsonValueKind.True or JsonValueKind.False;
    }

    private static void ValidateInvocationStreamPolicy(
        JsonObject parameters,
        string parametersPath,
        HashSet<string> parentStreamChannels,
        List<WorkflowValidationIssue> issues)
    {
        if (!parameters.TryGetPropertyValue("streams", out JsonNode? streamsNode))
        {
            return;
        }

        string streamsPath = JsonPointer.Combine(parametersPath, "streams");
        if (streamsNode is not JsonObject streams)
        {
            AddError(issues, WorkflowValidationCodes.InvalidInvocationStreamPolicy, "workflow.invoke streams must be an object.", streamsPath);
            return;
        }

        foreach (KeyValuePair<string, JsonNode?> property in streams)
        {
            if (property.Key is not ("mode" or "mappings"))
            {
                AddError(issues, WorkflowValidationCodes.InvalidInvocationStreamPolicy, "Unknown invocation stream policy property is not allowed.", JsonPointer.Combine(streamsPath, property.Key));
            }
        }

        string mode = "forward";
        if (streams.TryGetPropertyValue("mode", out JsonNode? modeNode))
        {
            if (modeNode is null || modeNode.GetValueKind() is not JsonValueKind.String)
            {
                AddError(issues, WorkflowValidationCodes.InvalidInvocationStreamPolicy, "Invocation stream mode must be a string.", JsonPointer.Combine(streamsPath, "mode"));
            }
            else
            {
                mode = modeNode.GetValue<string>();
            }
        }

        if (mode is not ("forward" or "suppress" or "map"))
        {
            AddError(issues, WorkflowValidationCodes.InvalidInvocationStreamPolicy, "Invocation stream mode is invalid.", JsonPointer.Combine(streamsPath, "mode"));
        }

        bool hasMappings = streams.TryGetPropertyValue("mappings", out JsonNode? mappingsNode);
        var mappings = mappingsNode as JsonObject;
        if (hasMappings && mappings is null)
        {
            AddError(issues, WorkflowValidationCodes.InvalidInvocationStreamPolicy, "Invocation stream mappings must be an object.", JsonPointer.Combine(streamsPath, "mappings"));
            return;
        }

        if (mode is "forward" or "suppress")
        {
            if (hasMappings)
            {
                AddError(issues, WorkflowValidationCodes.InvalidInvocationStreamPolicy, "Forward and suppress stream policies must not declare mappings.", streamsPath);
            }
        }
        else if (mode is "map")
        {
            if (!hasMappings || mappings!.Count == 0)
            {
                AddError(issues, WorkflowValidationCodes.InvalidInvocationStreamPolicy, "Map stream policies require at least one mapping.", streamsPath);
                return;
            }
        }

        if (mappings is null)
        {
            return;
        }

        foreach (KeyValuePair<string, JsonNode?> mapping in mappings)
        {
            string mappingPath = JsonPointer.Combine(streamsPath, "mappings", mapping.Key);
            if (!WorkflowValidationPatterns.IsOutputChannelName(mapping.Key))
            {
                AddError(issues, WorkflowValidationCodes.InvalidInvocationStreamChannel, "Invocation stream mapping source channel has an invalid format.", mappingPath);
            }

            if (mapping.Value is null || mapping.Value.GetValueKind() is not JsonValueKind.String)
            {
                AddError(issues, WorkflowValidationCodes.InvalidInvocationStreamChannel, "Invocation stream mapping target channel must be a string.", mappingPath);
                continue;
            }

            string target = mapping.Value.GetValue<string>();
            if (!WorkflowValidationPatterns.IsOutputChannelName(target))
            {
                AddError(issues, WorkflowValidationCodes.InvalidInvocationStreamChannel, "Invocation stream mapping target channel has an invalid format.", mappingPath);
            }
            else if (!parentStreamChannels.Contains(target))
            {
                AddError(issues, WorkflowValidationCodes.UndeclaredParentStreamChannel, "Invocation stream mapping target must be declared by a parent stream output.", mappingPath);
            }
        }
    }

    private static void ValidateDesigner(
        WorkflowDesignerMetadata? designer,
        IReadOnlyDictionary<string, int> nodeIds,
        List<WorkflowValidationIssue> issues)
    {
        if (designer is null)
        {
            return;
        }

        foreach (KeyValuePair<string, WorkflowNodePosition> position in designer.Positions)
        {
            if (!nodeIds.ContainsKey(position.Key))
            {
                AddWarning(issues, WorkflowValidationCodes.DesignerPositionUnknownNode, "Designer position references an unknown node.", JsonPointer.Combine("designer", "positions", position.Key));
            }

            if (!double.IsFinite(position.Value.X))
            {
                AddWarning(issues, WorkflowValidationCodes.InvalidDesignerPosition, "Designer position X must be finite.", JsonPointer.Combine("designer", "positions", position.Key, "x"));
            }

            if (!double.IsFinite(position.Value.Y))
            {
                AddWarning(issues, WorkflowValidationCodes.InvalidDesignerPosition, "Designer position Y must be finite.", JsonPointer.Combine("designer", "positions", position.Key, "y"));
            }
        }

        foreach (KeyValuePair<string, WorkflowNodeSize> size in designer.Sizes)
        {
            if (!nodeIds.ContainsKey(size.Key))
            {
                AddWarning(issues, WorkflowValidationCodes.DesignerSizeUnknownNode, "Designer size references an unknown node.", JsonPointer.Combine("designer", "sizes", size.Key));
            }

            if (!double.IsFinite(size.Value.Width) || size.Value.Width <= 0)
            {
                AddWarning(issues, WorkflowValidationCodes.InvalidDesignerSize, "Designer size width must be finite and greater than zero.", JsonPointer.Combine("designer", "sizes", size.Key, "width"));
            }

            if (!double.IsFinite(size.Value.Height) || size.Value.Height <= 0)
            {
                AddWarning(issues, WorkflowValidationCodes.InvalidDesignerSize, "Designer size height must be finite and greater than zero.", JsonPointer.Combine("designer", "sizes", size.Key, "height"));
            }
        }
    }

    private static bool CanAnalyzeReachability(
        IReadOnlyList<WorkflowNode> nodes,
        IReadOnlyList<WorkflowConnection> connections,
        NodeAnalysis nodeAnalysis,
        out int startIndex)
    {
        startIndex = -1;

        if (nodeAnalysis.StartNodeIndexes.Count != 1 || nodeAnalysis.DuplicateNodeIds.Count > 0)
        {
            return false;
        }

        startIndex = nodeAnalysis.StartNodeIndexes[0];
        if (startIndex < 0 || startIndex >= nodes.Count)
        {
            return false;
        }

        WorkflowNode? startNode = nodes[startIndex];
        if (startNode is null ||
            string.IsNullOrWhiteSpace(startNode.Id) ||
            !WorkflowValidationPatterns.IsWorkflowOrNodeId(startNode.Id))
        {
            return false;
        }

        foreach (WorkflowNode? node in nodes)
        {
            if (node is null ||
                string.IsNullOrWhiteSpace(node.Id) ||
                !WorkflowValidationPatterns.IsWorkflowOrNodeId(node.Id))
            {
                return false;
            }
        }

        foreach (WorkflowConnection connection in connections)
        {
            if (string.IsNullOrWhiteSpace(connection.From.Node) ||
                string.IsNullOrWhiteSpace(connection.To.Node) ||
                !nodeAnalysis.NodeIds.ContainsKey(connection.From.Node) ||
                !nodeAnalysis.NodeIds.ContainsKey(connection.To.Node))
            {
                return false;
            }
        }

        return true;
    }

    private static bool JsonPointerIsValid(string value)
    {
        if (value.Length == 0)
        {
            return true;
        }

        if (!value.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        string[] tokens = value[1..].Split('/');
        foreach (string token in tokens)
        {
            if (token == "-")
            {
                return false;
            }

            for (int index = 0; index < token.Length; index++)
            {
                if (token[index] == '~')
                {
                    if (index + 1 >= token.Length || token[index + 1] is not ('0' or '1'))
                    {
                        return false;
                    }

                    index++;
                }
            }
        }

        return true;
    }

    private static string CombinePointer(string basePath, string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return basePath;
        }

        if (string.IsNullOrEmpty(basePath))
        {
            return relativePath;
        }

        return basePath + relativePath;
    }

    private static bool DefaultMatchesType(JsonNode defaultValue, WorkflowInputType inputType)
    {
        JsonValueKind kind = GetValueKind(defaultValue);

        return inputType switch
        {
            WorkflowInputType.String => kind == JsonValueKind.String,
            WorkflowInputType.Integer => IsInteger(defaultValue),
            WorkflowInputType.Number => IsFiniteNumber(defaultValue),
            WorkflowInputType.Boolean => kind is JsonValueKind.True or JsonValueKind.False,
            WorkflowInputType.Object => kind == JsonValueKind.Object,
            WorkflowInputType.Array => kind == JsonValueKind.Array,
            _ => false,
        };
    }

    private static JsonValueKind GetValueKind(JsonNode value)
    {
        return value.GetValueKind();
    }

    private static bool IsInteger(JsonNode value)
    {
        if (GetValueKind(value) != JsonValueKind.Number || value is not JsonValue jsonValue)
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

    private static long ReadInt64(JsonNode value)
    {
        if (value is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<JsonElement>(out JsonElement element) && element.TryGetInt64(out long elementValue))
            {
                return elementValue;
            }

            if (jsonValue.TryGetValue<long>(out long longValue))
            {
                return longValue;
            }

            if (jsonValue.TryGetValue<int>(out int intValue))
            {
                return intValue;
            }

            if (jsonValue.TryGetValue<decimal>(out decimal decimalValue))
            {
                return decimal.ToInt64(decimalValue);
            }

            if (jsonValue.TryGetValue<double>(out double doubleValue))
            {
                return Convert.ToInt64(doubleValue, System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return 0;
    }

    private static bool IsFiniteNumber(JsonNode value)
    {
        if (GetValueKind(value) != JsonValueKind.Number || value is not JsonValue jsonValue)
        {
            return false;
        }

        if (jsonValue.TryGetValue<double>(out double doubleValue))
        {
            return double.IsFinite(doubleValue);
        }

        if (jsonValue.TryGetValue<decimal>(out _) ||
            jsonValue.TryGetValue<long>(out _) ||
            jsonValue.TryGetValue<int>(out _))
        {
            return true;
        }

        return false;
    }

    private static bool TryGetNode(NodeAnalysis nodeAnalysis, string nodeId, out WorkflowNode node)
    {
        node = null!;
        if (!nodeAnalysis.NodeIds.TryGetValue(nodeId, out int index))
        {
            return false;
        }

        node = nodeAnalysis.Nodes[index];
        return node is not null;
    }

    private static void AddError(List<WorkflowValidationIssue> issues, string code, string message, string path)
    {
        issues.Add(new WorkflowValidationIssue(code, WorkflowValidationSeverity.Error, message, path));
    }

    private static void AddWarning(List<WorkflowValidationIssue> issues, string code, string message, string path)
    {
        issues.Add(new WorkflowValidationIssue(code, WorkflowValidationSeverity.Warning, message, path));
    }

    private readonly record struct ConnectionKey(string FromNode, string FromPort, string ToNode, string ToPort);

    private sealed class NodeAnalysis
    {
        public NodeAnalysis(
            IReadOnlyDictionary<string, int> nodeIds,
            IReadOnlySet<string> duplicateNodeIds,
            IReadOnlyList<int> startNodeIndexes)
        {
            NodeIds = nodeIds;
            DuplicateNodeIds = duplicateNodeIds;
            StartNodeIndexes = startNodeIndexes;
        }

        public IReadOnlyDictionary<string, int> NodeIds { get; }

        public IReadOnlySet<string> DuplicateNodeIds { get; }

        public IReadOnlyList<int> StartNodeIndexes { get; }

        public IReadOnlyList<WorkflowNode> Nodes { get; init; } = Array.AsReadOnly(Array.Empty<WorkflowNode>());
    }
}
