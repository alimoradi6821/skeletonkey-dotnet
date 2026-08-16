using System.Text.Json.Nodes;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Workflow.Tests.Resources;

/// <summary>
/// Covers workflow resource and reference contracts.
/// </summary>
public sealed class WorkflowResourceContractTests
{
    /// <summary>
    /// Verifies workflow documents default resources to an empty immutable collection.
    /// </summary>
    [Fact]
    public void WorkflowDocumentDefaultsResourcesToEmpty()
    {
        WorkflowDocument document = new(id: "workflow", name: "Workflow", nodes: [new WorkflowNode("start", "core.start", 1)], connections: []);

        Assert.Empty(document.Resources);
    }

    /// <summary>
    /// Verifies workflow documents defensively copy resource dictionaries.
    /// </summary>
    [Fact]
    public void WorkflowDocumentDefensivelyCopiesResources()
    {
        Dictionary<string, WorkflowResourceDefinition> resources = new()
        {
            ["browser"] = new WorkflowResourceDefinition(StandardWorkflowResourceKinds.WebBrowser),
        };

        WorkflowDocument document = new(id: "workflow", name: "Workflow", resources: resources, nodes: [new WorkflowNode("start", "core.start", 1)], connections: []);
        resources["other"] = new WorkflowResourceDefinition(StandardWorkflowResourceKinds.WebPage);

        Assert.Single(document.Resources);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, WorkflowResourceDefinition>)document.Resources).Add("x", new WorkflowResourceDefinition("web.page")));
    }

    /// <summary>
    /// Verifies resource definition defaults are stable.
    /// </summary>
    [Fact]
    public void ResourceDefinitionDefaultsAreStable()
    {
        WorkflowResourceDefinition definition = new(StandardWorkflowResourceKinds.WebBrowser);

        Assert.Equal(WorkflowResourceLifetime.Invocation, definition.Lifetime);
        Assert.Equal(WorkflowResourceAccessMode.Exclusive, definition.Access);
        Assert.True(definition.Required);
        Assert.Empty(definition.Capabilities);
        Assert.Null(definition.Constraints);
    }

    /// <summary>
    /// Verifies resource capabilities are defensively copied.
    /// </summary>
    [Fact]
    public void ResourceCapabilitiesAreDefensivelyCopied()
    {
        List<string> capabilities = [StandardWorkflowResourceCapabilities.WebHeadful];
        WorkflowResourceDefinition definition = new(StandardWorkflowResourceKinds.WebBrowser, capabilities: capabilities);
        capabilities.Add(StandardWorkflowResourceCapabilities.WebDownloads);

        Assert.Equal([StandardWorkflowResourceCapabilities.WebHeadful], definition.Capabilities);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)definition.Capabilities).Add("web.frames"));
    }

    /// <summary>
    /// Verifies resource constraints are defensively cloned.
    /// </summary>
    [Fact]
    public void ResourceConstraintsAreDefensivelyCloned()
    {
        JsonObject constraints = new() { ["engine"] = "chromium" };
        WorkflowResourceDefinition definition = new(StandardWorkflowResourceKinds.WebBrowser, constraints: constraints);
        constraints["engine"] = "firefox";

        JsonObject returned = definition.Constraints!;
        returned["engine"] = "webkit";

        Assert.Equal("chromium", definition.Constraints!["engine"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies resource references preserve names.
    /// </summary>
    [Fact]
    public void ResourceReferencesPreserveResourceNames()
    {
        WorkflowResourceReference reference = new("browser");

        Assert.Equal("browser", reference.Name);
    }
}
