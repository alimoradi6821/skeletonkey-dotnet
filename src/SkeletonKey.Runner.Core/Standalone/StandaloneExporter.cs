using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using SkeletonKey.Serialization.Json;
using SkeletonKey.Workflow.Documents;

namespace SkeletonKey.Runner.Core;

/// <summary>Result returned after building one sealed scenario-specific executable.</summary>
public sealed record StandaloneExportResult(
    string OutputPath,
    string PackageId,
    string WorkflowId,
    string WorkflowSha256,
    string SettingsSha256,
    string TargetRuntime,
    long Bytes);

/// <summary>Builds a sealed standalone executable from a workflow and host settings.</summary>
public sealed class StandaloneExporter
{
    private const int MaximumWorkflowBytes = 16 * 1024 * 1024;
    private const int MaximumSettingsBytes = 1024 * 1024;
    private const int MaximumLocatorBytes = 4 * 1024 * 1024;
    private const int MaximumLocatorFiles = 256;
    private const int MaximumWorkflowFiles = 1024;

    /// <summary>Parses export arguments, validates immutable inputs, and publishes the scenario executable.</summary>
    public async ValueTask<StandaloneExportResult> ExportAsync(IReadOnlyList<string> args, CancellationToken cancellationToken = default)
    {
        StandaloneExportOptions options = StandaloneExportOptions.Parse(args);
        if (!string.Equals(options.TargetRuntime, "win-x64", StringComparison.Ordinal))
        {
            throw new StandaloneExportException("SKX3001", "Standalone Export 0.1 currently supports only --runtime win-x64.");
        }

        string outputPath = Path.GetFullPath(options.OutputPath);
        if (!string.Equals(Path.GetExtension(outputPath), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new StandaloneExportException("SKX3002", "Standalone export output must use the .exe extension.");
        }

        string stagingRoot = Path.Combine(Path.GetTempPath(), "skeletonkey-standalone-export", Guid.NewGuid().ToString("N"));
        string publishRoot = Path.Combine(stagingRoot, "publish");
        Directory.CreateDirectory(stagingRoot);

        try
        {
            Snapshot snapshot = await CreateSnapshotAsync(options, stagingRoot, cancellationToken).ConfigureAwait(false);
            await ValidateSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);

            byte[] workflowBytes = await File.ReadAllBytesAsync(snapshot.WorkflowPath, cancellationToken).ConfigureAwait(false);
            byte[] settingsBytes = await File.ReadAllBytesAsync(snapshot.SettingsPath, cancellationToken).ConfigureAwait(false);
            string workflowJson = await File.ReadAllTextAsync(snapshot.WorkflowPath, cancellationToken).ConfigureAwait(false);
            string settingsJson = await File.ReadAllTextAsync(snapshot.SettingsPath, cancellationToken).ConfigureAwait(false);
            StandaloneExecutionSettings.Parse(settingsJson);
            WorkflowDocument workflow = new WorkflowJsonSerializer().Deserialize(workflowJson);

            string version = typeof(SkeletonKeyRunner).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? typeof(SkeletonKeyRunner).Assembly.GetName().Version?.ToString()
                ?? "0.0.0";

            StandaloneContentIdentity workflowIdentity = Identity("scenario.workflow.json", workflowBytes);
            StandaloneContentIdentity settingsIdentity = Identity("execution.settings.json", settingsBytes);
            IReadOnlyList<StandaloneContentIdentity> dependencies = await BuildDependencyIdentitiesAsync(snapshot, cancellationToken).ConfigureAwait(false);
            string packageId = StandalonePackageManifest.ComputePackageId(version, options.TargetRuntime, workflowIdentity, settingsIdentity, dependencies);
            StandalonePackageManifest manifest = new(
                StandalonePackageManifest.CurrentFormat,
                packageId,
                version,
                options.TargetRuntime,
                workflowIdentity,
                settingsIdentity,
                dependencies);

            string manifestPath = Path.Combine(stagingRoot, "package.manifest.json");
            await File.WriteAllTextAsync(manifestPath, manifest.Serialize(), cancellationToken).ConfigureAwait(false);

            string hostProject = LocateHostProject();
            string assemblyName = "SkeletonKeyStandalone_" + packageId[(packageId.LastIndexOf(':') + 1)..][..16];
            Directory.CreateDirectory(publishRoot);
            await PublishHostAsync(hostProject, assemblyName, snapshot, manifestPath, publishRoot, options.TargetRuntime, cancellationToken).ConfigureAwait(false);

            string publishedExe = Path.Combine(publishRoot, assemblyName + ".exe");
            if (!File.Exists(publishedExe))
            {
                throw new StandaloneExportException("SKX3003", "Standalone host publish completed without producing the expected executable.");
            }

            string? outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            File.Copy(publishedExe, outputPath, overwrite: true);
            FileInfo outputInfo = new(outputPath);
            return new StandaloneExportResult(
                outputPath,
                packageId,
                workflow.Id,
                workflowIdentity.Sha256,
                settingsIdentity.Sha256,
                options.TargetRuntime,
                outputInfo.Length);
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
        }
    }

    private static async ValueTask<Snapshot> CreateSnapshotAsync(StandaloneExportOptions options, string stagingRoot, CancellationToken cancellationToken)
    {
        string workflowPath = await SnapshotFileAsync(options.WorkflowPath, Path.Combine(stagingRoot, "scenario.workflow.json"), MaximumWorkflowBytes, cancellationToken).ConfigureAwait(false);
        string settingsPath = await SnapshotFileAsync(options.SettingsPath, Path.Combine(stagingRoot, "execution.settings.json"), MaximumSettingsBytes, cancellationToken).ConfigureAwait(false);

        string? locators = options.LocatorDirectory is null
            ? null
            : await SnapshotDirectoryAsync(options.LocatorDirectory, Path.Combine(stagingRoot, "locators"), "*.locators.json", MaximumLocatorFiles, MaximumLocatorBytes, cancellationToken).ConfigureAwait(false);
        string? workflows = options.WorkflowDirectory is null
            ? null
            : await SnapshotDirectoryAsync(options.WorkflowDirectory, Path.Combine(stagingRoot, "workflows"), "*.workflow.json", MaximumWorkflowFiles, MaximumWorkflowBytes, cancellationToken).ConfigureAwait(false);

        return new Snapshot(workflowPath, settingsPath, locators, workflows);
    }

    private static async ValueTask<string> SnapshotFileAsync(string sourcePath, string targetPath, long maximumBytes, CancellationToken cancellationToken)
    {
        string source = Path.GetFullPath(sourcePath);
        FileInfo info = new(source);
        if (!info.Exists)
        {
            throw new StandaloneExportException("SKX3004", "Standalone export input does not exist: " + source + ".");
        }

        if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new StandaloneExportException("SKX3005", "Standalone export inputs cannot be symbolic links or reparse points: " + info.Name + ".");
        }

        if (info.Length is <= 0 || info.Length > maximumBytes)
        {
            throw new StandaloneExportException("SKX3006", "Standalone export input has an invalid size: " + info.Name + ".");
        }

        byte[] bytes = await File.ReadAllBytesAsync(source, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(targetPath, bytes, cancellationToken).ConfigureAwait(false);
        return targetPath;
    }

    private static async ValueTask<string> SnapshotDirectoryAsync(
        string sourceDirectory,
        string targetDirectory,
        string pattern,
        int maximumFiles,
        long maximumFileBytes,
        CancellationToken cancellationToken)
    {
        string source = Path.GetFullPath(sourceDirectory);
        DirectoryInfo directory = new(source);
        if (!directory.Exists)
        {
            throw new StandaloneExportException("SKX3007", "Standalone export directory does not exist: " + source + ".");
        }

        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new StandaloneExportException("SKX3008", "Standalone export directories cannot be symbolic links or reparse points: " + source + ".");
        }

        string[] files = Directory.GetFiles(source, pattern, SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.Ordinal);
        if (files.Length > maximumFiles)
        {
            throw new StandaloneExportException("SKX3009", $"Standalone export directory exceeds its {maximumFiles}-file limit: {source}.");
        }

        Directory.CreateDirectory(targetDirectory);
        foreach (string file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileInfo info = new(file);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || info.Length is <= 0 || info.Length > maximumFileBytes)
            {
                throw new StandaloneExportException("SKX3010", "Standalone export dependency is unsafe or exceeds its size limit: " + info.Name + ".");
            }

            byte[] bytes = await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(Path.Combine(targetDirectory, info.Name), bytes, cancellationToken).ConfigureAwait(false);
        }

        return targetDirectory;
    }

    private static async ValueTask ValidateSnapshotAsync(Snapshot snapshot, CancellationToken cancellationToken)
    {
        string[] common = BuildRunnerArguments(snapshot);
        foreach (string stage in new[] { "validate", "analyze", "plan" })
        {
            using StringWriter output = new(System.Globalization.CultureInfo.InvariantCulture);
            using StringWriter error = new(System.Globalization.CultureInfo.InvariantCulture);
            SkeletonKeyRunner runner = new(new StringReader(string.Empty), output, error);
            int exitCode = await runner.ExecuteAsync([stage, .. common], cancellationToken).ConfigureAwait(false);
            if (exitCode != RunnerExitCodes.Success)
            {
                string detail = output.ToString().Trim();
                if (detail.Length == 0)
                {
                    detail = error.ToString().Trim();
                }

                throw new StandaloneExportException("SKX3011", $"Standalone export {stage} stage rejected the snapshot. {Bound(detail)}");
            }
        }
    }

    private static string[] BuildRunnerArguments(Snapshot snapshot)
    {
        List<string> args = ["--file", snapshot.WorkflowPath];
        if (snapshot.LocatorDirectory is not null)
        {
            args.Add("--locator-directory");
            args.Add(snapshot.LocatorDirectory);
        }

        if (snapshot.WorkflowDirectory is not null)
        {
            args.Add("--workflow-directory");
            args.Add(snapshot.WorkflowDirectory);
        }

        return args.ToArray();
    }

    private static async ValueTask<IReadOnlyList<StandaloneContentIdentity>> BuildDependencyIdentitiesAsync(Snapshot snapshot, CancellationToken cancellationToken)
    {
        List<StandaloneContentIdentity> identities = [];
        if (snapshot.LocatorDirectory is not null)
        {
            await AddDirectoryIdentitiesAsync(identities, snapshot.LocatorDirectory, "locators", "*.locators.json", cancellationToken).ConfigureAwait(false);
        }

        if (snapshot.WorkflowDirectory is not null)
        {
            await AddDirectoryIdentitiesAsync(identities, snapshot.WorkflowDirectory, "workflows", "*.workflow.json", cancellationToken).ConfigureAwait(false);
        }

        return identities.OrderBy(static item => item.Path, StringComparer.Ordinal).ToArray();
    }

    private static async ValueTask AddDirectoryIdentitiesAsync(
        List<StandaloneContentIdentity> identities,
        string directory,
        string logicalDirectory,
        string pattern,
        CancellationToken cancellationToken)
    {
        string[] files = Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.Ordinal);
        foreach (string file in files)
        {
            byte[] bytes = await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false);
            identities.Add(Identity(logicalDirectory + "/" + Path.GetFileName(file), bytes));
        }
    }

    private static StandaloneContentIdentity Identity(string path, byte[] bytes)
    {
        return new StandaloneContentIdentity(path, StandalonePackageManifest.ComputeSha256(bytes), bytes.LongLength);
    }

    private static string LocateHostProject()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? current = new(Path.GetFullPath(start));
            while (current is not null)
            {
                string candidate = Path.Combine(current.FullName, "tools", "SkeletonKey.Standalone.Host", "SkeletonKey.Standalone.Host.csproj");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }
        }

        throw new StandaloneExportException(
            "SKX3012",
            "Standalone host project was not found. Version 0.1 export must run from a SkeletonKey source checkout that contains tools/SkeletonKey.Standalone.Host.");
    }

    private static async ValueTask PublishHostAsync(
        string project,
        string assemblyName,
        Snapshot snapshot,
        string manifestPath,
        string publishDirectory,
        string runtime,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo start = new()
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("publish");
        start.ArgumentList.Add(project);
        start.ArgumentList.Add("--configuration");
        start.ArgumentList.Add("Release");
        start.ArgumentList.Add("--runtime");
        start.ArgumentList.Add(runtime);
        start.ArgumentList.Add("--self-contained");
        start.ArgumentList.Add("true");
        start.ArgumentList.Add("--output");
        start.ArgumentList.Add(publishDirectory);
        AddProperty(start, "AssemblyName", assemblyName);
        AddProperty(start, "StandaloneWorkflowPath", snapshot.WorkflowPath);
        AddProperty(start, "StandaloneSettingsPath", snapshot.SettingsPath);
        AddProperty(start, "StandalonePackageManifestPath", manifestPath);
        if (snapshot.LocatorDirectory is not null)
        {
            AddProperty(start, "StandaloneLocatorDirectory", snapshot.LocatorDirectory);
        }

        if (snapshot.WorkflowDirectory is not null)
        {
            AddProperty(start, "StandaloneWorkflowDirectory", snapshot.WorkflowDirectory);
        }

        Process? started;
        try
        {
            started = Process.Start(start);
        }
        catch (Win32Exception exception)
        {
            throw new StandaloneExportException("SKX3013", "Could not start dotnet publish for the standalone host.", exception);
        }

        using Process process = started ?? throw new StandaloneExportException("SKX3013", "Could not start dotnet publish for the standalone host.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }

            throw;
        }

        string output = await stdout.ConfigureAwait(false);
        string error = await stderr.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            string detail = string.IsNullOrWhiteSpace(error) ? output : error;
            throw new StandaloneExportException("SKX3014", $"Standalone host publish failed with exit code {process.ExitCode}. {Bound(detail)}");
        }
    }

    private static void AddProperty(ProcessStartInfo start, string name, string value)
    {
        start.ArgumentList.Add("-p:" + name + "=" + value);
    }

    private static string Bound(string value)
    {
        string normalized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= 1024 ? normalized : normalized[..1024];
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record Snapshot(string WorkflowPath, string SettingsPath, string? LocatorDirectory, string? WorkflowDirectory);
}

/// <summary>Standalone export command-line options.</summary>
public sealed class StandaloneExportOptions
{
    private StandaloneExportOptions(string workflowPath, string settingsPath, string outputPath, string? locatorDirectory, string? workflowDirectory, string targetRuntime)
    {
        WorkflowPath = workflowPath;
        SettingsPath = settingsPath;
        OutputPath = outputPath;
        LocatorDirectory = locatorDirectory;
        WorkflowDirectory = workflowDirectory;
        TargetRuntime = targetRuntime;
    }

    /// <summary>Root workflow path.</summary>
    public string WorkflowPath { get; }

    /// <summary>Standalone execution settings path.</summary>
    public string SettingsPath { get; }

    /// <summary>Requested executable output path.</summary>
    public string OutputPath { get; }

    /// <summary>Optional locator-document directory.</summary>
    public string? LocatorDirectory { get; }

    /// <summary>Optional subworkflow directory.</summary>
    public string? WorkflowDirectory { get; }

    /// <summary>Target runtime identifier.</summary>
    public string TargetRuntime { get; }

    /// <summary>Parses the `export standalone` argument tail.</summary>
    public static StandaloneExportOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || !string.Equals(args[0], "standalone", StringComparison.Ordinal))
        {
            throw new StandaloneExportException("SKX3020", "export currently requires the 'standalone' subcommand.");
        }

        string? workflow = null;
        string? settings = null;
        string? output = null;
        string? locators = null;
        string? workflows = null;
        string runtime = "win-x64";

        for (int index = 1; index < args.Count; index++)
        {
            string item = args[index];
            string Next()
            {
                if (++index >= args.Count)
                {
                    throw new StandaloneExportException("SKX3021", "Missing value for " + item + ".");
                }

                return args[index];
            }

            switch (item)
            {
                case "--workflow":
                case "--file":
                case "-f":
                    workflow = Next();
                    break;
                case "--settings":
                    settings = Next();
                    break;
                case "--output":
                case "-o":
                    output = Next();
                    break;
                case "--locator-directory":
                    locators = Next();
                    break;
                case "--workflow-directory":
                    workflows = Next();
                    break;
                case "--runtime":
                    runtime = Next();
                    break;
                default:
                    throw new StandaloneExportException("SKX3022", "Unknown standalone export option: " + item + ".");
            }
        }

        if (string.IsNullOrWhiteSpace(workflow) || string.IsNullOrWhiteSpace(settings) || string.IsNullOrWhiteSpace(output))
        {
            throw new StandaloneExportException("SKX3023", "export standalone requires --workflow, --settings, and --output.");
        }

        return new StandaloneExportOptions(workflow, settings, output, locators, workflows, runtime);
    }
}

/// <summary>Thrown when standalone export cannot safely produce a package.</summary>
public sealed class StandaloneExportException : Exception
{
    /// <summary>Initializes an export exception with a stable diagnostic code.</summary>
    public StandaloneExportException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>Stable export diagnostic code.</summary>
    public string Code { get; }
}
