using SkeletonKey.Workflow.Designer;

namespace SkeletonKey.Workflow.Tests.Designer;

/// <summary>
/// Covers optional designer metadata behavior.
/// </summary>
public sealed class WorkflowDesignerMetadataTests
{
    /// <summary>
    /// Verifies omitted designer collections become empty.
    /// </summary>
    [Fact]
    public void DefaultsPositionAndSizeCollectionsToEmpty()
    {
        WorkflowDesignerMetadata metadata = new();

        Assert.Empty(metadata.Positions);
        Assert.Empty(metadata.Sizes);
    }

    /// <summary>
    /// Verifies source position dictionaries are defensively copied.
    /// </summary>
    [Fact]
    public void DefensivelyCopiesPositions()
    {
        Dictionary<string, WorkflowNodePosition> positions = new()
        {
            ["start"] = new WorkflowNodePosition(1, 2),
        };

        WorkflowDesignerMetadata metadata = new(positions: positions);

        positions["end"] = new WorkflowNodePosition(3, 4);

        Assert.Single(metadata.Positions);
        Assert.Equal(new WorkflowNodePosition(1, 2), metadata.Positions["start"]);
    }

    /// <summary>
    /// Verifies source size dictionaries are defensively copied.
    /// </summary>
    [Fact]
    public void DefensivelyCopiesSizes()
    {
        Dictionary<string, WorkflowNodeSize> sizes = new()
        {
            ["start"] = new WorkflowNodeSize(100, 80),
        };

        WorkflowDesignerMetadata metadata = new(sizes: sizes);

        sizes["end"] = new WorkflowNodeSize(120, 90);

        Assert.Single(metadata.Sizes);
        Assert.Equal(new WorkflowNodeSize(100, 80), metadata.Sizes["start"]);
    }

    /// <summary>
    /// Verifies a node may have a position without a size.
    /// </summary>
    [Fact]
    public void AllowsPositionWithoutSize()
    {
        WorkflowDesignerMetadata metadata = new(
            positions: new Dictionary<string, WorkflowNodePosition>
            {
                ["start"] = new WorkflowNodePosition(1, 2),
            });

        Assert.True(metadata.Positions.ContainsKey("start"));
        Assert.False(metadata.Sizes.ContainsKey("start"));
    }

    /// <summary>
    /// Verifies a node may have a size without a position.
    /// </summary>
    [Fact]
    public void AllowsSizeWithoutPosition()
    {
        WorkflowDesignerMetadata metadata = new(
            sizes: new Dictionary<string, WorkflowNodeSize>
            {
                ["start"] = new WorkflowNodeSize(100, 80),
            });

        Assert.True(metadata.Sizes.ContainsKey("start"));
        Assert.False(metadata.Positions.ContainsKey("start"));
    }
}
