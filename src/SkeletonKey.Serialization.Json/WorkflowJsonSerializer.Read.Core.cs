using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Serialization.Json.Internal;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Designer;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Inputs;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Outputs;
using SkeletonKey.Workflow.Policies;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Serialization.Json;

public sealed partial class WorkflowJsonSerializer
{
    private static WorkflowDocument ReadWorkflowDocument(JsonElement element, string path)
    {
        RequireObject(element, path);
        RejectUnknownProperties(element, path, ["$schema", "specVersion", "id", "name", "description", "inputs", "variables", "resources", "nodes", "connections", "outputs", "designer"]);

        return new WorkflowDocument(
            schema: ReadRequiredString(element, "$schema", Append(path, "$schema")),
            specVersion: ReadRequiredString(element, "specVersion", Append(path, "specVersion")),
            id: ReadRequiredString(element, "id", Append(path, "id")),
            name: ReadRequiredString(element, "name", Append(path, "name")),
            description: ReadOptionalString(element, "description", Append(path, "description")),
            inputs: ReadInputs(element, path),
            variables: ReadVariables(element, path),
            resources: ReadResources(element, path),
            nodes: ReadNodes(element, path),
            connections: ReadConnections(element, path),
            outputs: ReadOutputs(element, path),
            designer: ReadDesigner(element, path));
    }

    private static IReadOnlyDictionary<string, WorkflowInputDefinition> ReadInputs(JsonElement element, string path)
    {
        if (!element.TryGetProperty("inputs", out JsonElement inputsElement))
        {
            return new Dictionary<string, WorkflowInputDefinition>();
        }

        string inputsPath = Append(path, "inputs");
        RequireObject(inputsElement, inputsPath);
        Dictionary<string, WorkflowInputDefinition> inputs = new(StringComparer.Ordinal);

        foreach (JsonProperty inputProperty in inputsElement.EnumerateObject())
        {
            inputs[inputProperty.Name] = ReadInputDefinition(inputProperty.Value, Append(inputsPath, inputProperty.Name));
        }

        return inputs;
    }

    private static WorkflowInputDefinition ReadInputDefinition(JsonElement element, string path)
    {
        RequireObject(element, path);
        RejectUnknownProperties(element, path, ["type", "required", "default", "description"]);

        bool hasDefault = element.TryGetProperty("default", out JsonElement defaultElement);
        return new WorkflowInputDefinition(
            ReadInputType(ReadRequiredString(element, "type", Append(path, "type")), Append(path, "type")),
            ReadOptionalBoolean(element, "required", Append(path, "required"), defaultValue: false),
            hasDefault ? ToJsonNode(defaultElement) : null,
            ReadOptionalString(element, "description", Append(path, "description")),
            hasDefault);
    }

    private static IReadOnlyDictionary<string, JsonNode?> ReadVariables(JsonElement element, string path)
    {
        if (!element.TryGetProperty("variables", out JsonElement variablesElement))
        {
            return new Dictionary<string, JsonNode?>();
        }

        string variablesPath = Append(path, "variables");
        RequireObject(variablesElement, variablesPath);
        Dictionary<string, JsonNode?> variables = new(StringComparer.Ordinal);

        foreach (JsonProperty variableProperty in variablesElement.EnumerateObject())
        {
            variables[variableProperty.Name] = ToJsonNode(variableProperty.Value);
        }

        return variables;
    }

    private static IReadOnlyDictionary<string, WorkflowResourceDefinition> ReadResources(JsonElement element, string path)
    {
        if (!element.TryGetProperty("resources", out JsonElement resourcesElement))
        {
            return new Dictionary<string, WorkflowResourceDefinition>();
        }

        string resourcesPath = Append(path, "resources");
        RequireObject(resourcesElement, resourcesPath);
        Dictionary<string, WorkflowResourceDefinition> resources = new(StringComparer.Ordinal);

        foreach (JsonProperty resourceProperty in resourcesElement.EnumerateObject())
        {
            resources[resourceProperty.Name] = ReadResourceDefinition(resourceProperty.Value, Append(resourcesPath, resourceProperty.Name));
        }

        return resources;
    }

    private static WorkflowResourceDefinition ReadResourceDefinition(JsonElement element, string path)
    {
        RequireObject(element, path);
        RejectUnknownProperties(element, path, ["kind", "lifetime", "access", "required", "capabilities", "constraints", "description"]);

        return new WorkflowResourceDefinition(
            ReadRequiredString(element, "kind", Append(path, "kind")),
            element.TryGetProperty("lifetime", out JsonElement lifetimeElement)
                ? ReadResourceLifetime(ReadRequiredStringValue(lifetimeElement, Append(path, "lifetime")), Append(path, "lifetime"))
                : WorkflowResourceLifetime.Invocation,
            element.TryGetProperty("access", out JsonElement accessElement)
                ? ReadResourceAccess(ReadRequiredStringValue(accessElement, Append(path, "access")), Append(path, "access"))
                : WorkflowResourceAccessMode.Exclusive,
            ReadOptionalBoolean(element, "required", Append(path, "required"), defaultValue: true),
            ReadResourceCapabilities(element, path),
            ReadOptionalJsonObject(element, "constraints", Append(path, "constraints")),
            ReadOptionalString(element, "description", Append(path, "description")));
    }

    private static IReadOnlyList<string> ReadResourceCapabilities(JsonElement element, string path)
    {
        if (!element.TryGetProperty("capabilities", out JsonElement capabilitiesElement))
        {
            return [];
        }

        string capabilitiesPath = Append(path, "capabilities");
        RequireArray(capabilitiesElement, capabilitiesPath);
        List<string> capabilities = [];
        int index = 0;
        foreach (JsonElement capabilityElement in capabilitiesElement.EnumerateArray())
        {
            capabilities.Add(ReadRequiredStringValue(capabilityElement, Append(capabilitiesPath, index)));
            index++;
        }

        return capabilities;
    }

    private static IReadOnlyList<WorkflowNode> ReadNodes(JsonElement element, string path)
    {
        JsonElement nodesElement = ReadRequiredProperty(element, "nodes", Append(path, "nodes"));
        RequireArray(nodesElement, Append(path, "nodes"));
        List<WorkflowNode> nodes = [];
        int index = 0;

        foreach (JsonElement nodeElement in nodesElement.EnumerateArray())
        {
            string nodePath = Append(Append(path, "nodes"), index);
            if (nodeElement.ValueKind is JsonValueKind.Null)
            {
                throw JsonExceptionFactory.Create("Workflow node entries cannot be null.", nodePath);
            }

            nodes.Add(ReadNode(nodeElement, nodePath));
            index++;
        }

        return nodes;
    }

    private static IReadOnlyDictionary<string, WorkflowOutputDefinition> ReadOutputs(JsonElement element, string path)
    {
        if (!element.TryGetProperty("outputs", out JsonElement outputsElement))
        {
            return new Dictionary<string, WorkflowOutputDefinition>();
        }

        string outputsPath = Append(path, "outputs");
        RequireObject(outputsElement, outputsPath);
        Dictionary<string, WorkflowOutputDefinition> outputs = new(StringComparer.Ordinal);

        foreach (JsonProperty outputProperty in outputsElement.EnumerateObject())
        {
            outputs[outputProperty.Name] = ReadOutputDefinition(outputProperty.Value, Append(outputsPath, outputProperty.Name));
        }

        return outputs;
    }

    private static WorkflowOutputDefinition ReadOutputDefinition(JsonElement element, string path)
    {
        RequireObject(element, path);
        RejectUnknownProperties(element, path, ["mode", "from", "channel", "description"]);

        return new WorkflowOutputDefinition(
            ReadOutputMode(ReadRequiredString(element, "mode", Append(path, "mode")), Append(path, "mode")),
            element.TryGetProperty("from", out JsonElement fromElement)
                ? ReadEndpoint(fromElement, Append(path, "from"))
                : null,
            ReadOptionalNonNullString(element, "channel", Append(path, "channel")),
            ReadOptionalNonNullString(element, "description", Append(path, "description")));
    }

    private static WorkflowNode ReadNode(JsonElement element, string path)
    {
        RequireObject(element, path);
        RejectUnknownProperties(element, path, ["id", "type", "typeVersion", "displayName", "description", "disabled", "parameters", "policy"]);

        return new WorkflowNode(
            ReadRequiredString(element, "id", Append(path, "id")),
            ReadRequiredString(element, "type", Append(path, "type")),
            ReadRequiredInt32(element, "typeVersion", Append(path, "typeVersion")),
            ReadOptionalString(element, "displayName", Append(path, "displayName")),
            ReadOptionalString(element, "description", Append(path, "description")),
            ReadOptionalBoolean(element, "disabled", Append(path, "disabled"), defaultValue: false),
            ReadParameters(element, path),
            ReadPolicy(element, path));
    }

    private static JsonObject ReadParameters(JsonElement element, string path)
    {
        if (!element.TryGetProperty("parameters", out JsonElement parametersElement))
        {
            return [];
        }

        string parametersPath = Append(path, "parameters");
        RequireObject(parametersElement, parametersPath);
        return (JsonObject)(ToJsonNode(parametersElement) ?? new JsonObject());
    }

    private static WorkflowExecutionPolicy? ReadPolicy(JsonElement element, string path)
    {
        if (!element.TryGetProperty("policy", out JsonElement policyElement))
        {
            return null;
        }

        string policyPath = Append(path, "policy");
        if (policyElement.ValueKind is JsonValueKind.Null)
        {
            return null;
        }

        RequireObject(policyElement, policyPath);
        RejectUnknownProperties(policyElement, policyPath, ["timeout", "onError", "retry"]);

        return new WorkflowExecutionPolicy(
            ReadOptionalString(policyElement, "timeout", Append(policyPath, "timeout")),
            policyElement.TryGetProperty("onError", out JsonElement onErrorElement)
                ? ReadOnError(ReadRequiredStringValue(onErrorElement, Append(policyPath, "onError")), Append(policyPath, "onError"))
                : WorkflowOnError.Fail,
            ReadRetry(policyElement, policyPath));
    }

    private static WorkflowRetryPolicy? ReadRetry(JsonElement element, string path)
    {
        if (!element.TryGetProperty("retry", out JsonElement retryElement))
        {
            return null;
        }

        string retryPath = Append(path, "retry");
        if (retryElement.ValueKind is JsonValueKind.Null)
        {
            return null;
        }

        RequireObject(retryElement, retryPath);
        RejectUnknownProperties(retryElement, retryPath, ["maxAttempts", "delay", "backoff", "maxDelay"]);

        return new WorkflowRetryPolicy(
            retryElement.TryGetProperty("maxAttempts", out JsonElement maxAttemptsElement) ? ReadInt32Value(maxAttemptsElement, Append(retryPath, "maxAttempts")) : 1,
            ReadOptionalString(retryElement, "delay", Append(retryPath, "delay")),
            retryElement.TryGetProperty("backoff", out JsonElement backoffElement) ? ReadDoubleValue(backoffElement, Append(retryPath, "backoff")) : 1.0,
            ReadOptionalString(retryElement, "maxDelay", Append(retryPath, "maxDelay")));
    }
}
