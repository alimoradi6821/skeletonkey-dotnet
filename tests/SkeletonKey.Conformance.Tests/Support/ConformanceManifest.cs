using System.Text.Json;
using System.Text.Json.Serialization;

namespace SkeletonKey.Conformance.Tests.Support;

internal sealed class ConformanceManifest
{
    public string FormatVersion { get; init; } = string.Empty;

    public IReadOnlyList<ConformanceCase> Cases { get; init; } = [];

    public static ConformanceManifest Load()
    {
        string json = File.ReadAllText(RepositoryPaths.ManifestPath);
        ConformanceManifest? manifest = JsonSerializer.Deserialize<ConformanceManifest>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
        });

        return manifest ?? throw new InvalidOperationException("Conformance manifest could not be read.");
    }
}

internal sealed class ConformanceCase
{
    public string Id { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string File { get; init; } = string.Empty;

    public string Serialization { get; init; } = string.Empty;

    public string Schema { get; init; } = string.Empty;

    public SemanticExpectation? Semantic { get; init; }

    [JsonIgnore]
    public string FullPath => RepositoryPaths.ResolveFixture(File);

    public string ReadJson()
    {
        return FileSystem.ReadAllText(FullPath);
    }
}

internal sealed class SemanticExpectation
{
    public bool IsValid { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];
}

internal static class FileSystem
{
    public static string ReadAllText(string path)
    {
        return System.IO.File.ReadAllText(path);
    }
}
