using System.Text.Json.Nodes;
using SkeletonKey.Catalog;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.BuiltIns;

/// <summary>
/// Provides contract-only node definitions for reserved workflow language nodes.
/// </summary>
public static class BuiltInWorkflowNodeCatalog
{
    /// <summary>
    /// Gets the built-in catalog document.
    /// </summary>
    public static NodeCatalogDocument Document { get; } = new(
        id: "skeletonkey-builtins",
        version: "0.1.0",
        name: "SkeletonKey Built-in Nodes",
        description: "Contract-only reserved workflow language node definitions.",
        definitions:
        [
            Start(),
            End(),
            Return(),
            Invoke(),
            If(),
            Switch(),
            ForEach(),
            Repeat(),
            While(),
            InteractionRequest(),
        ]);

    /// <summary>
    /// Gets the immutable built-in catalog lookup.
    /// </summary>
    public static WorkflowNodeDefinitionCatalog Catalog { get; } = new(Document.Definitions);

    private static WorkflowNodeDefinition Start()
    {
        return new(
            "core.start",
            1,
            displayName: "Start",
            category: "core",
            outputs: OutputPorts("main"),
            behavior: new WorkflowNodeBehaviorMetadata(WorkflowNodeBehaviorKind.Entry),
            stability: WorkflowNodeStability.Preview,
            parameterExamples: [new JsonObject()]);
    }

    private static WorkflowNodeDefinition End()
    {
        return new(
            "core.end",
            1,
            displayName: "End",
            category: "core",
            inputs: InputPorts("main"),
            behavior: new WorkflowNodeBehaviorMetadata(WorkflowNodeBehaviorKind.Terminal, terminal: true),
            stability: WorkflowNodeStability.Preview,
            parameterExamples: [new JsonObject()]);
    }

    private static WorkflowNodeDefinition Return()
    {
        return new(
            "core.return",
            1,
            displayName: "Return",
            category: "core",
            parametersSchema: SchemaObject(["outcome"]),
            inputs: InputPorts("main"),
            behavior: new WorkflowNodeBehaviorMetadata(WorkflowNodeBehaviorKind.Terminal, terminal: true),
            stability: WorkflowNodeStability.Preview,
            parameterExamples:
            [
                new JsonObject
                {
                    ["outcome"] = new JsonObject
                    {
                        ["kind"] = "success",
                        ["code"] = "done",
                    },
                },
            ]);
    }

    private static WorkflowNodeDefinition Invoke()
    {
        return new(
            "workflow.invoke",
            1,
            displayName: "Invoke Workflow",
            category: "workflow",
            parametersSchema: SchemaObject(["workflow"]),
            inputs: InputPorts("main"),
            outputs: ResultPorts("result"),
            behavior: new WorkflowNodeBehaviorMetadata(WorkflowNodeBehaviorKind.Invocation),
            stability: WorkflowNodeStability.Preview,
            parameterExamples:
            [
                new JsonObject
                {
                    ["workflow"] = new JsonObject
                    {
                        ["id"] = "child-workflow",
                    },
                },
            ]);
    }

    private static WorkflowNodeDefinition If()
    {
        return new(
            "flow.if",
            1,
            displayName: "If",
            category: "flow",
            parametersSchema: SchemaObject(["condition"]),
            inputs: InputPorts("main"),
            outputs: OutputPorts("true", "false"),
            behavior: new WorkflowNodeBehaviorMetadata(WorkflowNodeBehaviorKind.Branch),
            stability: WorkflowNodeStability.Preview,
            parameterExamples:
            [
                new JsonObject
                {
                    ["condition"] = true,
                },
            ]);
    }

    private static WorkflowNodeDefinition Switch()
    {
        return new(
            "flow.switch",
            1,
            displayName: "Switch",
            category: "flow",
            parametersSchema: SchemaObject(["cases"]),
            inputs: InputPorts("main"),
            outputs: OutputPorts("default"),
            dynamicPorts:
            [
                new WorkflowDynamicPortRule(
                    WorkflowDynamicPortRuleKind.SwitchCases,
                    WorkflowPortDirection.Output,
                    "/cases",
                    "/id"),
            ],
            behavior: new WorkflowNodeBehaviorMetadata(WorkflowNodeBehaviorKind.Branch),
            stability: WorkflowNodeStability.Preview,
            parameterExamples:
            [
                new JsonObject
                {
                    ["cases"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["id"] = "matched",
                            ["when"] = true,
                        },
                    },
                },
            ]);
    }

    private static WorkflowNodeDefinition ForEach()
    {
        return new(
            "flow.foreach",
            1,
            displayName: "For Each",
            category: "flow",
            parametersSchema: SchemaObject(["items"]),
            inputs: InputPorts("main", "continue", "break"),
            outputs: OutputPorts("body", "completed"),
            behavior: new WorkflowNodeBehaviorMetadata(WorkflowNodeBehaviorKind.Loop),
            stability: WorkflowNodeStability.Preview,
            parameterExamples:
            [
                new JsonObject
                {
                    ["items"] = new JsonArray(),
                },
            ]);
    }

    private static WorkflowNodeDefinition Repeat()
    {
        return new(
            "flow.repeat",
            1,
            displayName: "Repeat",
            category: "flow",
            parametersSchema: SchemaObject(["count"]),
            inputs: InputPorts("main", "continue", "break"),
            outputs: OutputPorts("body", "completed"),
            behavior: new WorkflowNodeBehaviorMetadata(WorkflowNodeBehaviorKind.Loop),
            stability: WorkflowNodeStability.Preview,
            parameterExamples:
            [
                new JsonObject
                {
                    ["count"] = 1,
                },
            ]);
    }

    private static WorkflowNodeDefinition While()
    {
        return new(
            "flow.while",
            1,
            displayName: "While",
            category: "flow",
            parametersSchema: SchemaObject(["condition"]),
            inputs: InputPorts("main", "continue", "break"),
            outputs: OutputPorts("body", "completed"),
            behavior: new WorkflowNodeBehaviorMetadata(WorkflowNodeBehaviorKind.Loop),
            stability: WorkflowNodeStability.Preview,
            parameterExamples:
            [
                new JsonObject
                {
                    ["condition"] = true,
                },
            ]);
    }

    private static WorkflowNodeDefinition InteractionRequest()
    {
        return new(
            "interaction.request",
            1,
            displayName: "Request Interaction",
            category: "interaction",
            parametersSchema: SchemaObject(["kind", "prompt"]),
            inputs: InputPorts("main"),
            outputs: ResultPorts("result"),
            resources: new Dictionary<string, WorkflowNodeResourceRequirement>(StringComparer.Ordinal)
            {
                ["interaction"] = new(
                    "interaction",
                    StandardWorkflowResourceKinds.InteractionHandler,
                    required: false,
                    capabilities: [StandardWorkflowResourceCapabilities.InteractionConfirmation]),
            },
            behavior: new WorkflowNodeBehaviorMetadata(WorkflowNodeBehaviorKind.Interaction, maySuspend: true),
            stability: WorkflowNodeStability.Preview,
            parameterExamples:
            [
                new JsonObject
                {
                    ["kind"] = "confirmation",
                    ["prompt"] = "Continue?",
                },
            ]);
    }

    private static IReadOnlyDictionary<string, WorkflowPortDefinition> InputPorts(params string[] names)
    {
        return names.ToDictionary(
            static name => name,
            static name => new WorkflowPortDefinition(name, WorkflowPortDirection.Input),
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, WorkflowPortDefinition> OutputPorts(params string[] names)
    {
        return names.ToDictionary(
            static name => name,
            static name => new WorkflowPortDefinition(name, WorkflowPortDirection.Output),
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, WorkflowPortDefinition> ResultPorts(params string[] names)
    {
        return names.ToDictionary(
            static name => name,
            static name => new WorkflowPortDefinition(name, WorkflowPortDirection.Output, roles: ["control", "data"]),
            StringComparer.Ordinal);
    }

    private static JsonObject SchemaObject(string[] required)
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["required"] = new JsonArray([.. required.Select(static value => JsonValue.Create(value))]),
        };
    }
}
