using System.Diagnostics;
using System.Text.Json.Nodes;

namespace SkeletonKey.Runner.Integration.Tests;

/// <summary>
/// Covers the published command path at process boundary.
/// </summary>
public sealed class RunnerCommandSmokeTests
{
    /// <summary>
    /// Verifies the runner project can be launched as a process for the version command.
    /// </summary>
    [Fact]
    public async Task DotnetRunVersionProducesEnvelope()
    {
        string root = RepositoryRoot();
        ProcessStartInfo start = new()
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = root,
        };
        start.ArgumentList.Add(Path.Combine(root, "src", "SkeletonKey.Runner", "bin", "Release", "net10.0-windows", "skeletonkey.dll"));
        start.ArgumentList.Add("version");

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start runner.");
        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(process.ExitCode == 0, stderr + stdout);
        Assert.True(string.IsNullOrWhiteSpace(stderr), stderr);
        JsonNode envelope = JsonNode.Parse(stdout)!;
        Assert.True(envelope["accepted"]!.GetValue<bool>());
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
