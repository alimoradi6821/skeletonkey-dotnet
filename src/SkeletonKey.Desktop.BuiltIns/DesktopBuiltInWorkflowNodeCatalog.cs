using System.Text.Json.Nodes;
using SkeletonKey.Catalog;
using SkeletonKey.Locators;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Desktop.BuiltIns;

/// <summary>Provides contract-only definitions for essential Windows desktop automation nodes.</summary>
public static class DesktopBuiltInWorkflowNodeCatalog
{
    /// <summary>Gets the desktop built-in catalog document.</summary>
    public static NodeCatalogDocument Document { get; } = new(
        id: "skeletonkey-desktop-builtins",
        version: "0.1.0",
        name: "SkeletonKey Desktop Built-in Nodes",
        definitions:
        [
            Definition("desktop.click", capabilities: [StandardWorkflowResourceCapabilities.DesktopActions, StandardWorkflowResourceCapabilities.DesktopLocators]),
            Definition("desktop.fill", capabilities: [StandardWorkflowResourceCapabilities.DesktopForms, StandardWorkflowResourceCapabilities.DesktopLocators]),
            Definition("desktop.press", capabilities: [StandardWorkflowResourceCapabilities.DesktopActions, StandardWorkflowResourceCapabilities.DesktopLocators]),
            Definition("desktop.getText", LocatorUsageMode.Collection, DataOutput("result", allowsMultiple: true), [StandardWorkflowResourceCapabilities.DesktopText, StandardWorkflowResourceCapabilities.DesktopLocators]),
            Definition("desktop.getCount", LocatorUsageMode.Collection, DataOutput("count"), [StandardWorkflowResourceCapabilities.DesktopLocators]),
        ]);

    /// <summary>Gets the immutable desktop built-in catalog lookup.</summary>
    public static WorkflowNodeDefinitionCatalog Catalog { get; } = new(Document.Definitions);

    private static WorkflowNodeDefinition Definition(
        string type,
        LocatorUsageMode locatorUsage = LocatorUsageMode.Single,
        IReadOnlyDictionary<string, WorkflowPortDefinition>? dataOutputs = null,
        IReadOnlyList<string>? capabilities = null)
    {
        Dictionary<string, WorkflowPortDefinition> outputs = new(StringComparer.Ordinal)
        {
            ["continue"] = new("continue", WorkflowPortDirection.Output),
        };
        foreach (KeyValuePair<string, WorkflowPortDefinition> output in dataOutputs ?? new Dictionary<string, WorkflowPortDefinition>())
        {
            outputs[output.Key] = output.Value;
        }

        return new WorkflowNodeDefinition(
            type,
            1,
            displayName: type,
            category: "desktop",
            parametersSchema: new JsonObject { ["type"] = "object" },
            inputs: new Dictionary<string, WorkflowPortDefinition>(StringComparer.Ordinal)
            {
                ["main"] = new("main", WorkflowPortDirection.Input),
            },
            outputs: outputs,
            resources: new Dictionary<string, WorkflowNodeResourceRequirement>(StringComparer.Ordinal)
            {
                ["application"] = new("application", StandardWorkflowResourceKinds.DesktopApplication, capabilities: capabilities),
            },
            capabilities: capabilities,
            behavior: new WorkflowNodeBehaviorMetadata(WorkflowNodeBehaviorKind.Action),
            stability: WorkflowNodeStability.Preview,
            parameterExamples: [new JsonObject { ["application"] = Resource("application") }],
            locators: new Dictionary<string, NodeLocatorSlotDefinition>(StringComparer.Ordinal)
            {
                ["target"] = new("target", "/target", required: true, usage: locatorUsage, acceptedCardinalities: AcceptedCardinalities(), description: "Desktop UI Automation target."),
            });
    }

    private static IReadOnlyDictionary<string, WorkflowPortDefinition> DataOutput(string name, bool allowsMultiple = false)
    {
        return new Dictionary<string, WorkflowPortDefinition>(StringComparer.Ordinal)
        {
            [name] = new(name, WorkflowPortDirection.Output, allowsMultiple: allowsMultiple, roles: ["data"]),
        };
    }

    private static IReadOnlyList<LocatorCardinality> AcceptedCardinalities()
    {
        return [LocatorCardinality.One, LocatorCardinality.ZeroOrOne, LocatorCardinality.OneOrMore, LocatorCardinality.Many];
    }

    private static JsonObject Resource(string name)
    {
        return new JsonObject { ["$resource"] = new JsonObject { ["name"] = name } };
    }
}
