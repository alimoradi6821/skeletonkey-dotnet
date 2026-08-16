using SkeletonKey.Serialization.Json.Tests.Support;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Inputs;
using SkeletonKey.Workflow.Policies;

namespace SkeletonKey.Serialization.Json.Tests.Basic;

/// <summary>
/// Covers workflow JSON serialization behavior.
/// </summary>
public sealed class BasicSerializationTests
{
    private readonly WorkflowJsonSerializer _serializer = new();

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void SerializesUsingCanonicalRootPropertyOrder()
    {
        string json = _serializer.Serialize(WorkflowJsonTestData.CreateRepositoryExampleWorkflow(), indented: false);

        AssertInOrder(json, "\"$schema\"", "\"specVersion\"", "\"id\"", "\"name\"", "\"inputs\"", "\"variables\"", "\"nodes\"", "\"connections\"", "\"outputs\"", "\"designer\"");
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void SerializesNodePropertiesInCanonicalOrder()
    {
        string json = _serializer.Serialize(WorkflowJsonTestData.CreateComplexWorkflow(), indented: false);

        AssertInOrder(json, "\"id\":\"log\"", "\"type\"", "\"typeVersion\"", "\"displayName\"", "\"disabled\"", "\"parameters\"", "\"policy\"");
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void SerializesConnectionAndEndpointPropertiesInCanonicalOrder()
    {
        string json = _serializer.Serialize(WorkflowJsonTestData.CreateMinimalWorkflow(), indented: false);

        AssertInOrder(json, "\"from\"", "\"node\":\"start\"", "\"port\":\"main\"", "\"to\"", "\"node\":\"end\"");
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void SerializesEnumValuesAsLanguageStrings()
    {
        string json = _serializer.Serialize(WorkflowJsonTestData.CreateComplexWorkflow(), indented: false);

        Assert.Contains("\"type\":\"object\"", json, StringComparison.Ordinal);
        Assert.Contains("\"onError\":\"continue\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"onError\":1", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void SerializesSchemaAsDollarSchema()
    {
        string json = _serializer.Serialize(WorkflowJsonTestData.CreateMinimalWorkflow(), indented: false);

        Assert.Contains("\"$schema\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"schema\"", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void OmitsNullablePropertiesWhenNull()
    {
        string json = _serializer.Serialize(WorkflowJsonTestData.CreateMinimalWorkflow(), indented: false);

        Assert.DoesNotContain("description", json, StringComparison.Ordinal);
        Assert.DoesNotContain("displayName", json, StringComparison.Ordinal);
        Assert.DoesNotContain("policy", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void PreservesNodeOrder()
    {
        string json = _serializer.Serialize(WorkflowJsonTestData.CreateRepositoryExampleWorkflow(), indented: false);

        AssertInOrder(json, "\"id\":\"start\"", "\"id\":\"log\"", "\"id\":\"end\"");
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void PreservesConnectionOrder()
    {
        string json = _serializer.Serialize(WorkflowJsonTestData.CreateRepositoryExampleWorkflow(), indented: false);

        AssertInOrder(json, "\"node\":\"start\"", "\"node\":\"log\"", "\"node\":\"log\"", "\"node\":\"end\"");
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void UsesIndentedOutputWhenRequested()
    {
        string json = _serializer.Serialize(WorkflowJsonTestData.CreateMinimalWorkflow(), indented: true);

        Assert.Contains("\n", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\r\n", json, StringComparison.Ordinal);
        Assert.Contains("  \"$schema\"", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void UsesCompactOutputWhenRequested()
    {
        string json = _serializer.Serialize(WorkflowJsonTestData.CreateMinimalWorkflow(), indented: false);

        Assert.DoesNotContain("\r\n", json, StringComparison.Ordinal);
        Assert.DoesNotContain("  ", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies canonical serialization ends with exactly one LF.
    /// </summary>
    [Fact]
    public void SerializesWithExactlyOneTrailingLf()
    {
        string json = _serializer.Serialize(WorkflowJsonTestData.CreateMinimalWorkflow());

        Assert.EndsWith("\n", json, StringComparison.Ordinal);
        Assert.False(json.EndsWith("\n\n", StringComparison.Ordinal));
        Assert.DoesNotContain("\r\n", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void ProducesDeterministicOutputAcrossRepeatedCalls()
    {
        WorkflowDocument workflow = WorkflowJsonTestData.CreateComplexWorkflow();

        Assert.Equal(_serializer.Serialize(workflow), _serializer.Serialize(workflow));
    }

    private static void AssertInOrder(string text, params string[] tokens)
    {
        int currentIndex = -1;
        foreach (string token in tokens)
        {
            int nextIndex = text.IndexOf(token, currentIndex + 1, StringComparison.Ordinal);
            Assert.True(nextIndex > currentIndex, $"Token '{token}' did not appear after index {currentIndex} in: {text}");
            currentIndex = nextIndex;
        }
    }
}

