using SkeletonKey.Runner.Core;

namespace SkeletonKey.Runner.Tests;

/// <summary>
/// Covers the console project linkage to runner core.
/// </summary>
public sealed class RunnerProjectTests
{
    /// <summary>
    /// Verifies the runner executable project exposes the shared runner core dependency.
    /// </summary>
    [Fact]
    public void RunnerProjectReferencesRunnerCore()
    {
        Assert.Equal("SkeletonKeyRunner", typeof(SkeletonKeyRunner).Name);
    }
}
