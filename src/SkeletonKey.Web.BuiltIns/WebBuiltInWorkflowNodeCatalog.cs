using System.Text.Json.Nodes;
using SkeletonKey.Catalog;
using SkeletonKey.Locators;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Web.BuiltIns;

/// <summary>
/// Provides contract-only node definitions for essential web automation nodes.
/// </summary>
public static class WebBuiltInWorkflowNodeCatalog
{
    /// <summary>Gets the web built-in catalog document.</summary>
    public static NodeCatalogDocument Document { get; } = new(
        id: "skeletonkey-web-builtins",
        version: "0.1.0",
        name: "SkeletonKey Web Built-in Nodes",
        definitions:
        [
            Definition("web.navigate", outputs: DataOutput("url"), capabilities: [StandardWorkflowResourceCapabilities.WebNavigation]),
            Definition("web.click", locatorRequired: true, capabilities: [StandardWorkflowResourceCapabilities.WebActions, StandardWorkflowResourceCapabilities.WebLocators]),
            Definition("web.fill", locatorRequired: true, capabilities: [StandardWorkflowResourceCapabilities.WebForms, StandardWorkflowResourceCapabilities.WebLocators]),
            Definition("web.press", locatorRequired: true, capabilities: [StandardWorkflowResourceCapabilities.WebActions, StandardWorkflowResourceCapabilities.WebLocators]),
            Definition("web.selectOption", locatorRequired: true, capabilities: [StandardWorkflowResourceCapabilities.WebForms, StandardWorkflowResourceCapabilities.WebLocators]),
            Definition("web.setChecked", locatorRequired: true, capabilities: [StandardWorkflowResourceCapabilities.WebForms, StandardWorkflowResourceCapabilities.WebLocators]),
            Definition("web.wait", locatorRequired: true, capabilities: [StandardWorkflowResourceCapabilities.WebLocators]),
            Definition("web.getText", locatorRequired: true, locatorUsage: LocatorUsageMode.Collection, outputs: DataOutput("result", allowsMultiple: true), capabilities: [StandardWorkflowResourceCapabilities.WebText, StandardWorkflowResourceCapabilities.WebLocators]),
            Definition("web.getAttribute", locatorRequired: true, locatorUsage: LocatorUsageMode.Collection, outputs: DataOutput("result", allowsMultiple: true), capabilities: [StandardWorkflowResourceCapabilities.WebAttributes, StandardWorkflowResourceCapabilities.WebLocators]),
            Definition("web.getCount", locatorRequired: true, locatorUsage: LocatorUsageMode.Collection, outputs: DataOutput("count"), capabilities: [StandardWorkflowResourceCapabilities.WebLocators]),
            Definition("web.screenshot", locatorRequired: false, outputs: DataOutput("image"), capabilities: [StandardWorkflowResourceCapabilities.WebScreenshot, StandardWorkflowResourceCapabilities.WebLocators]),
            Definition("web.openPage", outputs: DataOutputs("page", "url"), capabilities: [StandardWorkflowResourceCapabilities.WebNavigation]),
            Definition("web.listPages", outputs: DataOutput("pages", allowsMultiple: true), capabilities: [StandardWorkflowResourceCapabilities.WebNavigation]),
            Definition("web.activatePage", capabilities: [StandardWorkflowResourceCapabilities.WebNavigation]),
            Definition("web.closePage", capabilities: [StandardWorkflowResourceCapabilities.WebNavigation]),
            Definition("web.clickAndWaitForPopup", locatorRequired: true, outputs: DataOutputs("page", "url"), capabilities: [StandardWorkflowResourceCapabilities.WebActions, StandardWorkflowResourceCapabilities.WebLocators, StandardWorkflowResourceCapabilities.WebNavigation]),
            Definition("web.uploadFiles", locatorRequired: true, outputs: DataOutput("artifacts", allowsMultiple: true), capabilities: [StandardWorkflowResourceCapabilities.WebForms, StandardWorkflowResourceCapabilities.WebLocators]),
            Definition("web.clickAndWaitForDownload", locatorRequired: true, outputs: DataOutput("artifact"), capabilities: [StandardWorkflowResourceCapabilities.WebActions, StandardWorkflowResourceCapabilities.WebLocators]),
            Definition("web.clickAndWaitForDialog", locatorRequired: true, outputs: DataOutput("dialog"), capabilities: [StandardWorkflowResourceCapabilities.WebActions, StandardWorkflowResourceCapabilities.WebLocators]),
            Definition("web.respondDialog", capabilities: [StandardWorkflowResourceCapabilities.WebActions]),
            Definition("web.getCookies", outputs: DataOutput("cookies", allowsMultiple: true), capabilities: [StandardWorkflowResourceCapabilities.WebNavigation]),
            Definition("web.setCookies", capabilities: [StandardWorkflowResourceCapabilities.WebNavigation]),
            Definition("web.clearCookies", capabilities: [StandardWorkflowResourceCapabilities.WebNavigation]),
            Definition("web.exportStorageState", outputs: DataOutput("artifact"), capabilities: [StandardWorkflowResourceCapabilities.WebNavigation]),
            Definition("web.importStorageState", outputs: DataOutput("context"), capabilities: [StandardWorkflowResourceCapabilities.WebNavigation]),
            Definition("web.waitForUrl", capabilities: [StandardWorkflowResourceCapabilities.WebNavigation]),
            Definition("web.waitForLoadState", capabilities: [StandardWorkflowResourceCapabilities.WebNavigation]),
        ]);

    /// <summary>Gets the immutable web built-in catalog lookup.</summary>
    public static WorkflowNodeDefinitionCatalog Catalog { get; } = new(Document.Definitions);

    private static WorkflowNodeDefinition Definition(string type, bool locatorRequired = false, LocatorUsageMode locatorUsage = LocatorUsageMode.Single, IReadOnlyDictionary<string, WorkflowPortDefinition>? outputs = null, IReadOnlyList<string>? capabilities = null)
    {
        Dictionary<string, WorkflowPortDefinition> allOutputs = new(StringComparer.Ordinal)
        {
            ["continue"] = new("continue", WorkflowPortDirection.Output),
        };
        if (outputs is not null)
        {
            foreach (KeyValuePair<string, WorkflowPortDefinition> output in outputs)
            {
                allOutputs[output.Key] = output.Value;
            }
        }

        Dictionary<string, NodeLocatorSlotDefinition> locators = new(StringComparer.Ordinal);
        if (type != "web.navigate")
        {
            locators["target"] = new("target", "/target", locatorRequired, locatorUsage, Accepted(locatorUsage), "Target locator.");
        }

        if (SupportsFrameSlots(type))
        {
            for (int index = 1; index <= 5; index++)
            {
                string name = "frame" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                locators[name] = new(name, "/" + name, required: false, LocatorUsageMode.Single, [LocatorCardinality.One], "Optional frame locator.");
            }
        }

        return new WorkflowNodeDefinition(
            type,
            1,
            displayName: type,
            category: "web",
            parametersSchema: new JsonObject { ["type"] = "object" },
            inputs: new Dictionary<string, WorkflowPortDefinition>(StringComparer.Ordinal) { ["main"] = new("main", WorkflowPortDirection.Input) },
            outputs: allOutputs,
            resources: new Dictionary<string, WorkflowNodeResourceRequirement>(StringComparer.Ordinal)
            {
                ["page"] = new("page", StandardWorkflowResourceKinds.WebPage, capabilities: capabilities),
            },
            capabilities: capabilities,
            behavior: new WorkflowNodeBehaviorMetadata(WorkflowNodeBehaviorKind.Action),
            stability: WorkflowNodeStability.Preview,
            parameterExamples: [new JsonObject { ["page"] = Resource("page") }],
            locators: locators);
    }

    private static IReadOnlyDictionary<string, WorkflowPortDefinition> DataOutput(string name, bool allowsMultiple = false)
    {
        return new Dictionary<string, WorkflowPortDefinition>(StringComparer.Ordinal)
        {
            [name] = new(name, WorkflowPortDirection.Output, allowsMultiple: allowsMultiple, roles: ["data"]),
        };
    }

    private static IReadOnlyDictionary<string, WorkflowPortDefinition> DataOutputs(params string[] names)
    {
        return names.ToDictionary(
            static name => name,
            static name => new WorkflowPortDefinition(name, WorkflowPortDirection.Output, roles: ["data"]),
            StringComparer.Ordinal);
    }

    private static IReadOnlyList<LocatorCardinality> Accepted(LocatorUsageMode usage)
    {
        return usage == LocatorUsageMode.Single
            ? [LocatorCardinality.One, LocatorCardinality.ZeroOrOne, LocatorCardinality.OneOrMore, LocatorCardinality.Many]
            : [LocatorCardinality.One, LocatorCardinality.ZeroOrOne, LocatorCardinality.OneOrMore, LocatorCardinality.Many];
    }

    private static bool SupportsFrameSlots(string type)
    {
        return type is "web.click" or "web.fill" or "web.press" or "web.selectOption" or "web.setChecked" or "web.wait" or "web.getText" or "web.getAttribute" or "web.getCount" or "web.screenshot" or
            "web.uploadFiles" or "web.clickAndWaitForDownload" or "web.clickAndWaitForPopup" or "web.clickAndWaitForDialog";
    }

    private static JsonObject Resource(string name)
    {
        return new JsonObject { ["$resource"] = new JsonObject { ["name"] = name } };
    }
}
