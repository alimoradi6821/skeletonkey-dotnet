using SkeletonKey.Runner.Core;

namespace SkeletonKey.Runner.Core.Tests;

public sealed class StandaloneExportOptionsTests
{
    [Fact]
    public void ParseRequiresStandaloneSubcommand()
    {
        StandaloneExportException error = Assert.Throws<StandaloneExportException>(() => StandaloneExportOptions.Parse([]));
        Assert.Equal("SKX3020", error.Code);
    }

    [Fact]
    public void ParseAcceptsRequiredInputsAndOptionalDirectories()
    {
        StandaloneExportOptions options = StandaloneExportOptions.Parse(
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
