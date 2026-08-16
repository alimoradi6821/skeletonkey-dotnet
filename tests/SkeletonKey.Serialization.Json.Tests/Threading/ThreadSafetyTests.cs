using SkeletonKey.Serialization.Json.Tests.Support;
using SkeletonKey.Workflow.Documents;

namespace SkeletonKey.Serialization.Json.Tests.Threading;

/// <summary>
/// Covers workflow JSON serialization behavior.
/// </summary>
public sealed class ThreadSafetyTests
{
    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public async Task SingleSerializerInstanceSupportsConcurrentSerialization()
    {
        WorkflowJsonSerializer serializer = new();
        WorkflowDocument workflow = WorkflowJsonTestData.CreateComplexWorkflow();

        string[] outputs = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => serializer.Serialize(workflow))));

        Assert.All(outputs, output => Assert.Equal(outputs[0], output));
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public async Task SingleSerializerInstanceSupportsConcurrentDeserialization()
    {
        WorkflowJsonSerializer serializer = new();
        string json = WorkflowJsonTestData.CreateComplexWorkflowJson();

        WorkflowDocument[] workflows = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => serializer.Deserialize(json))));

        Assert.All(workflows, workflow => Assert.Equal("complex", workflow.Id));
    }
}

