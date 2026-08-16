using System.Text.Json.Nodes;
using SkeletonKey.Workflow.Nodes;

namespace SkeletonKey.Workflow.Tests.Invocation;

/// <summary>
/// Covers resource mappings carried by invocation node parameters.
/// </summary>
public sealed class WorkflowResourceInvocationContractTests
{
    /// <summary>
    /// Verifies invocation resource mapping order is preserved.
    /// </summary>
    [Fact]
    public void InvocationResourceMappingPreservesOrder()
    {
        JsonObject resources = new()
        {
            ["browser"] = Resource("browser"),
            ["page"] = Resource("page"),
        };

        WorkflowNode node = new("invoke", "workflow.invoke", 1, parameters: new JsonObject { ["resources"] = resources });
        JsonObject returned = node.Parameters["resources"]!.AsObject();

        Assert.Equal(["browser", "page"], [.. returned.Select(static property => property.Key)]);
    }

    /// <summary>
    /// Verifies invocation resource mappings are cloned through node parameters.
    /// </summary>
    [Fact]
    public void InvocationResourceMappingIsImmutable()
    {
        JsonObject resources = new() { ["browser"] = Resource("browser") };
        WorkflowNode node = new("invoke", "workflow.invoke", 1, parameters: new JsonObject { ["resources"] = resources });
        resources["browser"] = Resource("other");

        JsonObject returned = node.Parameters["resources"]!.AsObject();
        returned["browser"] = Resource("mutated");

        Assert.Equal("browser", node.Parameters["resources"]!["browser"]!["$resource"]!["name"]!.GetValue<string>());
    }

    private static JsonObject Resource(string name)
    {
        return new JsonObject { ["$resource"] = new JsonObject { ["name"] = name } };
    }
}
