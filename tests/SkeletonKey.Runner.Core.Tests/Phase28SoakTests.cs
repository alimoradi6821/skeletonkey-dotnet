using System.Diagnostics;
using SkeletonKey.Runner.Core;

namespace SkeletonKey.Runner.Core.Tests;

/// <summary>Prevents the Phase 28 soak probe from racing other tests in this assembly.</summary>
[CollectionDefinition("Phase28Soak", DisableParallelization = true)]
public sealed class Phase28SoakCollectionDefinition
{
}

/// <summary>
/// Exercises many Runner executions inside one long-lived test process so managed allocations
/// and operating-system handles can be checked after warm-up and full collection.
/// </summary>
[Collection("Phase28Soak")]
public sealed class Phase28SoakTests
{
    private const int _warmupIterations = 20;
    private const int _measuredIterations = 250;
    private const long _maximumManagedGrowthBytes = 64L * 1024L * 1024L;
    private const int _maximumHandleGrowth = 64;

    /// <summary>
    /// Verifies repeated minimal executions do not accumulate unbounded managed memory or handles.
    /// </summary>
    [Fact]
    [Trait("Category", "Phase28Soak")]
    public async Task RepeatedMinimalRunnerExecutionsRemainResourceBounded()
    {
        string workflow = Path.Combine(
            RepositoryRoot(),
            "tests",
            "fixtures",
            "validation",
            "valid-minimal.workflow.json");

        for (int iteration = 0; iteration < _warmupIterations; iteration++)
        {
            await ExecuteOnceAsync(workflow, "phase-028-warmup-" + iteration);
        }

        ForceCollection();
        long managedBefore = GC.GetTotalMemory(forceFullCollection: false);
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        int handlesBefore = process.HandleCount;

        for (int iteration = 0; iteration < _measuredIterations; iteration++)
        {
            await ExecuteOnceAsync(workflow, "phase-028-measured-" + iteration);
        }

        ForceCollection();
        long managedAfter = GC.GetTotalMemory(forceFullCollection: false);
        process.Refresh();
        int handlesAfter = process.HandleCount;

        long managedGrowth = Math.Max(0, managedAfter - managedBefore);
        int handleGrowth = Math.Max(0, handlesAfter - handlesBefore);

        Assert.True(
            managedGrowth <= _maximumManagedGrowthBytes,
            $"Managed-memory growth after {_measuredIterations} in-process executions was {managedGrowth:N0} bytes; limit is {_maximumManagedGrowthBytes:N0} bytes.");
        Assert.True(
            handleGrowth <= _maximumHandleGrowth,
            $"Handle growth after {_measuredIterations} in-process executions was {handleGrowth}; limit is {_maximumHandleGrowth}.");
    }

    private static async Task ExecuteOnceAsync(string workflow, string executionId)
    {
        int exitCode = await new SkeletonKeyRunner(
            TextReader.Null,
            TextWriter.Null,
            TextWriter.Null).ExecuteAsync([
                "run",
                "--file",
                workflow,
                "--execution-id",
                executionId,
            ]);

        Assert.Equal(RunnerExitCodes.Success, exitCode);
    }

    private static void ForceCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GC.WaitForPendingFinalizers();
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
