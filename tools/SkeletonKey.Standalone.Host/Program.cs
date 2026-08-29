using System.Reflection;
using System.Text;
using SkeletonKey.Desktop.FlaUI;
using SkeletonKey.Runner.Core;

if (args.Length != 0)
{
    Console.Error.WriteLine("This SkeletonKey standalone application is sealed and does not accept runtime workflow or settings arguments.");
    return RunnerExitCodes.Usage;
}

using CancellationTokenSource shutdown = new();
ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};
Console.CancelKeyPress += cancelHandler;

string? workspace = null;
try
{
    StandaloneEmbeddedPayload payload = await StandaloneEmbeddedPayload.MaterializeAsync(Assembly.GetExecutingAssembly(), shutdown.Token).ConfigureAwait(false);
    workspace = payload.Workspace;
    StandaloneExecutionSettings settings = StandaloneExecutionSettings.Parse(await File.ReadAllTextAsync(payload.SettingsPath, shutdown.Token).ConfigureAwait(false));

    DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
    StandaloneScheduleCursor cursor = new(settings.Schedule, startedAtUtc);
    long sequence = 0;

    async ValueTask<int> RunOccurrenceAsync()
    {
        string executionId = payload.Manifest.PackageId + ":" + Interlocked.Increment(ref sequence).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ", System.Globalization.CultureInfo.InvariantCulture);
        List<string> runnerArgs = ["run", "--file", payload.WorkflowPath, "--execution-id", executionId, "--format", "json"];
        if (payload.LocatorDirectory is not null)
        {
            runnerArgs.Add("--locator-directory");
            runnerArgs.Add(payload.LocatorDirectory);
        }

        if (payload.WorkflowDirectory is not null)
        {
            runnerArgs.Add("--workflow-directory");
            runnerArgs.Add(payload.WorkflowDirectory);
        }

        using StringReader input = new(string.Empty);
        SkeletonKeyRunner runner = new(
            input,
            Console.Out,
            Console.Error,
            [new FlaUiApplicationResourceProvider()]);
        return await runner.ExecuteAsync(runnerArgs, shutdown.Token).ConfigureAwait(false);
    }

    if (settings.Schedule.Kind == StandaloneScheduleKind.Once)
    {
        return await RunOccurrenceAsync().ConfigureAwait(false);
    }

    if (settings.Execution.RunImmediately)
    {
        int immediate = await RunOccurrenceAsync().ConfigureAwait(false);
        if (immediate == RunnerExitCodes.Cancelled)
        {
            return immediate;
        }

        if (immediate != RunnerExitCodes.Success && !settings.Execution.ContinueAfterFailure)
        {
            return immediate;
        }
    }

    while (!shutdown.IsCancellationRequested)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset due = cursor.GetNextDueAfter(now) ?? throw new InvalidOperationException("Recurring standalone schedule did not produce a future occurrence.");
        TimeSpan delay = due - now;
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, shutdown.Token).ConfigureAwait(false);
        }

        int result = await RunOccurrenceAsync().ConfigureAwait(false);
        if (result == RunnerExitCodes.Cancelled)
        {
            return result;
        }

        if (result != RunnerExitCodes.Success)
        {
            if (!settings.Execution.ContinueAfterFailure)
            {
                return result;
            }

            Console.Error.WriteLine("[standalone-occurrence-failed] A workflow occurrence failed; the recurring schedule remains active.");
        }

        // The next due boundary is recalculated strictly after the completed occurrence.
        // Boundaries that passed while execution was active are therefore skipped, which
        // implements the 0.1 overlap=skip contract without parallel execution.
    }

    return RunnerExitCodes.Cancelled;
}
catch (OperationCanceledException)
{
    return RunnerExitCodes.Cancelled;
}
catch (StandaloneSettingsException exception)
{
    Console.Error.WriteLine($"[{exception.Code}] {exception.Message}");
    return RunnerExitCodes.Failed;
}
catch (StandalonePackageException exception)
{
    Console.Error.WriteLine($"[{exception.Code}] {exception.Message}");
    return RunnerExitCodes.Failed;
}
catch (Exception exception)
{
    Console.Error.WriteLine("[SKX2999] " + exception.Message);
    return RunnerExitCodes.Exception;
}
finally
{
    Console.CancelKeyPress -= cancelHandler;
    if (workspace is not null)
    {
        try
        {
            Directory.Delete(workspace, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

internal sealed record StandaloneEmbeddedPayload(
    string Workspace,
    string WorkflowPath,
    string SettingsPath,
    string? LocatorDirectory,
    string? WorkflowDirectory,
    StandalonePackageManifest Manifest)
{
    private const string WorkflowResource = "SkeletonKey.Standalone.workflow.json";
    private const string SettingsResource = "SkeletonKey.Standalone.execution.settings.json";
    private const string ManifestResource = "SkeletonKey.Standalone.package.manifest.json";
    private const string LocatorPrefix = "SkeletonKey.Standalone.Locators.";
    private const string WorkflowPrefix = "SkeletonKey.Standalone.Workflows.";

    public static async ValueTask<StandaloneEmbeddedPayload> MaterializeAsync(Assembly assembly, CancellationToken cancellationToken)
    {
        byte[] manifestBytes = await ReadResourceAsync(assembly, ManifestResource, cancellationToken).ConfigureAwait(false);
        StandalonePackageManifest manifest = StandalonePackageManifest.Deserialize(Encoding.UTF8.GetString(manifestBytes));

        byte[] workflowBytes = await ReadResourceAsync(assembly, WorkflowResource, cancellationToken).ConfigureAwait(false);
        byte[] settingsBytes = await ReadResourceAsync(assembly, SettingsResource, cancellationToken).ConfigureAwait(false);
        Verify(manifest.Workflow, workflowBytes, "scenario.workflow.json");
        Verify(manifest.Settings, settingsBytes, "execution.settings.json");

        string workspace = Path.Combine(Path.GetTempPath(), "skeletonkey-standalone", SafePackageSegment(manifest.PackageId), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        string workflowPath = Path.Combine(workspace, "scenario.workflow.json");
        string settingsPath = Path.Combine(workspace, "execution.settings.json");
        await File.WriteAllBytesAsync(workflowPath, workflowBytes, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(settingsPath, settingsBytes, cancellationToken).ConfigureAwait(false);

        string[] resources = assembly.GetManifestResourceNames();
        string? locatorDirectory = null;
        string? workflowDirectory = null;
        foreach (StandaloneContentIdentity dependency in manifest.Dependencies.OrderBy(static item => item.Path, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string resourceName;
            string targetDirectory;
            if (dependency.Path.StartsWith("locators/", StringComparison.Ordinal))
            {
                string name = dependency.Path["locators/".Length..];
                resourceName = LocatorPrefix + name;
                locatorDirectory ??= Path.Combine(workspace, "locators");
                targetDirectory = locatorDirectory;
            }
            else if (dependency.Path.StartsWith("workflows/", StringComparison.Ordinal))
            {
                string name = dependency.Path["workflows/".Length..];
                resourceName = WorkflowPrefix + name;
                workflowDirectory ??= Path.Combine(workspace, "workflows");
                targetDirectory = workflowDirectory;
            }
            else
            {
                throw new StandalonePackageException("SKX2002", "Unsupported embedded dependency path: " + dependency.Path + ".");
            }

            if (!resources.Contains(resourceName, StringComparer.Ordinal))
            {
                throw new StandalonePackageException("SKX2003", "Embedded dependency resource is missing: " + dependency.Path + ".");
            }

            byte[] bytes = await ReadResourceAsync(assembly, resourceName, cancellationToken).ConfigureAwait(false);
            Verify(dependency, bytes, dependency.Path);
            Directory.CreateDirectory(targetDirectory);
            string fileName = Path.GetFileName(dependency.Path);
            await File.WriteAllBytesAsync(Path.Combine(targetDirectory, fileName), bytes, cancellationToken).ConfigureAwait(false);
        }

        return new StandaloneEmbeddedPayload(workspace, workflowPath, settingsPath, locatorDirectory, workflowDirectory, manifest);
    }

    private static async ValueTask<byte[]> ReadResourceAsync(Assembly assembly, string name, CancellationToken cancellationToken)
    {
        await using Stream stream = assembly.GetManifestResourceStream(name)
            ?? throw new StandalonePackageException("SKX2004", "Required embedded standalone resource is missing: " + name + ".");
        using MemoryStream buffer = new();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    private static void Verify(StandaloneContentIdentity expected, byte[] bytes, string path)
    {
        string digest = StandalonePackageManifest.ComputeSha256(bytes);
        if (expected.Bytes != bytes.LongLength || !string.Equals(expected.Sha256, digest, StringComparison.Ordinal))
        {
            throw new StandalonePackageException("SKX2005", "Embedded standalone content failed digest verification: " + path + ".");
        }
    }

    private static string SafePackageSegment(string packageId)
    {
        string digest = packageId[(packageId.LastIndexOf(':') + 1)..];
        return digest.Length <= 32 ? digest : digest[..32];
    }
}
