using System.Text.Json.Nodes;
using SkeletonKey.Runner.Core;

namespace SkeletonKey.Runner.Core.Tests;

/// <summary>
/// Covers runner command envelopes without launching a separate process.
/// </summary>
public sealed class SkeletonKeyRunnerTests
{
    /// <summary>
    /// Verifies version returns a successful net10.0 envelope.
    /// </summary>
    [Fact]
    public async Task VersionReturnsSuccessfulEnvelope()
    {
        StringWriter output = new();
        int exitCode = await new SkeletonKeyRunner(TextReader.Null, output, TextWriter.Null).ExecuteAsync(["version"]);

        JsonNode envelope = JsonNode.Parse(output.ToString())!;
        Assert.Equal(RunnerExitCodes.Success, exitCode);
        Assert.True(envelope["accepted"]!.GetValue<bool>());
        Assert.Equal("net10.0", envelope["result"]!["targetFramework"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies validate reads workflow JSON from stdin.
    /// </summary>
    [Fact]
    public async Task ValidateReadsWorkflowFromStandardInput()
    {
        string workflow = File.ReadAllText(Path.Combine(RepositoryRoot(), "tests", "fixtures", "conformance", "valid", "core-return.workflow.json"));
        StringWriter output = new();
        int exitCode = await new SkeletonKeyRunner(new StringReader(workflow), output, TextWriter.Null).ExecuteAsync(["validate", "--file", "-"]);

        JsonNode envelope = JsonNode.Parse(output.ToString())!;
        Assert.Equal(RunnerExitCodes.Success, exitCode);
        Assert.True(envelope["accepted"]!.GetValue<bool>());
        Assert.Equal("valid", envelope["status"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies plan reports catalog analysis failures as a structured blocked envelope.
    /// </summary>
    [Fact]
    public async Task PlanReturnsBlockedEnvelopeWhenAnalysisFails()
    {
        string workflow = File.ReadAllText(Path.Combine(RepositoryRoot(), "examples", "minimal.workflow.json"));
        StringWriter output = new();
        int exitCode = await new SkeletonKeyRunner(new StringReader(workflow), output, TextWriter.Null).ExecuteAsync(["plan", "--file", "-"]);

        JsonNode envelope = JsonNode.Parse(output.ToString())!;
        Assert.Equal(RunnerExitCodes.Failed, exitCode);
        Assert.False(envelope["accepted"]!.GetValue<bool>());
        Assert.Equal("blocked", envelope["status"]!.GetValue<string>());
        Assert.Equal("SKA1001", envelope["issues"]![0]!["code"]!.GetValue<string>());
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
