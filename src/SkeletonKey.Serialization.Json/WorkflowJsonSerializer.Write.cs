using System.Text.Json;
using System.Text.Json.Nodes;
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
    private static void WriteWorkflowDocument(Utf8JsonWriter writer, WorkflowDocument workflow)
    {
        writer.WriteStartObject();
        writer.WriteString("$schema", workflow.Schema);
        writer.WriteString("specVersion", workflow.SpecVersion);
        writer.WriteString("id", workflow.Id);
        writer.WriteString("name", workflow.Name);
        WriteOptionalString(writer, "description", workflow.Description);
        WriteInputs(writer, workflow.Inputs);
        WriteVariables(writer, workflow.Variables);
        WriteResources(writer, workflow.Resources);
        WriteNodes(writer, workflow.Nodes);
        WriteConnections(writer, workflow.Connections);
        WriteOutputs(writer, workflow.Outputs);
        WriteDesigner(writer, workflow.Designer);
        writer.WriteEndObject();
    }

    private static void WriteInputs(Utf8JsonWriter writer, IReadOnlyDictionary<string, WorkflowInputDefinition> inputs)
    {
        writer.WritePropertyName("inputs");
        writer.WriteStartObject();

        foreach (KeyValuePair<string, WorkflowInputDefinition> input in inputs)
        {
            writer.WritePropertyName(input.Key);
            writer.WriteStartObject();
            writer.WriteString("type", WriteInputType(input.Value.Type));
            writer.WriteBoolean("required", input.Value.Required);
            if (input.Value.HasDefault)
            {
                writer.WritePropertyName("default");
                WriteJsonNode(writer, input.Value.Default);
            }

            WriteOptionalString(writer, "description", input.Value.Description);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteVariables(Utf8JsonWriter writer, IReadOnlyDictionary<string, JsonNode?> variables)
    {
        writer.WritePropertyName("variables");
        writer.WriteStartObject();

        foreach (KeyValuePair<string, JsonNode?> variable in variables)
        {
            writer.WritePropertyName(variable.Key);
            WriteJsonNode(writer, variable.Value);
        }

        writer.WriteEndObject();
    }

    private static void WriteResources(Utf8JsonWriter writer, IReadOnlyDictionary<string, WorkflowResourceDefinition> resources)
    {
        writer.WritePropertyName("resources");
        writer.WriteStartObject();

        foreach (KeyValuePair<string, WorkflowResourceDefinition> resource in resources)
        {
            writer.WritePropertyName(resource.Key);
            writer.WriteStartObject();
            writer.WriteString("kind", resource.Value.Kind);
            writer.WriteString("lifetime", WriteResourceLifetime(resource.Value.Lifetime));
            writer.WriteString("access", WriteResourceAccess(resource.Value.Access));
            writer.WriteBoolean("required", resource.Value.Required);
            writer.WritePropertyName("capabilities");
            writer.WriteStartArray();
            foreach (string capability in resource.Value.Capabilities)
            {
                writer.WriteStringValue(capability);
            }

            writer.WriteEndArray();
            if (resource.Value.Constraints is JsonObject constraints)
            {
                writer.WritePropertyName("constraints");
                WriteJsonNode(writer, constraints);
            }

            WriteOptionalString(writer, "description", resource.Value.Description);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteNodes(Utf8JsonWriter writer, IReadOnlyList<WorkflowNode> nodes)
    {
        writer.WritePropertyName("nodes");
        writer.WriteStartArray();

        foreach (WorkflowNode node in nodes)
        {
            writer.WriteStartObject();
            writer.WriteString("id", node.Id);
            writer.WriteString("type", node.Type);
            writer.WriteNumber("typeVersion", node.TypeVersion);
            WriteOptionalString(writer, "displayName", node.DisplayName);
            WriteOptionalString(writer, "description", node.Description);
            writer.WriteBoolean("disabled", node.Disabled);
            writer.WritePropertyName("parameters");
            WriteParameters(writer, node);
            WritePolicy(writer, node.Policy);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteParameters(Utf8JsonWriter writer, WorkflowNode node)
    {
        JsonObject parameters = node.Parameters;
        if (string.Equals(node.Type, "workflow.invoke", StringComparison.Ordinal))
        {
            WriteInvocationParameters(writer, parameters);
            return;
        }

        if (node.Type is "flow.if" or "flow.switch" or "flow.foreach" or "flow.repeat" or "flow.while" or "core.return")
        {
            WriteControlParameters(writer, node.Type, parameters);
            return;
        }

        if (string.Equals(node.Type, "interaction.request", StringComparison.Ordinal))
        {
            WriteInteractionRequestParameters(writer, parameters);
            return;
        }

        if (!string.Equals(node.Type, "workflow.invoke", StringComparison.Ordinal))
        {
            parameters.WriteTo(writer);
            return;
        }
    }

    private static void WriteInvocationParameters(Utf8JsonWriter writer, JsonObject parameters)
    {
        writer.WriteStartObject();
        if (parameters.TryGetPropertyValue("workflow", out JsonNode? workflow))
        {
            writer.WritePropertyName("workflow");
            WriteWorkflowReferenceValue(writer, workflow);
        }

        if (parameters.TryGetPropertyValue("inputs", out JsonNode? inputs))
        {
            writer.WritePropertyName("inputs");
            WriteWorkflowValue(writer, inputs);
        }

        if (parameters.TryGetPropertyValue("resources", out JsonNode? resources))
        {
            writer.WritePropertyName("resources");
            WriteInvocationResourceMappings(writer, resources);
        }

        if (parameters.TryGetPropertyValue("streams", out JsonNode? streams))
        {
            writer.WritePropertyName("streams");
            WriteInvocationStreams(writer, streams);
        }

        foreach (KeyValuePair<string, JsonNode?> property in parameters)
        {
            if (property.Key is "workflow" or "inputs" or "resources" or "streams")
            {
                continue;
            }

            writer.WritePropertyName(property.Key);
            WriteJsonNode(writer, property.Value);
        }

        writer.WriteEndObject();
    }

    private static void WriteControlParameters(Utf8JsonWriter writer, string nodeType, JsonObject parameters)
    {
        writer.WriteStartObject();
        if (nodeType is "flow.if")
        {
            WriteControlProperty(writer, parameters, "condition");
        }
        else if (nodeType is "flow.switch")
        {
            if (parameters.TryGetPropertyValue("cases", out JsonNode? cases))
            {
                writer.WritePropertyName("cases");
                WriteSwitchCases(writer, cases);
            }
        }
        else if (nodeType is "flow.foreach")
        {
            WriteControlProperty(writer, parameters, "items");
            if (parameters.TryGetPropertyValue("execution", out JsonNode? execution))
            {
                writer.WritePropertyName("execution");
                WriteForEachExecution(writer, execution);
            }
        }
        else if (nodeType is "flow.repeat")
        {
            WriteControlProperty(writer, parameters, "count");
        }
        else if (nodeType is "flow.while")
        {
            WriteControlProperty(writer, parameters, "condition");
            WriteControlProperty(writer, parameters, "maxIterations");
        }
        else if (nodeType is "core.return")
        {
            if (parameters.TryGetPropertyValue("outcome", out JsonNode? outcome))
            {
                writer.WritePropertyName("outcome");
                WriteReturnOutcome(writer, outcome);
            }
        }

        foreach (KeyValuePair<string, JsonNode?> property in parameters)
        {
            if (IsKnownControlParameter(nodeType, property.Key))
            {
                continue;
            }

            writer.WritePropertyName(property.Key);
            WriteJsonNode(writer, property.Value);
        }

        writer.WriteEndObject();
    }

    private static bool IsKnownControlParameter(string nodeType, string propertyName)
    {
        return nodeType switch
        {
            "flow.if" => propertyName is "condition",
            "flow.switch" => propertyName is "cases",
            "flow.foreach" => propertyName is "items" or "execution",
            "flow.repeat" => propertyName is "count",
            "flow.while" => propertyName is "condition" or "maxIterations",
            "core.return" => propertyName is "outcome",
            _ => false,
        };
    }

    private static void WriteControlProperty(Utf8JsonWriter writer, JsonObject parameters, string propertyName)
    {
        if (!parameters.TryGetPropertyValue(propertyName, out JsonNode? value))
        {
            return;
        }

        writer.WritePropertyName(propertyName);
        WriteWorkflowValue(writer, value);
    }

    private static void WriteSwitchCases(Utf8JsonWriter writer, JsonNode? value)
    {
        if (value is not JsonArray cases)
        {
            WriteJsonNode(writer, value);
            return;
        }

        writer.WriteStartArray();
        foreach (JsonNode? item in cases)
        {
            if (item is not JsonObject switchCase)
            {
                WriteJsonNode(writer, item);
                continue;
            }

            writer.WriteStartObject();
            WriteJsonObjectProperty(writer, switchCase, "id");
            if (switchCase.TryGetPropertyValue("when", out JsonNode? when))
            {
                writer.WritePropertyName("when");
                WriteWorkflowValue(writer, when);
            }

            WriteJsonObjectProperty(writer, switchCase, "description");
            foreach (KeyValuePair<string, JsonNode?> property in switchCase)
            {
                if (property.Key is "id" or "when" or "description")
                {
                    continue;
                }

                writer.WritePropertyName(property.Key);
                WriteJsonNode(writer, property.Value);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteForEachExecution(Utf8JsonWriter writer, JsonNode? value)
    {
        if (value is not JsonObject execution)
        {
            WriteJsonNode(writer, value);
            return;
        }

        writer.WriteStartObject();
        WriteJsonObjectProperty(writer, execution, "mode");
        WriteJsonObjectProperty(writer, execution, "maxConcurrency");
        foreach (KeyValuePair<string, JsonNode?> property in execution)
        {
            if (property.Key is "mode" or "maxConcurrency")
            {
                continue;
            }

            writer.WritePropertyName(property.Key);
            WriteJsonNode(writer, property.Value);
        }

        writer.WriteEndObject();
    }

    private static void WriteReturnOutcome(Utf8JsonWriter writer, JsonNode? value)
    {
        if (value is not JsonObject outcome)
        {
            WriteJsonNode(writer, value);
            return;
        }

        writer.WriteStartObject();
        WriteJsonObjectProperty(writer, outcome, "kind");
        WriteJsonObjectProperty(writer, outcome, "code");
        if (outcome.TryGetPropertyValue("message", out JsonNode? message))
        {
            writer.WritePropertyName("message");
            WriteWorkflowValue(writer, message);
        }

        if (outcome.TryGetPropertyValue("data", out JsonNode? data))
        {
            writer.WritePropertyName("data");
            WriteWorkflowValue(writer, data);
        }

        foreach (KeyValuePair<string, JsonNode?> property in outcome)
        {
            if (property.Key is "kind" or "code" or "message" or "data")
            {
                continue;
            }

            writer.WritePropertyName(property.Key);
            WriteJsonNode(writer, property.Value);
        }

        writer.WriteEndObject();
    }

    private static void WriteJsonObjectProperty(Utf8JsonWriter writer, JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out JsonNode? value))
        {
            return;
        }

        writer.WritePropertyName(propertyName);
        WriteJsonNode(writer, value);
    }

    private static void WriteWorkflowReferenceValue(Utf8JsonWriter writer, JsonNode? value)
    {
        if (value is not JsonObject workflow)
        {
            WriteJsonNode(writer, value);
            return;
        }

        writer.WriteStartObject();
        if (workflow.TryGetPropertyValue("id", out JsonNode? id))
        {
            writer.WritePropertyName("id");
            WriteJsonNode(writer, id);
        }

        if (workflow.TryGetPropertyValue("version", out JsonNode? version))
        {
            writer.WritePropertyName("version");
            WriteJsonNode(writer, version);
        }

        foreach (KeyValuePair<string, JsonNode?> property in workflow)
        {
            if (property.Key is "id" or "version")
            {
                continue;
            }

            writer.WritePropertyName(property.Key);
            WriteJsonNode(writer, property.Value);
        }

        writer.WriteEndObject();
    }

    private static void WriteInvocationStreams(Utf8JsonWriter writer, JsonNode? value)
    {
        if (value is not JsonObject streams)
        {
            WriteJsonNode(writer, value);
            return;
        }

        writer.WriteStartObject();
        if (streams.TryGetPropertyValue("mode", out JsonNode? mode))
        {
            writer.WritePropertyName("mode");
            WriteJsonNode(writer, mode);
        }

        if (streams.TryGetPropertyValue("mappings", out JsonNode? mappings))
        {
            writer.WritePropertyName("mappings");
            WriteJsonNode(writer, mappings);
        }

        foreach (KeyValuePair<string, JsonNode?> property in streams)
        {
            if (property.Key is "mode" or "mappings")
            {
                continue;
            }

            writer.WritePropertyName(property.Key);
            WriteJsonNode(writer, property.Value);
        }

        writer.WriteEndObject();
    }

    private static void WriteInteractionRequestParameters(Utf8JsonWriter writer, JsonObject parameters)
    {
        writer.WriteStartObject();
        WriteJsonObjectProperty(writer, parameters, "kind");
        if (parameters.TryGetPropertyValue("prompt", out JsonNode? prompt))
        {
            writer.WritePropertyName("prompt");
            WriteWorkflowValue(writer, prompt);
        }

        if (parameters.TryGetPropertyValue("description", out JsonNode? description))
        {
            writer.WritePropertyName("description");
            WriteWorkflowValue(writer, description);
        }

        if (parameters.TryGetPropertyValue("options", out JsonNode? options))
        {
            writer.WritePropertyName("options");
            WriteInteractionOptions(writer, options);
        }

        if (parameters.ContainsKey("default"))
        {
            writer.WritePropertyName("default");
            WriteJsonNode(writer, parameters["default"]);
        }

        WriteJsonObjectProperty(writer, parameters, "required");
        WriteJsonObjectProperty(writer, parameters, "timeout");

        foreach (KeyValuePair<string, JsonNode?> property in parameters)
        {
            if (property.Key is "kind" or "prompt" or "description" or "options" or "default" or "required" or "timeout")
            {
                continue;
            }

            writer.WritePropertyName(property.Key);
            WriteJsonNode(writer, property.Value);
        }

        writer.WriteEndObject();
    }

    private static void WriteInteractionOptions(Utf8JsonWriter writer, JsonNode? value)
    {
        if (value is not JsonArray options)
        {
            WriteJsonNode(writer, value);
            return;
        }

        writer.WriteStartArray();
        foreach (JsonNode? item in options)
        {
            if (item is not JsonObject option)
            {
                WriteJsonNode(writer, item);
                continue;
            }

            writer.WriteStartObject();
            WriteJsonObjectProperty(writer, option, "id");
            WriteJsonObjectProperty(writer, option, "label");
            WriteJsonObjectProperty(writer, option, "description");
            if (option.ContainsKey("value"))
            {
                writer.WritePropertyName("value");
                WriteJsonNode(writer, option["value"]);
            }

            foreach (KeyValuePair<string, JsonNode?> property in option)
            {
                if (property.Key is "id" or "label" or "description" or "value")
                {
                    continue;
                }

                writer.WritePropertyName(property.Key);
                WriteJsonNode(writer, property.Value);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteInvocationResourceMappings(Utf8JsonWriter writer, JsonNode? value)
    {
        if (value is not JsonObject resources)
        {
            WriteJsonNode(writer, value);
            return;
        }

        writer.WriteStartObject();
        foreach (KeyValuePair<string, JsonNode?> resource in resources)
        {
            writer.WritePropertyName(resource.Key);
            WriteWorkflowValue(writer, resource.Value);
        }

        writer.WriteEndObject();
    }

    private static void WriteWorkflowValue(Utf8JsonWriter writer, JsonNode? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        if (value is JsonArray array)
        {
            writer.WriteStartArray();
            foreach (JsonNode? item in array)
            {
                WriteWorkflowValue(writer, item);
            }

            writer.WriteEndArray();
            return;
        }

        if (value is not JsonObject jsonObject)
        {
            value.WriteTo(writer);
            return;
        }

        if (jsonObject.ContainsKey("$binding") && jsonObject.Count == 1)
        {
            WriteBindingWrapper(writer, jsonObject);
            return;
        }

        if (jsonObject.ContainsKey("$expression") && jsonObject.Count == 1)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("$expression");
            WriteJsonNode(writer, jsonObject["$expression"]);
            writer.WriteEndObject();
            return;
        }

        if (jsonObject.ContainsKey("$resource") && jsonObject.Count == 1)
        {
            WriteResourceWrapper(writer, jsonObject);
            return;
        }

        if (jsonObject.ContainsKey("$locator") && jsonObject.Count == 1)
        {
            WriteLocatorWrapper(writer, jsonObject);
            return;
        }

        if (jsonObject.ContainsKey("$literal") && jsonObject.Count == 1)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("$literal");
            WriteJsonNode(writer, jsonObject["$literal"]);
            writer.WriteEndObject();
            return;
        }

        writer.WriteStartObject();
        foreach (KeyValuePair<string, JsonNode?> property in jsonObject)
        {
            writer.WritePropertyName(property.Key);
            WriteWorkflowValue(writer, property.Value);
        }

        writer.WriteEndObject();
    }

    private static void WriteBindingWrapper(Utf8JsonWriter writer, JsonObject wrapper)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("$binding");

        if (wrapper["$binding"] is not JsonObject binding)
        {
            WriteJsonNode(writer, wrapper["$binding"]);
            writer.WriteEndObject();
            return;
        }

        writer.WriteStartObject();
        WriteBindingProperty(writer, binding, "source");
        WriteBindingProperty(writer, binding, "name");
        WriteBindingProperty(writer, binding, "node");
        WriteBindingProperty(writer, binding, "port");
        WriteBindingProperty(writer, binding, "iteration");
        WriteBindingProperty(writer, binding, "path");
        WriteBindingProperty(writer, binding, "onMissing");
        WriteBindingProperty(writer, binding, "default");

        foreach (KeyValuePair<string, JsonNode?> property in binding)
        {
            if (property.Key is "source" or "name" or "node" or "port" or "iteration" or "path" or "onMissing" or "default")
            {
                continue;
            }

            writer.WritePropertyName(property.Key);
            WriteJsonNode(writer, property.Value);
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteResourceWrapper(Utf8JsonWriter writer, JsonObject wrapper)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("$resource");
        if (wrapper["$resource"] is not JsonObject reference)
        {
            WriteJsonNode(writer, wrapper["$resource"]);
            writer.WriteEndObject();
            return;
        }

        writer.WriteStartObject();
        WriteJsonObjectProperty(writer, reference, "name");
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteLocatorWrapper(Utf8JsonWriter writer, JsonObject wrapper)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("$locator");
        if (wrapper["$locator"] is not JsonObject reference)
        {
            WriteJsonNode(writer, wrapper["$locator"]);
            writer.WriteEndObject();
            return;
        }

        writer.WriteStartObject();
        WriteJsonObjectProperty(writer, reference, "catalog");
        WriteJsonObjectProperty(writer, reference, "version");
        WriteJsonObjectProperty(writer, reference, "id");
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteBindingProperty(Utf8JsonWriter writer, JsonObject binding, string propertyName)
    {
        if (!binding.TryGetPropertyValue(propertyName, out JsonNode? value))
        {
            return;
        }

        writer.WritePropertyName(propertyName);
        WriteJsonNode(writer, value);
    }

    private static void WriteConnections(Utf8JsonWriter writer, IReadOnlyList<WorkflowConnection> connections)
    {
        writer.WritePropertyName("connections");
        writer.WriteStartArray();

        foreach (WorkflowConnection connection in connections)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("from");
            WriteEndpoint(writer, connection.From);
            writer.WritePropertyName("to");
            WriteEndpoint(writer, connection.To);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteEndpoint(Utf8JsonWriter writer, WorkflowEndpoint endpoint)
    {
        writer.WriteStartObject();
        writer.WriteString("node", endpoint.Node);
        writer.WriteString("port", endpoint.Port);
        writer.WriteEndObject();
    }

    private static void WriteOutputs(Utf8JsonWriter writer, IReadOnlyDictionary<string, WorkflowOutputDefinition> outputs)
    {
        writer.WritePropertyName("outputs");
        writer.WriteStartObject();

        foreach (KeyValuePair<string, WorkflowOutputDefinition> output in outputs)
        {
            writer.WritePropertyName(output.Key);
            writer.WriteStartObject();
            writer.WriteString("mode", WriteOutputMode(output.Value.Mode));
            if (output.Value.From.HasValue)
            {
                writer.WritePropertyName("from");
                WriteEndpoint(writer, output.Value.From.Value);
            }

            WriteOptionalString(writer, "channel", output.Value.Channel);
            WriteOptionalString(writer, "description", output.Value.Description);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WritePolicy(Utf8JsonWriter writer, WorkflowExecutionPolicy? policy)
    {
        if (policy is null)
        {
            return;
        }

        writer.WritePropertyName("policy");
        writer.WriteStartObject();
        WriteOptionalString(writer, "timeout", policy.Timeout);
        writer.WriteString("onError", WriteOnError(policy.OnError));
        WriteRetry(writer, policy.Retry);
        writer.WriteEndObject();
    }

    private static void WriteRetry(Utf8JsonWriter writer, WorkflowRetryPolicy? retry)
    {
        if (retry is null)
        {
            return;
        }

        writer.WritePropertyName("retry");
        writer.WriteStartObject();
        writer.WriteNumber("maxAttempts", retry.MaxAttempts);
        WriteOptionalString(writer, "delay", retry.Delay);
        writer.WriteNumber("backoff", retry.Backoff);
        WriteOptionalString(writer, "maxDelay", retry.MaxDelay);
        writer.WriteEndObject();
    }

    private static void WriteDesigner(Utf8JsonWriter writer, WorkflowDesignerMetadata? designer)
    {
        if (designer is null)
        {
            return;
        }

        writer.WritePropertyName("designer");
        writer.WriteStartObject();
        writer.WritePropertyName("positions");
        writer.WriteStartObject();
        foreach (KeyValuePair<string, WorkflowNodePosition> position in designer.Positions)
        {
            writer.WritePropertyName(position.Key);
            writer.WriteStartObject();
            writer.WriteNumber("x", position.Value.X);
            writer.WriteNumber("y", position.Value.Y);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.WritePropertyName("sizes");
        writer.WriteStartObject();
        foreach (KeyValuePair<string, WorkflowNodeSize> size in designer.Sizes)
        {
            writer.WritePropertyName(size.Key);
            writer.WriteStartObject();
            writer.WriteNumber("width", size.Value.Width);
            writer.WriteNumber("height", size.Value.Height);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
