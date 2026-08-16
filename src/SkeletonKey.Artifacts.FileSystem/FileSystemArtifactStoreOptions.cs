namespace SkeletonKey.Artifacts.FileSystem;

/// <summary>
/// Configures a directory-backed workflow artifact store.
/// </summary>
public sealed class FileSystemArtifactStoreOptions
{
    /// <summary>
    /// Initializes filesystem artifact store options.
    /// </summary>
    public FileSystemArtifactStoreOptions(string rootDirectory, long maximumArtifactBytes = 64 * 1024 * 1024)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        if (maximumArtifactBytes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumArtifactBytes), "Maximum artifact size must be positive.");
        }

        RootDirectory = rootDirectory;
        MaximumArtifactBytes = maximumArtifactBytes;
    }

    /// <summary>Gets the host-supplied artifact root directory.</summary>
    public string RootDirectory { get; }

    /// <summary>Gets the maximum artifact size in bytes.</summary>
    public long MaximumArtifactBytes { get; }
}
