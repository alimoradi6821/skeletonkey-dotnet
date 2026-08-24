using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Events;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Analysis;
using SkeletonKey.Analysis.Default;
using SkeletonKey.Artifacts.FileSystem;
using SkeletonKey.BuiltIns;
using SkeletonKey.BuiltIns.Runtime;
using SkeletonKey.Catalog;
using SkeletonKey.Desktop.BuiltIns;
using SkeletonKey.Handlers;
using SkeletonKey.Locators;
using SkeletonKey.Locators.Json;
using SkeletonKey.Locators.Runtime;
using SkeletonKey.Locators.Validation;
using SkeletonKey.Materialization;
using SkeletonKey.Planning;
using SkeletonKey.Planning.Default;
using SkeletonKey.Runner.Core.Plugins;
using SkeletonKey.Runtime;
using SkeletonKey.Runtime.Default;
using SkeletonKey.Runtime.Invocation;
using SkeletonKey.Runtime.Resources;
using SkeletonKey.Serialization.Json;
using SkeletonKey.Validation;
using SkeletonKey.Web.BuiltIns;
using SkeletonKey.Web.Playwright;
using SkeletonKey.Workflow.Documents;

namespace SkeletonKey.Runner.Core;

/// <summary>
/// Executes SkeletonKey runner commands and writes deterministic JSON envelopes.
/// </summary>
public sealed class SkeletonKeyRunner
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = false };
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly TextWriter _error;
    private readonly IReadOnlyList<IWorkflowRuntimeResourceProvider> _hostResourceProviders;
    private RunnerOutputFormat _outputFormat;
    private bool _diagnostics;

    /// <summary>Initializes a runner facade over explicit streams.</summary>
    public SkeletonKeyRunner(
        TextReader input,
        TextWriter output,
        TextWriter error,
        IReadOnlyList<IWorkflowRuntimeResourceProvider>? hostResourceProviders = null)
    {
        _input = input;
        _output = output;
        _error = error;
        _hostResourceProviders = Array.AsReadOnly([.. hostResourceProviders ?? []]);
    }

    /// <summary>Executes one command and returns a process-style exit code.</summary>
    public async ValueTask<int> ExecuteAsync(IReadOnlyList<string> args, CancellationToken cancellationToken = default)
    {
        if (args.Count == 0 || IsHelp(args[0]))
        {
            await WriteUsageAsync().ConfigureAwait(false);
            return RunnerExitCodes.Usage;
        }

        try
        {
            string command = args[0];
            var options = RunnerOptions.Parse(args.Skip(1).ToArray());
            _outputFormat = options.OutputFormat;
            _diagnostics = options.Diagnostics;
            await WriteDiagnosticAsync("command-start", command).ConfigureAwait(false);
            return command switch
            {
                "version" => await VersionAsync(cancellationToken).ConfigureAwait(false),
                "plugins" => await PluginsAsync(options, cancellationToken).ConfigureAwait(false),
                "validate" => await ValidateAsync(options, cancellationToken).ConfigureAwait(false),
                "analyze" => await AnalyzeAsync(options, cancellationToken).ConfigureAwait(false),
                "plan" => await PlanAsync(options, cancellationToken).ConfigureAwait(false),
                "run" => await RunAsync(options, cancellationToken).ConfigureAwait(false),
                "resume" => await ResumeAsync(options, cancellationToken).ConfigureAwait(false),
                "install-browsers" => await InstallBrowsersAsync(options, cancellationToken).ConfigureAwait(false),
                _ => await UnknownCommandAsync(command).ConfigureAwait(false),
            };
        }
        catch (OperationCanceledException)
        {
            await WriteEnvelopeAsync(RunnerEnvelope.Failure("cancelled", "Operation was cancelled.", "SKR1300"), cancellationToken: CancellationToken.None).ConfigureAwait(false);
            return RunnerExitCodes.Cancelled;
        }
        catch (RunnerUsageException exception)
        {
            await _error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return RunnerExitCodes.Usage;
        }
        catch (WorkflowCheckpointStoreException exception)
        {
            await WriteEnvelopeAsync(RunnerEnvelope.Failure(args[0], exception.Message, exception.Code), cancellationToken: CancellationToken.None).ConfigureAwait(false);
            return RunnerExitCodes.Failed;
        }
        catch (SkeletonKeyPluginLoadException exception)
        {
            await WriteEnvelopeAsync(RunnerEnvelope.Failure(args[0], exception.Message, exception.Code), cancellationToken: CancellationToken.None).ConfigureAwait(false);
            return RunnerExitCodes.Failed;
        }
        catch (Exception exception)
        {
            await WriteEnvelopeAsync(RunnerEnvelope.Failure("error", exception.Message, "SKR1999"), cancellationToken: CancellationToken.None).ConfigureAwait(false);
            return RunnerExitCodes.Exception;
        }
    }

    private async ValueTask<int> VersionAsync(CancellationToken cancellationToken)
    {
        Assembly assembly = typeof(SkeletonKeyRunner).Assembly;
        await WriteEnvelopeAsync(RunnerEnvelope.Success("version", new Dictionary<string, object?>
        {
            ["assemblyVersion"] = assembly.GetName().Version?.ToString() ?? "0.0.0.0",
            ["informationalVersion"] = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0",
            ["targetFramework"] = "net10.0",
        }), cancellationToken).ConfigureAwait(false);
        return RunnerExitCodes.Success;
    }

    private async ValueTask<int> ValidateAsync(RunnerOptions options, CancellationToken cancellationToken)
    {
        WorkflowDocument workflow = await ReadWorkflowAsync(options, cancellationToken).ConfigureAwait(false);
        WorkflowValidationResult validation = new WorkflowSemanticValidator().Validate(workflow);
        await WriteEnvelopeAsync(RunnerEnvelope.FromValidation("validate", validation), cancellationToken).ConfigureAwait(false);
        return validation.IsValid ? RunnerExitCodes.Success : RunnerExitCodes.Failed;
    }

    private async ValueTask<int> AnalyzeAsync(RunnerOptions options, CancellationToken cancellationToken)
    {
        WorkflowDocument workflow = await ReadWorkflowAsync(options, cancellationToken).ConfigureAwait(false);
        SkeletonKeyPluginLoadResult plugins = await LoadPluginsAsync(options, cancellationToken).ConfigureAwait(false);
        ILocatorPlanResolver? locatorResolver = await LoadLocatorResolverAsync(options, cancellationToken).ConfigureAwait(false);
        WorkflowAnalysisResult analysis = new DefaultWorkflowAnalyzer(locatorResolver: locatorResolver).Analyze(workflow, Catalog(plugins));
        if (!analysis.CanPlanExecution)
        {
            await WriteEnvelopeAsync(RunnerEnvelope.FromAnalysis("analyze", analysis), cancellationToken).ConfigureAwait(false);
            return RunnerExitCodes.Failed;
        }

        ImmutableWorkflowRepository repository = await LoadWorkflowRepositoryAsync(options, workflow, cancellationToken).ConfigureAwait(false);
        WorkflowInvocationAnalysisResult invocationAnalysis = await new WorkflowInvocationGraphAnalyzer().AnalyzeAsync(workflow, repository, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!invocationAnalysis.IsValid)
        {
            await WriteEnvelopeAsync(RunnerEnvelope.FromInvocationAnalysis("analyze", invocationAnalysis), cancellationToken).ConfigureAwait(false);
            return RunnerExitCodes.Failed;
        }

        await WriteEnvelopeAsync(RunnerEnvelope.FromAnalysis("analyze", analysis), cancellationToken).ConfigureAwait(false);
        return RunnerExitCodes.Success;
    }

    private async ValueTask<int> PlanAsync(RunnerOptions options, CancellationToken cancellationToken)
    {
        WorkflowDocument workflow = await ReadWorkflowAsync(options, cancellationToken).ConfigureAwait(false);
        SkeletonKeyPluginLoadResult plugins = await LoadPluginsAsync(options, cancellationToken).ConfigureAwait(false);
        ILocatorPlanResolver? locatorResolver = await LoadLocatorResolverAsync(options, cancellationToken).ConfigureAwait(false);
        WorkflowAnalysisResult analysis = new DefaultWorkflowAnalyzer(locatorResolver: locatorResolver).Analyze(workflow, Catalog(plugins));
        if (!analysis.CanPlanExecution)
        {
            await WriteEnvelopeAsync(RunnerEnvelope.FromAnalysis("plan", analysis), cancellationToken).ConfigureAwait(false);
            return RunnerExitCodes.Failed;
        }

        ImmutableWorkflowRepository repository = await LoadWorkflowRepositoryAsync(options, workflow, cancellationToken).ConfigureAwait(false);
        WorkflowInvocationAnalysisResult invocationAnalysis = await new WorkflowInvocationGraphAnalyzer().AnalyzeAsync(workflow, repository, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!invocationAnalysis.IsValid)
        {
            await WriteEnvelopeAsync(RunnerEnvelope.FromInvocationAnalysis("plan", invocationAnalysis), cancellationToken).ConfigureAwait(false);
            return RunnerExitCodes.Failed;
        }

        WorkflowExecutionPlanResult planning = new DefaultWorkflowExecutionPlanner().Plan(workflow, analysis);
        await WriteEnvelopeAsync(RunnerEnvelope.FromPlan("plan", planning), cancellationToken).ConfigureAwait(false);
        return planning.IsReady ? RunnerExitCodes.Success : RunnerExitCodes.Failed;
    }

    private async ValueTask<int> RunAsync(RunnerOptions options, CancellationToken cancellationToken)
    {
        FileSystemWorkflowCheckpointStore? checkpointStore = options.CheckpointDirectory is null ? null : new FileSystemWorkflowCheckpointStore(options.CheckpointDirectory);
        return await ExecuteWorkflowAsync("run", options, checkpointStore, resumeCheckpoint: null, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<int> ResumeAsync(RunnerOptions options, CancellationToken cancellationToken)
    {
        if (options.CheckpointDirectory is null || options.ExecutionId is null)
        {
            throw new RunnerUsageException("resume requires --checkpoint-directory and --execution-id.");
        }

        FileSystemWorkflowCheckpointStore checkpointStore = new(options.CheckpointDirectory);
        WorkflowExecutionCheckpoint? checkpoint = await checkpointStore.LoadAsync(options.ExecutionId, cancellationToken).ConfigureAwait(false);
        if (checkpoint is null)
        {
            await WriteEnvelopeAsync(RunnerEnvelope.Failure("resume", "No checkpoint exists for the requested execution identifier.", WorkflowCheckpointErrorCodes.InvalidCheckpoint), cancellationToken).ConfigureAwait(false);
            return RunnerExitCodes.Failed;
        }

        return await ExecuteWorkflowAsync("resume", options, checkpointStore, checkpoint, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<int> ExecuteWorkflowAsync(
        string command,
        RunnerOptions options,
        IWorkflowCheckpointStore? checkpointStore,
        WorkflowExecutionCheckpoint? resumeCheckpoint,
        CancellationToken cancellationToken)
    {
        WorkflowDocument workflow = await ReadWorkflowAsync(options, cancellationToken).ConfigureAwait(false);
        ImmutableWorkflowRepository workflowRepository = await LoadWorkflowRepositoryAsync(options, workflow, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<string, JsonNode?> inputs = await ReadInputsAsync(options, cancellationToken).ConfigureAwait(false);
        SkeletonKeyPluginLoadResult plugins = await LoadPluginsAsync(options, cancellationToken).ConfigureAwait(false);
        ILocatorPlanResolver? locatorResolver = await LoadLocatorResolverAsync(options, cancellationToken).ConfigureAwait(false);
        WorkflowNodeDefinitionCatalog catalog = Catalog(plugins);
        IReadOnlyList<INodeHandler> handlers = ComposeHandlers(plugins);
        IReadOnlyList<IWorkflowRuntimeResourceProvider> resourceProviders = ComposeResourceProviders(plugins);
        DefaultWorkflowRuntime runtime = new(
            new WorkflowSemanticValidator(),
            new DefaultWorkflowAnalyzer(locatorResolver: locatorResolver),
            new DefaultWorkflowExecutionPlanner(),
            catalog,
            new ImmutableNodeHandlerResolver(handlers),
            new NodeParameterMaterializer(),
            options: new WorkflowRuntimeOptions(maximumExecutedNodeAttempts: 1000),
            workflowRepository: workflowRepository,
            resourceProviders: resourceProviders,
            locatorResolver: locatorResolver);

        string executionId = options.ExecutionId ?? "execution";
        string planId = ComputePlanId(workflow);
        BufferedWorkflowEventSink? eventSink = options.OutputFormat == RunnerOutputFormat.Ndjson ? new BufferedWorkflowEventSink() : null;
        WorkflowRuntimeResult result = await runtime.ExecuteAsync(new WorkflowExecutionRequest(
            workflow,
            executionId,
            planId,
            inputs,
            eventSink: eventSink,
            checkpointStore: checkpointStore,
            resumeCheckpoint: resumeCheckpoint), cancellationToken).ConfigureAwait(false);
        bool accepted = result.Result.Status == WorkflowExecutionStatus.Succeeded;
        if (eventSink is not null)
        {
            foreach (WorkflowEvent workflowEvent in eventSink.Events)
            {
                await WriteNdjsonRecordAsync(new { type = "event", @event = workflowEvent }, cancellationToken).ConfigureAwait(false);
            }
        }
        await WriteEnvelopeAsync(RunnerEnvelope.FromRuntime(command, accepted, result), cancellationToken).ConfigureAwait(false);
        return accepted ? RunnerExitCodes.Success : RunnerExitCodes.Failed;
    }

    private static string ComputePlanId(WorkflowDocument workflow)
    {
        string canonical = new WorkflowJsonSerializer().Serialize(workflow, indented: false);
        return "plan:sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private async ValueTask<int> InstallBrowsersAsync(RunnerOptions options, CancellationToken cancellationToken)
    {
        string browser = options.Browser ?? "chromium";
        if (browser is not ("chromium" or "firefox" or "webkit" or "all"))
        {
            throw new RunnerUsageException("Browser must be chromium, firefox, webkit, or all.");
        }

        ProcessStartInfo start = new()
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add(BrowserInstallerPath());
        start.ArgumentList.Add(browser);

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start dotnet Playwright installer.");
        string stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        string stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        bool accepted = process.ExitCode == 0;
        await WriteEnvelopeAsync(accepted
            ? RunnerEnvelope.Success("install-browsers", new Dictionary<string, object?> { ["browser"] = browser, ["exitCode"] = process.ExitCode, ["stdout"] = stdout })
            : RunnerEnvelope.Failure("install-browsers", stderr.Length == 0 ? stdout : stderr, "SKR2039"), cancellationToken).ConfigureAwait(false);
        return accepted ? RunnerExitCodes.Success : RunnerExitCodes.Failed;
    }

    private static string BrowserInstallerPath()
    {
        DirectoryInfo current = new(AppContext.BaseDirectory);
        while (current.Parent is not null)
        {
            string candidate = Path.Combine(current.FullName, "tools", "SkeletonKey.Playwright.BrowserInstaller", "bin", "Release", "net10.0", "skeletonkey.playwright-installer.dll");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Playwright browser installer DLL was not found.");
    }

    private async ValueTask<WorkflowDocument> ReadWorkflowAsync(RunnerOptions options, CancellationToken cancellationToken)
    {
        string json = options.WorkflowPath switch
        {
            null or "-" => await _input.ReadToEndAsync(cancellationToken).ConfigureAwait(false),
            _ => await File.ReadAllTextAsync(options.WorkflowPath, cancellationToken).ConfigureAwait(false),
        };
        return new WorkflowJsonSerializer().Deserialize(json);
    }

    private static async ValueTask<ImmutableWorkflowRepository> LoadWorkflowRepositoryAsync(
        RunnerOptions options,
        WorkflowDocument root,
        CancellationToken cancellationToken)
    {
        Dictionary<string, WorkflowDocument> workflows = new(StringComparer.Ordinal)
        {
            [root.Id] = root,
        };
        if (options.WorkflowDirectory is null)
        {
            return new ImmutableWorkflowRepository(workflows);
        }

        string directory = Path.GetFullPath(options.WorkflowDirectory);
        if (!Directory.Exists(directory))
        {
            throw new RunnerUsageException("Workflow directory does not exist: " + directory + ".");
        }

        string? rootPath = options.WorkflowPath is null or "-" ? null : Path.GetFullPath(options.WorkflowPath);
        string[] files = Directory.GetFiles(directory, "*.workflow.json", SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.Ordinal);
        if (files.Length > 1024)
        {
            throw new RunnerUsageException("Workflow directory cannot contain more than 1024 workflow documents.");
        }

        WorkflowJsonSerializer serializer = new();
        foreach (string file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fullPath = Path.GetFullPath(file);
            if (rootPath is not null && string.Equals(fullPath, rootPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            FileInfo info = new(fullPath);
            if (info.Length > 16 * 1024 * 1024)
            {
                throw new RunnerUsageException("Workflow document exceeds the 16 MiB limit: " + info.Name + ".");
            }

            WorkflowDocument workflow = serializer.Deserialize(await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false));
            string registration = RegistrationKey(info.Name, workflow);
            if (string.Equals(registration, root.Id, StringComparison.Ordinal))
            {
                continue;
            }

            if (!workflows.TryAdd(registration, workflow))
            {
                throw new RunnerUsageException("Duplicate workflow repository registration: " + registration + ".");
            }
        }

        return new ImmutableWorkflowRepository(workflows);
    }

    private static string RegistrationKey(string fileName, WorkflowDocument workflow)
    {
        const string suffix = ".workflow.json";
        string stem = fileName[..^suffix.Length];
        int separator = stem.LastIndexOf('@');
        if (separator < 0)
        {
            return workflow.Id;
        }

        string id = stem[..separator];
        string version = stem[(separator + 1)..];
        if (!string.Equals(id, workflow.Id, StringComparison.Ordinal) || version.Length == 0)
        {
            throw new RunnerUsageException("Versioned workflow filenames must use <workflow-id>@<exact-version>.workflow.json.");
        }

        return id + "@" + version;
    }

    private static async ValueTask<ILocatorPlanResolver?> LoadLocatorResolverAsync(RunnerOptions options, CancellationToken cancellationToken)
    {
        if (options.LocatorDirectory is null)
        {
            return null;
        }

        string directory = Path.GetFullPath(options.LocatorDirectory);
        if (!Directory.Exists(directory))
        {
            throw new RunnerUsageException("Locator directory does not exist: " + directory + ".");
        }

        string[] files = Directory.GetFiles(directory, "*.locators.json", SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.Ordinal);
        if (files.Length > 256)
        {
            throw new RunnerUsageException("Locator directory cannot contain more than 256 locator documents.");
        }

        LocatorJsonSerializer serializer = new();
        LocatorSemanticValidator validator = new();
        List<LocatorDocument> documents = [];
        foreach (string file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileInfo info = new(Path.GetFullPath(file));
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new RunnerUsageException("Locator documents cannot be symbolic links: " + info.Name + ".");
            }

            if (info.Length is <= 0 or > 4 * 1024 * 1024)
            {
                throw new RunnerUsageException("Locator document must be non-empty and at most 4 MiB: " + info.Name + ".");
            }

            LocatorDocument document;
            try
            {
                document = serializer.Deserialize(await File.ReadAllTextAsync(info.FullName, cancellationToken).ConfigureAwait(false));
            }
            catch (LocatorSerializationException exception)
            {
                throw new RunnerUsageException("Locator document is invalid: " + info.Name + ". " + exception.Message);
            }

            LocatorValidationResult validation = validator.Validate(document);
            if (!validation.IsValid)
            {
                throw new RunnerUsageException("Locator document failed semantic validation: " + info.Name + ". " + validation.Issues[0].Code + ".");
            }

            documents.Add(document);
        }

        try
        {
            return new LocatorPlanResolver(new ImmutableLocatorDocumentRepository(documents));
        }
        catch (ArgumentException exception)
        {
            throw new RunnerUsageException("Locator directory contains duplicate catalog identities: " + exception.Message);
        }
    }

    private static async ValueTask<IReadOnlyDictionary<string, JsonNode?>> ReadInputsAsync(RunnerOptions options, CancellationToken cancellationToken)
    {
        if (options.InputsJson is null && options.InputsPath is null)
        {
            return new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        }

        string json = options.InputsJson ?? await File.ReadAllTextAsync(options.InputsPath!, cancellationToken).ConfigureAwait(false);
        var node = JsonNode.Parse(json);
        if (node is not JsonObject inputObject)
        {
            throw new RunnerUsageException("Inputs must be a JSON object.");
        }

        return inputObject.ToDictionary(static pair => pair.Key, static pair => pair.Value?.DeepClone(), StringComparer.Ordinal);
    }

    private static WorkflowNodeDefinitionCatalog Catalog(SkeletonKeyPluginLoadResult plugins)
    {
        try
        {
            return new WorkflowNodeDefinitionCatalog([.. BuiltInWorkflowNodeCatalog.Catalog.Definitions, .. WebBuiltInWorkflowNodeCatalog.Catalog.Definitions, .. DesktopBuiltInWorkflowNodeCatalog.Catalog.Definitions, .. plugins.NodeDefinitions]);
        }
        catch (ArgumentException exception)
        {
            throw new SkeletonKeyPluginLoadException("SKP2208", "A plugin node definition conflicts with the host catalog.", exception);
        }
    }

    private static IReadOnlyList<INodeHandler> ComposeHandlers(SkeletonKeyPluginLoadResult plugins)
    {
        IReadOnlyList<INodeHandler> handlers = [.. BuiltInRuntimeHandlers.Create(), .. WebBuiltInRuntimeHandlers.Create(), .. DesktopBuiltInRuntimeHandlers.Create(), .. plugins.NodeHandlers];
        try
        {
            _ = new ImmutableNodeHandlerResolver(handlers);
            return handlers;
        }
        catch (ArgumentException exception)
        {
            throw new SkeletonKeyPluginLoadException("SKP2209", "A plugin node handler conflicts with a host handler.", exception);
        }
    }

    private IReadOnlyList<IWorkflowRuntimeResourceProvider> ComposeResourceProviders(SkeletonKeyPluginLoadResult plugins)
    {
        IReadOnlyList<IWorkflowRuntimeResourceProvider> providers = [new PlaywrightPageResourceProvider(), .. _hostResourceProviders, .. plugins.ResourceProviders];
        if (providers.GroupBy(static provider => provider.Kind, StringComparer.Ordinal).Any(static group => group.Count() > 1))
        {
            throw new SkeletonKeyPluginLoadException("SKP2210", "A plugin resource provider conflicts with a host provider.");
        }

        return providers;
    }

    private static ValueTask<SkeletonKeyPluginLoadResult> LoadPluginsAsync(RunnerOptions options, CancellationToken cancellationToken)
    {
        return SkeletonKeyPluginLoader.LoadAsync(options.PluginDirectories, cancellationToken);
    }

    private async ValueTask<int> PluginsAsync(RunnerOptions options, CancellationToken cancellationToken)
    {
        SkeletonKeyPluginLoadResult plugins = await LoadPluginsAsync(options, cancellationToken).ConfigureAwait(false);
        _ = Catalog(plugins);
        _ = ComposeHandlers(plugins);
        _ = ComposeResourceProviders(plugins);
        await WriteEnvelopeAsync(RunnerEnvelope.Success("plugins", new
        {
            count = plugins.Plugins.Count,
            plugins = plugins.Plugins.Select(static plugin => new
            {
                plugin.Id,
                plugin.Version,
                plugin.AssemblyFileName,
                plugin.EntryType,
                nodeDefinitions = plugin.NodeDefinitionCount,
                nodeHandlers = plugin.NodeHandlerCount,
                resourceProviders = plugin.ResourceProviderCount,
            }).ToArray(),
        }), cancellationToken).ConfigureAwait(false);
        return RunnerExitCodes.Success;
    }

    private async ValueTask WriteEnvelopeAsync(RunnerEnvelope envelope, CancellationToken cancellationToken)
    {
        object value = _outputFormat == RunnerOutputFormat.Ndjson ? new { type = "result", envelope } : envelope;
        await WriteNdjsonRecordAsync(value, cancellationToken).ConfigureAwait(false);
        await WriteDiagnosticAsync("command-complete", envelope.Status).ConfigureAwait(false);
    }

    private async ValueTask WriteNdjsonRecordAsync(object value, CancellationToken cancellationToken)
    {
        JsonNode? node = JsonSerializer.SerializeToNode(value, value.GetType(), _jsonOptions);
        RedactSensitiveValues(node);
        await _output.WriteLineAsync((node?.ToJsonString(_jsonOptions) ?? "null").AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    private static void RedactSensitiveValues(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (string key in obj.Select(static pair => pair.Key).ToArray())
            {
                if (IsSensitiveKey(key))
                {
                    obj[key] = "[REDACTED]";
                }
                else
                {
                    RedactSensitiveValues(obj[key]);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? item in array)
            {
                RedactSensitiveValues(item);
            }
        }
    }

    private static bool IsSensitiveKey(string key)
    {
        return key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("token", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("authorization", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("cookie", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("storageState", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("promptText", StringComparison.OrdinalIgnoreCase);
    }

    private async ValueTask WriteDiagnosticAsync(string name, string value)
    {
        if (_diagnostics)
        {
            await _error.WriteLineAsync($"[{name}] {SanitizeDiagnostic(value)}").ConfigureAwait(false);
        }
    }

    private static string SanitizeDiagnostic(string value)
    {
        string singleLine = value.Replace('\r', ' ').Replace('\n', ' ');
        return singleLine.Length <= 256 ? singleLine : singleLine[..256];
    }

    private async ValueTask<int> UnknownCommandAsync(string command)
    {
        await _error.WriteLineAsync("Unknown command: " + command).ConfigureAwait(false);
        await WriteUsageAsync().ConfigureAwait(false);
        return RunnerExitCodes.Usage;
    }

    private async ValueTask WriteUsageAsync()
    {
        await _output.WriteLineAsync("skeletonkey <version|plugins|validate|analyze|plan|run|resume|install-browsers> [--file <workflow.json>|-] [--workflow-directory <path>] [--locator-directory <path>] [--plugin-directory <path>] [--inputs <json>] [--inputs-file <inputs.json>] [--execution-id <id>] [--checkpoint-directory <path>] [--browser <name>] [--format <json|ndjson>] [--diagnostics]").ConfigureAwait(false);
    }

    private static bool IsHelp(string value)
    {
        return value is "-h" or "--help" or "help";
    }
}

/// <summary>Stable runner exit codes.</summary>
public static class RunnerExitCodes
{
    /// <summary>Command succeeded.</summary>
    public const int Success = 0;

    /// <summary>Command completed but rejected the workflow or action.</summary>
    public const int Failed = 1;

    /// <summary>Invalid command-line usage.</summary>
    public const int Usage = 2;

    /// <summary>Unhandled command exception.</summary>
    public const int Exception = 3;

    /// <summary>Command was cancelled.</summary>
    public const int Cancelled = 130;
}

internal sealed class RunnerUsageException(string message) : Exception(message);

internal sealed class RunnerOptions
{
    public string? WorkflowPath { get; private init; }

    public string? WorkflowDirectory { get; private init; }

    public string? LocatorDirectory { get; private init; }

    public string? InputsJson { get; private init; }

    public string? InputsPath { get; private init; }

    public string? ExecutionId { get; private init; }

    public string? CheckpointDirectory { get; private init; }

    public string? Browser { get; private init; }

    public IReadOnlyList<string> PluginDirectories { get; private init; } = Array.AsReadOnly(Array.Empty<string>());

    public RunnerOutputFormat OutputFormat { get; private init; }

    public bool Diagnostics { get; private init; }

    public static RunnerOptions Parse(IReadOnlyList<string> args)
    {
        string? workflowPath = null;
        string? workflowDirectory = null;
        string? locatorDirectory = null;
        string? inputsJson = null;
        string? inputsPath = null;
        string? executionId = null;
        string? checkpointDirectory = null;
        string? browser = null;
        List<string> pluginDirectories = [];
        RunnerOutputFormat outputFormat = RunnerOutputFormat.Json;
        bool diagnostics = false;

        for (int index = 0; index < args.Count; index++)
        {
            string item = args[index];
            string Next()
            {
                if (++index >= args.Count)
                {
                    throw new RunnerUsageException("Missing value for " + item + ".");
                }

                return args[index];
            }

            switch (item)
            {
                case "-f":
                case "--file":
                    workflowPath = Next();
                    break;
                case "--inputs":
                    inputsJson = Next();
                    break;
                case "--workflow-directory":
                    workflowDirectory = Next();
                    break;
                case "--locator-directory":
                    locatorDirectory = Next();
                    break;
                case "--inputs-file":
                    inputsPath = Next();
                    break;
                case "--execution-id":
                    executionId = Next();
                    break;
                case "--checkpoint-directory":
                    checkpointDirectory = Next();
                    break;
                case "--browser":
                    browser = Next();
                    break;
                case "--plugin-directory":
                    pluginDirectories.Add(Next());
                    if (pluginDirectories.Count > 8)
                    {
                        throw new RunnerUsageException("At most 8 plugin directories may be supplied.");
                    }

                    break;
                case "--format":
                    outputFormat = Next() switch
                    {
                        "json" => RunnerOutputFormat.Json,
                        "ndjson" => RunnerOutputFormat.Ndjson,
                        _ => throw new RunnerUsageException("Format must be json or ndjson."),
                    };
                    break;
                case "--diagnostics":
                    diagnostics = true;
                    break;
                default:
                    if (workflowPath is null && !item.StartsWith("-", StringComparison.Ordinal))
                    {
                        workflowPath = item;
                        break;
                    }

                    throw new RunnerUsageException("Unknown option: " + item + ".");
            }
        }

        if (inputsJson is not null && inputsPath is not null)
        {
            throw new RunnerUsageException("Use either --inputs or --inputs-file, not both.");
        }

        return new RunnerOptions
        {
            WorkflowPath = workflowPath,
            WorkflowDirectory = workflowDirectory,
            LocatorDirectory = locatorDirectory,
            InputsJson = inputsJson,
            InputsPath = inputsPath,
            ExecutionId = executionId,
            CheckpointDirectory = checkpointDirectory,
            Browser = browser,
            PluginDirectories = pluginDirectories.AsReadOnly(),
            OutputFormat = outputFormat,
            Diagnostics = diagnostics,
        };
    }
}

/// <summary>Supported machine-readable output formats.</summary>
public enum RunnerOutputFormat
{
    /// <summary>One JSON command envelope.</summary>
    Json,

    /// <summary>One JSON record per line, including runtime events and the final result.</summary>
    Ndjson,
}

internal sealed class BufferedWorkflowEventSink : IWorkflowEventSink
{
    private readonly List<WorkflowEvent> _events = [];

    public IReadOnlyList<WorkflowEvent> Events => _events.AsReadOnly();

    public ValueTask PublishAsync(WorkflowEvent workflowEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _events.Add(workflowEvent ?? throw new ArgumentNullException(nameof(workflowEvent)));
        return ValueTask.CompletedTask;
    }
}

/// <summary>JSON runner response envelope.</summary>
public sealed record RunnerEnvelope(string Command, bool Accepted, string Status, object? Result = null, IReadOnlyList<RunnerIssue>? Issues = null)
{
    /// <summary>Creates a successful envelope.</summary>
    public static RunnerEnvelope Success(string command, object? result = null)
    {
        return new RunnerEnvelope(command, true, "succeeded", result, Array.AsReadOnly(Array.Empty<RunnerIssue>()));
    }

    /// <summary>Creates a failed envelope with one error issue.</summary>
    public static RunnerEnvelope Failure(string command, string message, string code)
    {
        return new RunnerEnvelope(command, false, "failed", null, [new RunnerIssue(code, "error", message, null, null)]);
    }

    /// <summary>Creates an envelope from semantic validation.</summary>
    public static RunnerEnvelope FromValidation(string command, WorkflowValidationResult result)
    {
        return new RunnerEnvelope(command, result.IsValid, result.IsValid ? "valid" : "invalid", new { errors = result.Errors.Count, warnings = result.Warnings.Count }, result.Issues.Select(static issue => new RunnerIssue(issue.Code, issue.Severity.ToString(), issue.Message, issue.Path, null)).ToArray());
    }

    /// <summary>Creates an envelope from catalog-aware analysis.</summary>
    public static RunnerEnvelope FromAnalysis(string command, WorkflowAnalysisResult result)
    {
        return new RunnerEnvelope(command, result.CanPlanExecution, result.CanPlanExecution ? "ready" : "blocked", new { errors = result.Errors.Count(), warnings = result.Warnings.Count(), nodes = result.Nodes.Count, connections = result.Connections.Count }, result.Issues.Select(static issue => new RunnerIssue(issue.Code, issue.Severity.ToString(), issue.Message, issue.Path, issue.NodeId)).ToArray());
    }

    /// <summary>Creates an envelope from cross-workflow invocation analysis.</summary>
    public static RunnerEnvelope FromInvocationAnalysis(string command, WorkflowInvocationAnalysisResult result)
    {
        return new RunnerEnvelope(
            command,
            result.IsValid,
            result.IsValid ? "ready" : "blocked",
            new { dependencies = result.Dependencies.Count, errors = result.Issues.Count },
            result.Issues.Select(static issue => new RunnerIssue(issue.Code, "Error", issue.Message, issue.Path, issue.NodeId)).ToArray());
    }

    /// <summary>Creates an envelope from execution planning.</summary>
    public static RunnerEnvelope FromPlan(string command, WorkflowExecutionPlanResult result)
    {
        object? payload = result.Plan is null ? null : new { result.Plan.PlanId, steps = result.Plan.Steps.Count, dependencies = result.Plan.Dependencies.Count, resources = result.Plan.Resources.Count };
        return new RunnerEnvelope(command, result.IsReady, result.IsReady ? "ready" : result.Status.ToString(), payload, result.Issues.Select(static issue => new RunnerIssue(issue.Code, issue.Severity.ToString(), issue.Message, issue.Path, issue.NodeId)).ToArray());
    }

    /// <summary>Creates an envelope from runtime execution.</summary>
    public static RunnerEnvelope FromRuntime(string command, bool accepted, WorkflowRuntimeResult result)
    {
        WorkflowExecutionResult execution = result.Result;
        return new RunnerEnvelope(command, accepted, execution.Status.ToString(), new
        {
            execution.ExecutionId,
            execution.WorkflowId,
            outcome = execution.Outcome?.Code,
            outputs = execution.Outputs,
            nodes = result.NodeResults.Count,
            error = execution.Error is null ? null : new { execution.Error.Code, execution.Error.Message, execution.Error.NodeId },
        }, Array.AsReadOnly(Array.Empty<RunnerIssue>()));
    }
}

/// <summary>One JSON runner diagnostic.</summary>
public sealed record RunnerIssue(string Code, string Severity, string Message, string? Path, string? NodeId);
