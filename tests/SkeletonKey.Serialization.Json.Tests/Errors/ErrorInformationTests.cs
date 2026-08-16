using SkeletonKey.Serialization.Json.Tests.Support;

namespace SkeletonKey.Serialization.Json.Tests.Errors;

/// <summary>
/// Covers workflow JSON serialization behavior.
/// </summary>
public sealed class ErrorInformationTests
{
    private readonly WorkflowJsonSerializer _serializer = new();

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void ExceptionIdentifiesDeserializeOperation()
    {
        WorkflowSerializationException exception = Assert.Throws<WorkflowSerializationException>(() => _serializer.Deserialize("{ invalid"));

        Assert.Equal(WorkflowSerializationOperation.Deserialize, exception.Operation);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void ExceptionIdentifiesSerializeOperation()
    {
        WorkflowSerializationException exception = Assert.Throws<WorkflowSerializationException>(() => _serializer.Serialize(null!));

        Assert.Equal(WorkflowSerializationOperation.Serialize, exception.Operation);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void DeserializationExceptionContainsJsonPointerPathWhenAvailable()
    {
        WorkflowSerializationException exception = Assert.Throws<WorkflowSerializationException>(() => _serializer.Deserialize("""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "minimal",
              "name": "Minimal workflow",
              "nodes": [{ "id": "start", "typeVersion": 1 }],
              "connections": []
            }
            """));

        Assert.Equal("/nodes/0/type", exception.JsonPath);
        Assert.Contains("/nodes/0/type", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void DeserializationExceptionContainsLineAndBytePositionWhenAvailable()
    {
        WorkflowSerializationException exception = Assert.Throws<WorkflowSerializationException>(() => _serializer.Deserialize("""
            {
              "$schema":
            }
            """));

        Assert.NotNull(exception.LineNumber);
        Assert.NotNull(exception.BytePositionInLine);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void InnerExceptionIsPreserved()
    {
        WorkflowSerializationException exception = Assert.Throws<WorkflowSerializationException>(() => _serializer.Deserialize("{ invalid"));

        Assert.NotNull(exception.InnerException);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public async Task FileReadExceptionIdentifiesReadFileOperation()
    {
        string missingFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.workflow.json");

        WorkflowSerializationException exception = await Assert.ThrowsAsync<WorkflowSerializationException>(async () =>
            await _serializer.DeserializeFileAsync(missingFile));

        Assert.Equal(WorkflowSerializationOperation.ReadFile, exception.Operation);
        Assert.Contains(missingFile, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public async Task FileWriteExceptionIdentifiesWriteFileOperation()
    {
        string missingParent = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "workflow.json");

        WorkflowSerializationException exception = await Assert.ThrowsAsync<WorkflowSerializationException>(async () =>
            await _serializer.SerializeFileAsync(missingParent, WorkflowJsonTestData.CreateMinimalWorkflow()));

        Assert.Equal(WorkflowSerializationOperation.WriteFile, exception.Operation);
        Assert.Contains(missingParent, exception.Message, StringComparison.Ordinal);
    }
}
