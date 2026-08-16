using SkeletonKey.Workflow.Documents;

namespace SkeletonKey.Serialization.Json.Tests.Examples;

/// <summary>
/// Covers workflow JSON serialization behavior.
/// </summary>
public sealed class RepositoryExampleTests
{
    private readonly WorkflowJsonSerializer _serializer = new();

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public async Task RepositoryMinimalExampleDeserializesSuccessfully()
    {
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "examples", "minimal.workflow.json"));
        string json = await File.ReadAllTextAsync(path);

        WorkflowDocument workflow = _serializer.Deserialize(json);

        Assert.Equal("minimal-workflow", workflow.Id);
        Assert.Equal(3, workflow.Nodes.Count);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public async Task RepositoryMinimalExampleRoundTripsToCanonicalOutput()
    {
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "examples", "minimal.workflow.json"));
        string json = await File.ReadAllTextAsync(path);

        string canonical = _serializer.Serialize(_serializer.Deserialize(json));

        Assert.Equal(canonical, json);
    }
}


