using System.Text;
using SkeletonKey.Artifacts;
using SkeletonKey.Artifacts.FileSystem;

namespace SkeletonKey.Artifacts.FileSystem.Tests;

/// <summary>
/// Covers directory-backed workflow artifact persistence.
/// </summary>
public sealed class FileSystemWorkflowArtifactStoreTests
{
    /// <summary>
    /// Verifies content can be written, read, described, and deleted below the configured root.
    /// </summary>
    [Fact]
    public async Task StoreRoundTripsArtifactContent()
    {
        string root = Path.Combine(Path.GetTempPath(), "skeletonkey-artifacts-tests", Guid.NewGuid().ToString("N"));
        FileSystemWorkflowArtifactStore store = new(new FileSystemArtifactStoreOptions(root, maximumArtifactBytes: 1024));
        await using MemoryStream content = new(Encoding.UTF8.GetBytes("hello"));

        WorkflowArtifactReference reference = await store.WriteAsync(new WorkflowArtifactWriteRequest("b.txt", "text/plain", WorkflowArtifactSensitivity.Internal, 1024), content);
        await using Stream read = await store.OpenReadAsync(reference);
        using StreamReader reader = new(read, Encoding.UTF8);
        WorkflowArtifactMetadata metadata = await store.GetMetadataAsync(reference);

        Assert.Equal("hello", await reader.ReadToEndAsync());
        Assert.Equal(reference.ArtifactId, metadata.Reference.ArtifactId);
        Assert.DoesNotContain(Path.DirectorySeparatorChar, reference.Filename);

        reader.Dispose();
        await store.DeleteAsync(reference);
        await Assert.ThrowsAsync<WorkflowArtifactException>(async () => await store.OpenReadAsync(reference));
    }

    /// <summary>
    /// Verifies duplicate logical filenames produce independent collision-resistant artifact IDs.
    /// </summary>
    [Fact]
    public async Task DuplicateLogicalFilenamesProduceDistinctArtifacts()
    {
        string root = NewRoot();
        FileSystemWorkflowArtifactStore store = new(new FileSystemArtifactStoreOptions(root, maximumArtifactBytes: 1024));
        await using MemoryStream first = new(Encoding.UTF8.GetBytes("one"));
        await using MemoryStream second = new(Encoding.UTF8.GetBytes("two"));

        WorkflowArtifactReference firstReference = await store.WriteAsync(new WorkflowArtifactWriteRequest("same.txt", "text/plain", WorkflowArtifactSensitivity.Internal, 1024), first);
        WorkflowArtifactReference secondReference = await store.WriteAsync(new WorkflowArtifactWriteRequest("same.txt", "text/plain", WorkflowArtifactSensitivity.Internal, 1024), second);

        Assert.NotEqual(firstReference.ArtifactId, secondReference.ArtifactId);
        Assert.Equal("same.txt", firstReference.Filename);
        Assert.Equal("same.txt", secondReference.Filename);
        Assert.Equal(64, firstReference.Sha256!.Length);
        Assert.Equal(64, secondReference.Sha256!.Length);
    }

    /// <summary>
    /// Verifies unsafe workflow-supplied filenames are rejected instead of normalized into host paths.
    /// </summary>
    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("..\\evil.txt")]
    [InlineData("folder/file.txt")]
    [InlineData("folder\\file.txt")]
    [InlineData("C:\\evil.txt")]
    [InlineData("\\\\server\\share\\evil.txt")]
    [InlineData("COM1.txt")]
    [InlineData("bad.")]
    [InlineData("bad ")]
    [InlineData("bad:name.txt")]
    public async Task StoreRejectsUnsafeFilenames(string filename)
    {
        string root = NewRoot();
        FileSystemWorkflowArtifactStore store = new(new FileSystemArtifactStoreOptions(root, maximumArtifactBytes: 1024));
        await using MemoryStream content = new(Encoding.UTF8.GetBytes("hello"));

        WorkflowArtifactException exception = await Assert.ThrowsAsync<WorkflowArtifactException>(async () =>
            await store.WriteAsync(new WorkflowArtifactWriteRequest(filename, "text/plain", WorkflowArtifactSensitivity.Internal, 1024), content));

        Assert.Equal(WorkflowArtifactErrorCodes.ArtifactPathRejected, exception.Code);
    }

    /// <summary>
    /// Verifies size overflow rejects the write and removes partial store-owned files.
    /// </summary>
    [Fact]
    public async Task SizeOverflowCleansPartialArtifact()
    {
        string root = NewRoot();
        FileSystemWorkflowArtifactStore store = new(new FileSystemArtifactStoreOptions(root, maximumArtifactBytes: 4));
        await using MemoryStream content = new(Encoding.UTF8.GetBytes("hello"));

        WorkflowArtifactException exception = await Assert.ThrowsAsync<WorkflowArtifactException>(async () =>
            await store.WriteAsync(new WorkflowArtifactWriteRequest("too-large.txt", "text/plain", WorkflowArtifactSensitivity.Internal, 4), content));

        Assert.Equal(WorkflowArtifactErrorCodes.ArtifactSizeLimitExceeded, exception.Code);
        Assert.Empty(Directory.EnumerateDirectories(root));
    }

    /// <summary>
    /// Verifies metadata is immutable with respect to the requested artifact ID.
    /// </summary>
    [Fact]
    public async Task TamperedMetadataIsRejected()
    {
        string root = NewRoot();
        FileSystemWorkflowArtifactStore store = new(new FileSystemArtifactStoreOptions(root, maximumArtifactBytes: 1024));
        await using MemoryStream content = new(Encoding.UTF8.GetBytes("hello"));
        WorkflowArtifactReference reference = await store.WriteAsync(new WorkflowArtifactWriteRequest("safe.txt", "text/plain", WorkflowArtifactSensitivity.Internal, 1024), content);
        string metadataPath = Path.Combine(root, reference.ArtifactId, "metadata.json");
        string metadata = await File.ReadAllTextAsync(metadataPath);
        await File.WriteAllTextAsync(metadataPath, metadata.Replace(reference.ArtifactId, "artifact-tampered", StringComparison.Ordinal));

        WorkflowArtifactException exception = await Assert.ThrowsAsync<WorkflowArtifactException>(async () => await store.GetMetadataAsync(reference));

        Assert.Equal(WorkflowArtifactErrorCodes.ArtifactUnavailable, exception.Code);
    }

    private static string NewRoot()
    {
        return Path.Combine(Path.GetTempPath(), "skeletonkey-artifacts-tests", Guid.NewGuid().ToString("N"));
    }
}
