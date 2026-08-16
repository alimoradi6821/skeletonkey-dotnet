using System.Text;
using SkeletonKey.Serialization.Json.Tests.Support;
using SkeletonKey.Workflow.Documents;

namespace SkeletonKey.Serialization.Json.Tests.Files;

/// <summary>
/// Covers workflow JSON serialization behavior.
/// </summary>
public sealed class FileOperationTests
{
    private readonly WorkflowJsonSerializer _serializer = new();

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public async Task ReadsWorkflowFromUtf8File()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string path = temporaryDirectory.GetPath("workflow.json");
        await File.WriteAllTextAsync(path, WorkflowJsonTestData.MinimalJson, new UTF8Encoding(false));

        WorkflowDocument workflow = await _serializer.DeserializeFileAsync(path);

        Assert.Equal("minimal", workflow.Id);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public async Task WritesWorkflowWithoutUtf8Bom()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string path = temporaryDirectory.GetPath("workflow.json");

        await _serializer.SerializeFileAsync(path, WorkflowJsonTestData.CreateMinimalWorkflow());

        byte[] bytes = await File.ReadAllBytesAsync(path);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public async Task WritesExactlyOneFinalNewline()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string path = temporaryDirectory.GetPath("workflow.json");

        await _serializer.SerializeFileAsync(path, WorkflowJsonTestData.CreateMinimalWorkflow());

        string text = await File.ReadAllTextAsync(path);
        Assert.EndsWith("\n", text, StringComparison.Ordinal);
        Assert.False(text.EndsWith("\n\n", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public async Task WritesCanonicalJson()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string path = temporaryDirectory.GetPath("workflow.json");
        WorkflowDocument workflow = WorkflowJsonTestData.CreateRepositoryExampleWorkflow();

        await _serializer.SerializeFileAsync(path, workflow);

        string text = await File.ReadAllTextAsync(path);
        Assert.Equal(_serializer.Serialize(workflow), text);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public async Task ReplacesExistingFile()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string path = temporaryDirectory.GetPath("workflow.json");
        await File.WriteAllTextAsync(path, "old");

        await _serializer.SerializeFileAsync(path, WorkflowJsonTestData.CreateMinimalWorkflow());

        Assert.Contains("\"id\": \"minimal\"", await File.ReadAllTextAsync(path), StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public async Task DoesNotLeavePartialTargetFileOnSerializationFailure()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string path = temporaryDirectory.GetPath("workflow.json");
        await File.WriteAllTextAsync(path, "stable");

        await Assert.ThrowsAsync<WorkflowSerializationException>(async () =>
            await _serializer.SerializeFileAsync(path, null!));

        Assert.Equal("stable", await File.ReadAllTextAsync(path));
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public async Task HonorsCancellationDuringFileRead()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string path = temporaryDirectory.GetPath("workflow.json");
        await File.WriteAllTextAsync(path, WorkflowJsonTestData.MinimalJson);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        WorkflowSerializationException exception = await Assert.ThrowsAsync<WorkflowSerializationException>(async () =>
            await _serializer.DeserializeFileAsync(path, cancellation.Token));

        Assert.Equal(WorkflowSerializationOperation.ReadFile, exception.Operation);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RejectsEmptyFilePath(string path)
    {
        WorkflowSerializationException readException = await Assert.ThrowsAsync<WorkflowSerializationException>(async () =>
            await _serializer.DeserializeFileAsync(path));
        WorkflowSerializationException writeException = await Assert.ThrowsAsync<WorkflowSerializationException>(async () =>
            await _serializer.SerializeFileAsync(path, WorkflowJsonTestData.CreateMinimalWorkflow()));

        Assert.Equal(WorkflowSerializationOperation.ReadFile, readException.Operation);
        Assert.Equal(WorkflowSerializationOperation.WriteFile, writeException.Operation);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public async Task RejectsMissingParentDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "workflow.json");

        WorkflowSerializationException exception = await Assert.ThrowsAsync<WorkflowSerializationException>(async () =>
            await _serializer.SerializeFileAsync(path, WorkflowJsonTestData.CreateMinimalWorkflow()));

        Assert.Equal(WorkflowSerializationOperation.WriteFile, exception.Operation);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            PathValue = Path.Combine(Path.GetTempPath(), "SkeletonKeyTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(PathValue);
        }

        public string PathValue { get; }

        public string GetPath(string fileName)
        {
            return Path.Combine(PathValue, fileName);
        }

        public void Dispose()
        {
            if (Directory.Exists(PathValue))
            {
                Directory.Delete(PathValue, recursive: true);
            }
        }
    }
}


