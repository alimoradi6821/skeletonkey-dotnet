using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SkeletonKey.Artifacts;

namespace SkeletonKey.Artifacts.FileSystem;

/// <summary>
/// Stores workflow artifacts below a single canonical host-owned directory.
/// </summary>
public sealed class FileSystemWorkflowArtifactStore : IWorkflowArtifactStore
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };
    private readonly string _rootDirectory;
    private readonly long _maximumArtifactBytes;

    /// <summary>
    /// Initializes a directory-backed artifact store.
    /// </summary>
    public FileSystemWorkflowArtifactStore(FileSystemArtifactStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _rootDirectory = CanonicalizeRoot(options.RootDirectory);
        _maximumArtifactBytes = options.MaximumArtifactBytes;
        try
        {
            Directory.CreateDirectory(_rootDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new WorkflowArtifactException(WorkflowArtifactErrorCodes.ArtifactPersistenceFailed, "Artifact root could not be created.", exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<WorkflowArtifactReference> WriteAsync(WorkflowArtifactWriteRequest request, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();

        long maximum = request.MaximumBytes is null ? _maximumArtifactBytes : Math.Min(_maximumArtifactBytes, request.MaximumBytes.Value);
        string artifactId = CreateArtifactId();
        string filename = SanitizeFilename(request.Filename);
        string artifactDirectory = ArtifactDirectory(artifactId);
        string contentPath = Path.Combine(artifactDirectory, "content" + Path.GetExtension(filename));
        string temporaryPath = Path.Combine(artifactDirectory, "content.tmp");
        string metadataPath = Path.Combine(artifactDirectory, "metadata.json");
        string metadataTemporaryPath = Path.Combine(artifactDirectory, "metadata.tmp");

        try
        {
            Directory.CreateDirectory(artifactDirectory);
            await using FileStream output = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
            using var hash = SHA256.Create();
            byte[] buffer = new byte[81920];
            long total = 0;
            while (true)
            {
                int read = await content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                total += read;
                if (total > maximum)
                {
                    throw new WorkflowArtifactException(WorkflowArtifactErrorCodes.ArtifactSizeLimitExceeded, "Artifact size exceeded the configured maximum.");
                }

                hash.TransformBlock(buffer, 0, read, null, 0);
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            hash.TransformFinalBlock([], 0, 0);
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            output.Close();
            File.Move(temporaryPath, contentPath);

            string sha256 = Convert.ToHexString(hash.Hash!).ToLowerInvariant();
            WorkflowArtifactReference reference = new(artifactId, filename, request.MediaType, total, request.Sensitivity, sha256);
            var stored = StoredMetadata.From(reference, DateTimeOffset.UtcNow, Path.GetFileName(contentPath));
            await File.WriteAllTextAsync(metadataTemporaryPath, JsonSerializer.Serialize(stored, _jsonOptions), new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            File.Move(metadataTemporaryPath, metadataPath);
            return reference;
        }
        catch (WorkflowArtifactException)
        {
            CleanupPartialArtifact(temporaryPath, contentPath, metadataTemporaryPath, metadataPath, artifactDirectory);
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            CleanupPartialArtifact(temporaryPath, contentPath, metadataTemporaryPath, metadataPath, artifactDirectory);
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            CleanupPartialArtifact(temporaryPath, contentPath, metadataTemporaryPath, metadataPath, artifactDirectory);
            throw new WorkflowArtifactException(WorkflowArtifactErrorCodes.ArtifactPersistenceFailed, "Artifact persistence failed.", exception);
        }
        catch
        {
            CleanupPartialArtifact(temporaryPath, contentPath, metadataTemporaryPath, metadataPath, artifactDirectory);
            throw;
        }
    }

    /// <inheritdoc />
    public ValueTask<Stream> OpenReadAsync(WorkflowArtifactReference reference, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StoredMetadata metadata = ReadStored(reference);
        return ValueTask.FromResult<Stream>(new FileStream(ContentPath(reference.ArtifactId, metadata.ContentFileName), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous));
    }

    /// <inheritdoc />
    public ValueTask<WorkflowArtifactMetadata> GetMetadataAsync(WorkflowArtifactReference reference, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StoredMetadata metadata = ReadStored(reference);
        WorkflowArtifactReference stored = new(metadata.ArtifactId, metadata.Filename, metadata.MediaType, metadata.Size, metadata.Sensitivity, metadata.Sha256);
        return ValueTask.FromResult(new WorkflowArtifactMetadata(stored, metadata.CreatedAt));
    }

    /// <inheritdoc />
    public ValueTask DeleteAsync(WorkflowArtifactReference reference, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string directory = ArtifactDirectory(ValidateArtifactId(reference.ArtifactId));
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        return ValueTask.CompletedTask;
    }

    private static string CanonicalizeRoot(string rootDirectory)
    {
        if (File.Exists(rootDirectory))
        {
            throw new WorkflowArtifactException(WorkflowArtifactErrorCodes.ArtifactPathRejected, "Artifact root cannot be a file.");
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory));
    }

    private string ArtifactDirectory(string artifactId)
    {
        string directory = Path.GetFullPath(Path.Combine(_rootDirectory, ValidateArtifactId(artifactId)));
        if (!IsUnderRoot(directory))
        {
            throw new WorkflowArtifactException(WorkflowArtifactErrorCodes.ArtifactPathRejected, "Artifact path escaped the artifact root.");
        }

        return directory;
    }

    private string ContentPath(string artifactId, string contentFileName)
    {
        if (Path.IsPathFullyQualified(contentFileName) ||
            contentFileName.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            contentFileName.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new WorkflowArtifactException(WorkflowArtifactErrorCodes.ArtifactPathRejected, "Artifact content filename is invalid.");
        }

        string path = Path.GetFullPath(Path.Combine(ArtifactDirectory(artifactId), contentFileName));
        if (!IsUnderRoot(path))
        {
            throw new WorkflowArtifactException(WorkflowArtifactErrorCodes.ArtifactPathRejected, "Artifact content path escaped the artifact root.");
        }

        return path;
    }

    private StoredMetadata ReadStored(WorkflowArtifactReference reference)
    {
        string metadataPath = Path.Combine(ArtifactDirectory(reference.ArtifactId), "metadata.json");
        if (!File.Exists(metadataPath))
        {
            throw new WorkflowArtifactException(WorkflowArtifactErrorCodes.ArtifactUnavailable, "Artifact metadata is unavailable.");
        }

        StoredMetadata metadata = JsonSerializer.Deserialize<StoredMetadata>(File.ReadAllText(metadataPath), _jsonOptions)
            ?? throw new WorkflowArtifactException(WorkflowArtifactErrorCodes.ArtifactUnavailable, "Artifact metadata is invalid.");
        if (!string.Equals(metadata.ArtifactId, reference.ArtifactId, StringComparison.Ordinal))
        {
            throw new WorkflowArtifactException(WorkflowArtifactErrorCodes.ArtifactUnavailable, "Artifact metadata does not match the requested artifact.");
        }

        return metadata;
    }

    private static string CreateArtifactId()
    {
        return "artifact-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
    }

    private static string ValidateArtifactId(string artifactId)
    {
        if (string.IsNullOrWhiteSpace(artifactId) || artifactId.Any(static c => !(char.IsAsciiLetterOrDigit(c) || c == '-')) || artifactId.Contains("..", StringComparison.Ordinal))
        {
            throw new WorkflowArtifactException(WorkflowArtifactErrorCodes.ArtifactPathRejected, "Artifact identifier is invalid.");
        }

        return artifactId;
    }

    private static string SanitizeFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename) ||
            Path.IsPathFullyQualified(filename) ||
            filename.Contains("..", StringComparison.Ordinal) ||
            filename.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            filename.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new WorkflowArtifactException(WorkflowArtifactErrorCodes.ArtifactPathRejected, "Artifact filename is invalid.");
        }

        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            if (filename.Contains(invalid, StringComparison.Ordinal))
            {
                throw new WorkflowArtifactException(WorkflowArtifactErrorCodes.ArtifactPathRejected, "Artifact filename is invalid.");
            }
        }

        string name = Path.GetFileName(filename);
        if (string.IsNullOrWhiteSpace(name) || name.EndsWith(' ') || name.EndsWith('.'))
        {
            throw new WorkflowArtifactException(WorkflowArtifactErrorCodes.ArtifactPathRejected, "Artifact filename is invalid.");
        }

        string stem = Path.GetFileNameWithoutExtension(name);
        if (IsWindowsDeviceName(stem))
        {
            throw new WorkflowArtifactException(WorkflowArtifactErrorCodes.ArtifactPathRejected, "Artifact filename is invalid.");
        }

        return name.Length > 128 ? name[..128] : name;
    }

    private bool IsUnderRoot(string path)
    {
        string relative = Path.GetRelativePath(_rootDirectory, path);
        return relative.Length == 0 ||
            (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathFullyQualified(relative));
    }

    private static bool IsWindowsDeviceName(string value)
    {
        string upper = value.ToUpperInvariant();
        return upper is "CON" or "PRN" or "AUX" or "NUL" or "COM1" or "COM2" or "COM3" or "COM4" or "COM5" or "COM6" or "COM7" or "COM8" or "COM9" or "LPT1" or "LPT2" or "LPT3" or "LPT4" or "LPT5" or "LPT6" or "LPT7" or "LPT8" or "LPT9";
    }

    private static void CleanupPartialArtifact(
        string temporaryPath,
        string contentPath,
        string metadataTemporaryPath,
        string metadataPath,
        string artifactDirectory)
    {
        TryDeleteFile(temporaryPath);
        TryDeleteFile(contentPath);
        TryDeleteFile(metadataTemporaryPath);
        TryDeleteFile(metadataPath);
        TryDeleteDirectory(artifactDirectory);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed record StoredMetadata(
        string ArtifactId,
        string Filename,
        string MediaType,
        long Size,
        WorkflowArtifactSensitivity Sensitivity,
        string? Sha256,
        DateTimeOffset CreatedAt,
        string ContentFileName)
    {
        public static StoredMetadata From(WorkflowArtifactReference reference, DateTimeOffset createdAt, string contentFileName)
        {
            return new(reference.ArtifactId, reference.Filename, reference.MediaType, reference.Size, reference.Sensitivity, reference.Sha256, createdAt, contentFileName);
        }
    }
}
