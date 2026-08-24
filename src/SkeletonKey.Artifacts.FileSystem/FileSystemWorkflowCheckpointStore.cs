using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using SkeletonKey.Runtime;

namespace SkeletonKey.Artifacts.FileSystem;

/// <summary>
/// Stores integrity-protected workflow checkpoints beneath one host-owned filesystem root.
/// </summary>
/// <remarks>
/// Execution identifiers are hashed before use as filenames. Writes use an exclusive lock, optimistic revision check,
/// durable temporary file, and atomic replacement. Workflow data cannot select a path outside the configured root.
/// </remarks>
public sealed class FileSystemWorkflowCheckpointStore : IWorkflowCheckpointStore
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _rootPath;

    /// <summary>Initializes a checkpoint store rooted at a host-owned directory.</summary>
    public FileSystemWorkflowCheckpointStore(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        string canonicalRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        try
        {
            if (File.Exists(canonicalRoot))
            {
                throw new IOException("Checkpoint root cannot be a file.");
            }

            Directory.CreateDirectory(canonicalRoot);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new WorkflowCheckpointStoreException(WorkflowCheckpointErrorCodes.StoreFailure, "The checkpoint root is unavailable.", exception);
        }

        _rootPath = canonicalRoot;
    }

    /// <inheritdoc />
    public async ValueTask<WorkflowExecutionCheckpoint?> LoadAsync(string executionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        cancellationToken.ThrowIfCancellationRequested();
        string checkpointPath = GetCheckpointPath(executionId);
        try
        {
            await using FileStream lockStream = await AcquireLockAsync(checkpointPath + ".lock", cancellationToken).ConfigureAwait(false);
            if (!File.Exists(checkpointPath))
            {
                return null;
            }

            WorkflowExecutionCheckpoint checkpoint = await ReadCheckpointAsync(checkpointPath, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(checkpoint.ExecutionId, executionId, StringComparison.Ordinal))
            {
                throw new WorkflowCheckpointStoreException(WorkflowCheckpointErrorCodes.IdentityMismatch, "Checkpoint execution identity does not match its storage key.");
            }

            return checkpoint;
        }
        catch (WorkflowCheckpointStoreException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new WorkflowCheckpointStoreException(WorkflowCheckpointErrorCodes.StoreFailure, "The workflow checkpoint could not be loaded.", exception);
        }
        catch (Exception exception)
        {
            throw new WorkflowCheckpointStoreException(WorkflowCheckpointErrorCodes.InvalidCheckpoint, "The workflow checkpoint payload is invalid.", exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync(WorkflowExecutionCheckpoint checkpoint, long expectedRevision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (!WorkflowExecutionCheckpoint.IsSupportedFormatVersion(checkpoint.FormatVersion))
        {
            throw new WorkflowCheckpointStoreException(WorkflowCheckpointErrorCodes.UnsupportedFormatVersion, "The checkpoint format version is not supported.");
        }

        if (expectedRevision < 0 || checkpoint.Revision != expectedRevision + 1)
        {
            throw new WorkflowCheckpointStoreException(WorkflowCheckpointErrorCodes.RevisionConflict, "Checkpoint revision must be exactly one greater than the expected revision.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        string checkpointPath = GetCheckpointPath(checkpoint.ExecutionId);
        string lockPath = checkpointPath + ".lock";
        await using FileStream lockStream = await AcquireLockAsync(lockPath, cancellationToken).ConfigureAwait(false);
        try
        {
            WorkflowExecutionCheckpoint? current = File.Exists(checkpointPath)
                ? await ReadCheckpointAsync(checkpointPath, cancellationToken).ConfigureAwait(false)
                : null;
            long currentRevision = current?.Revision ?? 0;
            if (currentRevision != expectedRevision)
            {
                throw new WorkflowCheckpointStoreException(WorkflowCheckpointErrorCodes.RevisionConflict, "The persisted checkpoint revision changed before this save.");
            }

            if (current is not null && !string.Equals(current.ExecutionId, checkpoint.ExecutionId, StringComparison.Ordinal))
            {
                throw new WorkflowCheckpointStoreException(WorkflowCheckpointErrorCodes.IdentityMismatch, "Checkpoint execution identity does not match its storage key.");
            }

            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(checkpoint, _jsonOptions);
            string checksum = Convert.ToHexStringLower(SHA256.HashData(payload));
            byte[] envelope = JsonSerializer.SerializeToUtf8Bytes(new CheckpointEnvelope(checkpoint.FormatVersion, checksum, Convert.ToBase64String(payload)), _jsonOptions);
            string temporaryPath = checkpointPath + "." + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".tmp";
            await WriteTemporaryAsync(temporaryPath, envelope, cancellationToken).ConfigureAwait(false);
            if (File.Exists(checkpointPath))
            {
                File.Replace(temporaryPath, checkpointPath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temporaryPath, checkpointPath);
            }
        }
        catch (WorkflowCheckpointStoreException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new WorkflowCheckpointStoreException(WorkflowCheckpointErrorCodes.StoreFailure, "The workflow checkpoint could not be saved atomically.", exception);
        }
    }

    private async ValueTask<WorkflowExecutionCheckpoint> ReadCheckpointAsync(string checkpointPath, CancellationToken cancellationToken)
    {
        byte[] envelopeBytes = await File.ReadAllBytesAsync(checkpointPath, cancellationToken).ConfigureAwait(false);
        CheckpointEnvelope? envelope = JsonSerializer.Deserialize<CheckpointEnvelope>(envelopeBytes, _jsonOptions);
        if (envelope is null || !WorkflowExecutionCheckpoint.IsSupportedFormatVersion(envelope.FormatVersion))
        {
            throw new WorkflowCheckpointStoreException(WorkflowCheckpointErrorCodes.UnsupportedFormatVersion, "The checkpoint envelope format version is not supported.");
        }

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(envelope.Payload);
        }
        catch (FormatException exception)
        {
            throw new WorkflowCheckpointStoreException(WorkflowCheckpointErrorCodes.InvalidCheckpoint, "The checkpoint payload encoding is invalid.", exception);
        }

        byte[] expectedChecksum;
        try
        {
            expectedChecksum = Convert.FromHexString(envelope.Sha256);
        }
        catch (FormatException exception)
        {
            throw new WorkflowCheckpointStoreException(WorkflowCheckpointErrorCodes.InvalidCheckpoint, "The checkpoint checksum encoding is invalid.", exception);
        }

        byte[] actualChecksum = SHA256.HashData(payload);
        if (expectedChecksum.Length != actualChecksum.Length || !CryptographicOperations.FixedTimeEquals(expectedChecksum, actualChecksum))
        {
            throw new WorkflowCheckpointStoreException(WorkflowCheckpointErrorCodes.InvalidCheckpoint, "The checkpoint payload checksum does not match.");
        }

        WorkflowExecutionCheckpoint? checkpoint = JsonSerializer.Deserialize<WorkflowExecutionCheckpoint>(payload, _jsonOptions);
        if (checkpoint is null)
        {
            throw new WorkflowCheckpointStoreException(WorkflowCheckpointErrorCodes.InvalidCheckpoint, "The checkpoint payload is empty.");
        }

        if (!string.Equals(checkpoint.FormatVersion, envelope.FormatVersion, StringComparison.Ordinal))
        {
            throw new WorkflowCheckpointStoreException(WorkflowCheckpointErrorCodes.InvalidCheckpoint, "The checkpoint envelope and payload format versions do not match.");
        }

        return checkpoint;
    }

    private static async ValueTask<FileStream> AcquireLockAsync(string lockPath, CancellationToken cancellationToken)
    {
        const int maximumAttempts = 250;
        for (int attempt = 0; attempt < maximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.Asynchronous | FileOptions.WriteThrough);
            }
            catch (IOException) when (attempt + 1 < maximumAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken).ConfigureAwait(false);
            }
            catch (IOException exception)
            {
                throw new WorkflowCheckpointStoreException(WorkflowCheckpointErrorCodes.StoreFailure, "The checkpoint lock could not be acquired.", exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new WorkflowCheckpointStoreException(WorkflowCheckpointErrorCodes.StoreFailure, "The checkpoint lock could not be acquired.", exception);
            }
        }

        throw new WorkflowCheckpointStoreException(WorkflowCheckpointErrorCodes.StoreFailure, "The checkpoint lock could not be acquired.");
    }

    private static async ValueTask WriteTemporaryAsync(string temporaryPath, byte[] bytes, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private string GetCheckpointPath(string executionId)
    {
        byte[] identity = System.Text.Encoding.UTF8.GetBytes(executionId);
        string fileName = "checkpoint-" + Convert.ToHexStringLower(SHA256.HashData(identity)) + ".json";
        string path = Path.GetFullPath(Path.Combine(_rootPath, fileName));
        string relative = Path.GetRelativePath(_rootPath, path);
        if (Path.IsPathFullyQualified(relative) ||
            string.Equals(relative, "..", StringComparison.Ordinal) ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new WorkflowCheckpointStoreException(WorkflowCheckpointErrorCodes.StoreFailure, "The checkpoint path escaped the configured root.");
        }

        return path;
    }

    private sealed record CheckpointEnvelope(string FormatVersion, string Sha256, string Payload);
}
