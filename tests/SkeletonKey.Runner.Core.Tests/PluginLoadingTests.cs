using System.Security.Cryptography;
using System.Text.Json;
using SkeletonKey.Runner.Core.Plugins;

namespace SkeletonKey.Runner.Core.Tests;

/// <summary>Verifies explicit local plugin loading and Runner composition.</summary>
public sealed class PluginLoadingTests
{
    /// <summary>Verifies a hash-verified manifest loads its exact declared entry type.</summary>
    [Fact]
    public async Task LoadAsync_loads_hash_verified_exact_entry_type()
    {
        string manifestPath = await WriteManifestAsync(typeof(Phase22FixturePlugin));
        try
        {
            SkeletonKeyPluginLoadResult result = await SkeletonKeyPluginLoader.LoadAsync([Path.GetDirectoryName(manifestPath)!]);

            SkeletonKeyPluginDescriptor descriptor = Assert.Single(result.Plugins);
            Assert.Equal("phase22.fixture", descriptor.Id);
            Assert.Single(result.NodeDefinitions);
            Assert.Single(result.NodeHandlers);
            Assert.Single(result.ResourceProviders);
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    /// <summary>Verifies an assembly hash mismatch returns the stable integrity code.</summary>
    [Fact]
    public async Task LoadAsync_rejects_hash_mismatch_with_stable_code()
    {
        string manifestPath = await WriteManifestAsync(typeof(Phase22FixturePlugin), sha256: new string('0', 64));
        try
        {
            SkeletonKeyPluginLoadException exception = await Assert.ThrowsAsync<SkeletonKeyPluginLoadException>(async () =>
                await SkeletonKeyPluginLoader.LoadAsync([Path.GetDirectoryName(manifestPath)!]));

            Assert.Equal("SKP2205", exception.Code);
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    /// <summary>Verifies the closed manifest rejects unknown properties.</summary>
    [Fact]
    public async Task LoadAsync_rejects_unknown_manifest_properties()
    {
        string manifestPath = await WriteManifestAsync(typeof(Phase22FixturePlugin), includeUnknownProperty: true);
        try
        {
            SkeletonKeyPluginLoadException exception = await Assert.ThrowsAsync<SkeletonKeyPluginLoadException>(async () =>
                await SkeletonKeyPluginLoader.LoadAsync([Path.GetDirectoryName(manifestPath)!]));

            Assert.Equal("SKP2202", exception.Code);
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    /// <summary>Verifies implementation identity must match the manifest identity.</summary>
    [Fact]
    public async Task LoadAsync_rejects_implementation_identity_mismatch()
    {
        string manifestPath = await WriteManifestAsync(typeof(Phase22MismatchedPlugin));
        try
        {
            SkeletonKeyPluginLoadException exception = await Assert.ThrowsAsync<SkeletonKeyPluginLoadException>(async () =>
                await SkeletonKeyPluginLoader.LoadAsync([Path.GetDirectoryName(manifestPath)!]));

            Assert.Equal("SKP2207", exception.Code);
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    /// <summary>Verifies the Runner inventories and executes an explicitly supplied plugin.</summary>
    [Fact]
    public async Task Runner_plugins_and_run_compose_explicit_plugin()
    {
        string manifestPath = await WriteManifestAsync(typeof(Phase22FixturePlugin));
        try
        {
            string directory = Path.GetDirectoryName(manifestPath)!;
            StringWriter pluginsOutput = new();
            SkeletonKeyRunner pluginsRunner = new(TextReader.Null, pluginsOutput, TextWriter.Null);
            int pluginsExitCode = await pluginsRunner.ExecuteAsync(["plugins", "--plugin-directory", directory]);
            Assert.Equal(RunnerExitCodes.Success, pluginsExitCode);
            Assert.Contains("phase22.fixture", pluginsOutput.ToString(), StringComparison.Ordinal);

            const string workflow = """
                {
                  "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
                  "specVersion": "0.1.0",
                  "id": "phase22-plugin-smoke",
                  "name": "Phase 22 Plugin Smoke",
                  "inputs": {},
                  "variables": {},
                  "nodes": [
                    { "id": "start", "type": "core.start", "typeVersion": 1, "disabled": false, "parameters": {} },
                    { "id": "complete", "type": "phase22.fixture.complete", "typeVersion": 1, "disabled": false, "parameters": {} }
                  ],
                  "connections": [
                    { "from": { "node": "start", "port": "main" }, "to": { "node": "complete", "port": "main" } }
                  ]
                }
                """;
            StringWriter runOutput = new();
            SkeletonKeyRunner runRunner = new(new StringReader(workflow), runOutput, TextWriter.Null);
            int runExitCode = await runRunner.ExecuteAsync(["run", "--plugin-directory", directory]);
            Assert.True(runExitCode == RunnerExitCodes.Success, runOutput.ToString());
            Assert.Contains("Succeeded", runOutput.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    private static async ValueTask<string> WriteManifestAsync(
        Type entryType,
        string? sha256 = null,
        bool includeUnknownProperty = false)
    {
        string assemblyPath = entryType.Assembly.Location;
        string directory = Path.GetDirectoryName(assemblyPath)!;
        string manifestPath = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".skeletonkey-plugin.json");
        string hash = sha256 ?? Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(assemblyPath)));
        Dictionary<string, object> manifest = new(StringComparer.Ordinal)
        {
            ["schemaVersion"] = "0.1",
            ["id"] = "phase22.fixture",
            ["version"] = "1.0.0",
            ["assembly"] = Path.GetFileName(assemblyPath),
            ["entryType"] = entryType.FullName!,
            ["sha256"] = hash,
        };
        if (includeUnknownProperty)
        {
            manifest["unknown"] = true;
        }

        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest));
        return manifestPath;
    }
}
