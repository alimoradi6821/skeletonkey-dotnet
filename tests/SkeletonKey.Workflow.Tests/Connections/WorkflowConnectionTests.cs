using SkeletonKey.Workflow.Connections;

namespace SkeletonKey.Workflow.Tests.Connections;

/// <summary>
/// Covers endpoint and connection value behavior.
/// </summary>
public sealed class WorkflowConnectionTests
{
    /// <summary>
    /// Verifies the source endpoint is preserved.
    /// </summary>
    [Fact]
    public void PreservesSourceEndpoint()
    {
        WorkflowEndpoint source = new("start", "main");
        WorkflowConnection connection = new(source, new WorkflowEndpoint("end", "main"));

        Assert.Equal(source, connection.From);
    }

    /// <summary>
    /// Verifies the target endpoint is preserved.
    /// </summary>
    [Fact]
    public void PreservesTargetEndpoint()
    {
        WorkflowEndpoint target = new("end", "main");
        WorkflowConnection connection = new(new WorkflowEndpoint("start", "main"), target);

        Assert.Equal(target, connection.To);
    }

    /// <summary>
    /// Verifies endpoints compare structurally.
    /// </summary>
    [Fact]
    public void EndpointsProvideStructuralEquality()
    {
        Assert.Equal(new WorkflowEndpoint("node", "main"), new WorkflowEndpoint("node", "main"));
        Assert.NotEqual(new WorkflowEndpoint("node", "main"), new WorkflowEndpoint("node", "alternate"));
    }

    /// <summary>
    /// Verifies connections compare structurally.
    /// </summary>
    [Fact]
    public void ConnectionsSupportStructuralComparison_WhenAppropriate()
    {
        WorkflowConnection first = new(
            new WorkflowEndpoint("start", "main"),
            new WorkflowEndpoint("end", "main"));
        WorkflowConnection second = new(
            new WorkflowEndpoint("start", "main"),
            new WorkflowEndpoint("end", "main"));

        Assert.Equal(first, second);
    }
}
