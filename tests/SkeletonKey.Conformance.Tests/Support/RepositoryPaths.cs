namespace SkeletonKey.Conformance.Tests.Support;

internal static class RepositoryPaths
{
    public static string Root { get; } = FindRepositoryRoot();

    public static string SchemaPath => Path.Combine(Root, "schemas", "workflow", "0.1", "schema.json");

    public static string ConformanceRoot => Path.Combine(Root, "tests", "fixtures", "conformance");

    public static string ManifestPath => Path.Combine(ConformanceRoot, "manifest.json");

    public static string ExamplePath => Path.Combine(Root, "examples", "minimal.workflow.json");

    public static string ResolveFixture(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(ConformanceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SkeletonKey.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate SkeletonKey.sln from the test output directory.");
    }
}
