using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Outputs;

namespace SkeletonKey.Workflow.Tests.Outputs;

/// <summary>
/// Covers workflow output declaration construction.
/// </summary>
public sealed class WorkflowOutputDefinitionTests
{
    /// <summary>
    /// Verifies single outputs preserve their source endpoint.
    /// </summary>
    [Fact]
    public void SingleOutputPreservesSourceEndpoint()
    {
        WorkflowEndpoint endpoint = new("node", "main");
        WorkflowOutputDefinition output = new(WorkflowOutputMode.Single, endpoint);

        Assert.Equal(WorkflowOutputMode.Single, output.Mode);
        Assert.Equal(endpoint, output.From);
    }

    /// <summary>
    /// Verifies collection outputs preserve their source endpoint.
    /// </summary>
    [Fact]
    public void CollectionOutputPreservesSourceEndpoint()
    {
        WorkflowEndpoint endpoint = new("node", "items");
        WorkflowOutputDefinition output = new(WorkflowOutputMode.Collection, endpoint);

        Assert.Equal(WorkflowOutputMode.Collection, output.Mode);
        Assert.Equal(endpoint, output.From);
    }

    /// <summary>
    /// Verifies stream outputs preserve their channel.
    /// </summary>
    [Fact]
    public void StreamOutputPreservesChannel()
    {
        WorkflowOutputDefinition output = new(WorkflowOutputMode.Stream, channel: "records");

        Assert.Equal(WorkflowOutputMode.Stream, output.Mode);
        Assert.Equal("records", output.Channel);
    }

    /// <summary>
    /// Verifies output descriptions are optional.
    /// </summary>
    [Fact]
    public void OutputDescriptionRemainsOptional()
    {
        WorkflowOutputDefinition output = new(WorkflowOutputMode.Single, new WorkflowEndpoint("node", "main"));

        Assert.Null(output.Description);
    }
}
