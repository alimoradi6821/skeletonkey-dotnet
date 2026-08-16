namespace SkeletonKey.Web.Advanced.Integration.Tests;

/// <summary>
/// Covers package isolation rules for Playwright.
/// </summary>
public sealed class PlaywrightPackageBoundaryTests
{
    /// <summary>
    /// Verifies Microsoft.Playwright is only directly referenced by approved projects.
    /// </summary>
    [Fact]
    public void MicrosoftPlaywrightDirectReferencesStayInApprovedProjects()
    {
        string root = RepositoryRoot();
        HashSet<string> allowed = new(StringComparer.OrdinalIgnoreCase)
        {
            "SkeletonKey.Web.Playwright.csproj",
            "SkeletonKey.Playwright.BrowserInstaller.csproj",
            "SkeletonKey.Web.Playwright.Tests.csproj",
            "SkeletonKey.Web.Integration.Tests.csproj",
            "SkeletonKey.Web.Advanced.Tests.csproj",
            "SkeletonKey.Web.Advanced.Integration.Tests.csproj",
        };

        string[] offenders = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains("Microsoft.Playwright", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .Where(name => name is not null && !allowed.Contains(name))
            .Cast<string>()
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo current = new(AppContext.BaseDirectory);
        while (current.Parent is not null && !File.Exists(Path.Combine(current.FullName, "SkeletonKey.sln")))
        {
            current = current.Parent;
        }

        return current.FullName;
    }
}
