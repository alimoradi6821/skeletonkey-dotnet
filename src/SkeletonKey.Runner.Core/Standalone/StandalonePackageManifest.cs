using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SkeletonKey.Runner.Core;

/// <summary>Cryptographic identity of one packaged standalone content item.</summary>
public sealed record StandaloneContentIdentity(string Path, string Sha256, long Bytes);

/// <summary>Immutable metadata embedded in a generated standalone application.</summary>
public sealed record StandalonePackageManifest(
    string Format,
    string PackageId,
    string SkeletonKeyVersion,
    string TargetRuntime,
    StandaloneContentIdentity Workflow,
    StandaloneContentIdentity Settings,
    IReadOnlyList<StandaloneContentIdentity> Dependencies)
{
    /// <summary>Current package manifest format.</summary>
    public const string CurrentFormat = "skeletonkey.standalone/0.1";

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    /// <summary>Serializes the manifest using the stable web JSON naming policy.</summary>
    public string Serialize() => JsonSerializer.Serialize(this, _jsonOptions);

    /// <summary>Deserializes an embedded standalone package manifest.</summary>
    public static StandalonePackageManifest Deserialize(string json)
    {
        StandalonePackageManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<StandalonePackageManifest>(json, _jsonOptions);
        }
        catch (JsonException exception)
        {
            throw new StandalonePackageException("SKX2001", "Standalone package manifest is not valid JSON.", exception);
        }

        if (manifest is null ||
            !string.Equals(manifest.Format, CurrentFormat, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(manifest.PackageId) ||
            string.IsNullOrWhiteSpace(manifest.SkeletonKeyVersion) ||
            string.IsNullOrWhiteSpace(manifest.TargetRuntime) ||
            manifest.Workflow is null ||
            manifest.Settings is null ||
            manifest.Dependencies is null)
        {
            throw new StandalonePackageException("SKX2001", "Standalone package manifest is missing required metadata or uses an unsupported format.");
        }

        ValidateIdentity(manifest.Workflow, "workflow");
        ValidateIdentity(manifest.Settings, "settings");
        HashSet<string> dependencyPaths = new(StringComparer.Ordinal);
        foreach (StandaloneContentIdentity dependency in manifest.Dependencies)
        {
            ValidateIdentity(dependency, "dependency");
            if (!dependencyPaths.Add(dependency.Path))
            {
                throw new StandalonePackageException("SKX2008", "Standalone package contains a duplicate dependency path: " + dependency.Path + ".");
            }
        }

        string expectedPackageId = ComputePackageId(
            manifest.SkeletonKeyVersion,
            manifest.TargetRuntime,
            manifest.Workflow,
            manifest.Settings,
            manifest.Dependencies);
        if (!string.Equals(manifest.PackageId, expectedPackageId, StringComparison.Ordinal))
        {
            throw new StandalonePackageException("SKX2006", "Standalone package identifier does not match its embedded manifest content identities.");
        }

        return manifest;
    }

    /// <summary>Computes a lowercase SHA-256 digest for the supplied bytes.</summary>
    public static string ComputeSha256(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    /// <summary>Computes the deterministic package identity from immutable content identities.</summary>
    public static string ComputePackageId(
        string skeletonKeyVersion,
        string targetRuntime,
        StandaloneContentIdentity workflow,
        StandaloneContentIdentity settings,
        IReadOnlyList<StandaloneContentIdentity> dependencies)
    {
        StringBuilder canonical = new();
        canonical.Append(CurrentFormat).Append('\n');
        canonical.Append(skeletonKeyVersion).Append('\n');
        canonical.Append(targetRuntime).Append('\n');
        AppendIdentity(canonical, workflow);
        AppendIdentity(canonical, settings);
        foreach (StandaloneContentIdentity dependency in dependencies.OrderBy(static item => item.Path, StringComparer.Ordinal))
        {
            AppendIdentity(canonical, dependency);
        }

        string digest = ComputeSha256(Encoding.UTF8.GetBytes(canonical.ToString()));
        return "standalone:sha256:" + digest;
    }


    private static void ValidateIdentity(StandaloneContentIdentity identity, string kind)
    {
        if (identity is null ||
            string.IsNullOrWhiteSpace(identity.Path) ||
            identity.Bytes < 0 ||
            string.IsNullOrWhiteSpace(identity.Sha256) ||
            identity.Sha256.Length != 64 ||
            identity.Sha256.Any(static character => !Uri.IsHexDigit(character)))
        {
            throw new StandalonePackageException("SKX2007", "Standalone package " + kind + " identity is invalid.");
        }
    }

    private static void AppendIdentity(StringBuilder builder, StandaloneContentIdentity identity)
    {
        builder.Append(identity.Path).Append('\0').Append(identity.Sha256).Append('\0').Append(identity.Bytes).Append('\n');
    }
}

/// <summary>Thrown when a sealed package is malformed, corrupted, or inconsistent.</summary>
public sealed class StandalonePackageException : Exception
{
    /// <summary>Initializes a package exception with a stable diagnostic code.</summary>
    public StandalonePackageException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>Stable package diagnostic code.</summary>
    public string Code { get; }
}
