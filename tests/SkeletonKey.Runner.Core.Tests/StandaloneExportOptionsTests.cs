using SkeletonKey.Runner.Core;

namespace SkeletonKey.Runner.Core.Tests;

/// <summary>Tests parsing of standalone export command-line options.</summary>
public sealed class StandaloneExportOptionsTests
{
    /// <summary>Verifies that the standalone subcommand is mandatory.</summary>
    [Fact]
    public void ParseRequiresStandaloneSubcommand()
    {
        StandaloneExportException error = Assert.Throws<StandaloneExportException>(() => StandaloneExportOptions.Parse([]));
        Assert.Equal("SKX3020", error.Code);
    }

    /// <summary>Verifies required inputs and optional directories are parsed correctly.</summary>
    [Fact]
    public void ParseAcceptsRequiredInputsAndOptionalDirectories()
    {
        var options = StandaloneExportOptions.Parse(
        [
            "standalone",
            "--workflow", "scenario.workflow.json",
            "--settings", "execution.settings.json",
            "--output", "Scenario.exe",
            "--locator-directory", "locators",
            "--workflow-directory", "workflows",
            "--runtime", "win-x64",
        ]);

        Assert.Equal("scenario.workflow.json", options.WorkflowPath);
        Assert.Equal("execution.settings.json", options.SettingsPath);
        Assert.Equal("Scenario.exe", options.OutputPath);
        Assert.Equal("locators", options.LocatorDirectory);
        Assert.Equal("workflows", options.WorkflowDirectory);
        Assert.Equal("win-x64", options.TargetRuntime);
    }

    /// <summary>Verifies that unknown export options are rejected.</summary>
    [Fact]
    public void ParseRejectsUnknownOptions()
    {
        StandaloneExportException error = Assert.Throws<StandaloneExportException>(() => StandaloneExportOptions.Parse(
        [
            "standalone",
            "--workflow", "scenario.workflow.json",
            "--settings", "execution.settings.json",
            "--output", "Scenario.exe",
            "--plugin-directory", "plugins",
        ]));

        Assert.Equal("SKX3022", error.Code);
    }
}
