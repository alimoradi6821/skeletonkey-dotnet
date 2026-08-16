using System.Text.Json.Nodes;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Designer;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Inputs;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Outputs;
using SkeletonKey.Workflow.Policies;

namespace SkeletonKey.Validation.Tests.Support;

internal static class ValidationTestData
{
    public static WorkflowSemanticValidator Validator { get; } = new();

    public static WorkflowDocument CreateValidWorkflow(
        string schema = "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
        string specVersion = "0.1.0",
        string id = "minimal",
        string name = "Minimal",
        IReadOnlyDictionary<string, WorkflowInputDefinition>? inputs = null,
        IReadOnlyDictionary<string, JsonNode?>? variables = null,
        IReadOnlyList<WorkflowNode>? nodes = null,
        IReadOnlyList<WorkflowConnection>? connections = null,
        IReadOnlyDictionary<string, WorkflowOutputDefinition>? outputs = null,
        WorkflowDesignerMetadata? designer = null)
    {
        WorkflowNode[] effectiveNodes =
        [
            new("start", "core.start", 1),
            new("end", "core.end", 1),
        ];

        WorkflowConnection[] effectiveConnections =
        [
            new(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint("end", "main")),
        ];

        return new WorkflowDocument(
            schema,
            specVersion,
            id,
            name,
            inputs: inputs,
            variables: variables,
            nodes: nodes ?? effectiveNodes,
            connections: connections ?? effectiveConnections,
            outputs: outputs,
            designer: designer);
    }

    public static WorkflowNode Node(
        string id,
        string type = "core.log",
        int typeVersion = 1,
        bool disabled = false,
        JsonObject? parameters = null,
        WorkflowExecutionPolicy? policy = null)
    {
        return new WorkflowNode(id, type, typeVersion, disabled: disabled, parameters: parameters, policy: policy);
    }

    public static WorkflowValidationResult Validate(WorkflowDocument workflow)
    {
        return Validator.Validate(workflow);
    }

    public static WorkflowValidationIssue Issue(string code, WorkflowValidationSeverity severity = WorkflowValidationSeverity.Error)
    {
        return new WorkflowValidationIssue(code, severity, "message", string.Empty);
    }
}
