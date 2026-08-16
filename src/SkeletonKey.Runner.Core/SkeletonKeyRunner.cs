using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Analysis;
using SkeletonKey.Analysis.Default;
using SkeletonKey.BuiltIns;
using SkeletonKey.BuiltIns.Runtime;
using SkeletonKey.Catalog;
using SkeletonKey.Handlers;
using SkeletonKey.Materialization;
using SkeletonKey.Planning;
using SkeletonKey.Planning.Default;
using SkeletonKey.Runtime;
using SkeletonKey.Runtime.Default;
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

    /// <summary>Initializes a runner facade over explicit streams.</summary>
    public SkeletonKeyRunner(TextReader input, TextWriter output, TextWriter error)
    {
        _input = input;
        _output = output;
        _error = error;
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
            return command switch
            {
                "version" => await VersionAsync(cancellationToken).ConfigureAwait(false),
                "validate" => await ValidateAsync(options, cancellationToken).ConfigureAwait(false),
                "analyze" => await AnalyzeAsync(options, cancellationToken).ConfigureAwait(false),
                "plan" => await PlanAsync(options, cancellationToken).ConfigureAwait(false),
                "run" => await RunAsync(options, cancellationToken).ConfigureAwait(false),
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
        catch (Exception exception)
        {
            await WriteEnvelopeAsync(RunnerEnvelope.Failure("error", exception.Message, "SKR1999"), cancellationToken: CancellationToken.None).ConfigureAwait(false);
            return RunnerExitCodes.Exception;
        }
    }

    private async ValueTask<int> VersionAsync(CancellationToken cancellationToken)
    {
        await WriteEnvelopeAsync(RunnerEnvelope.Success("version", new Dictionary<string, object?>
        {
            ["assemblyVersion"] = typeof(SkeletonKeyRunner).Assembly.GetName().Version?.ToString() ?? "0.0.0.0",
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
        WorkflowAnalysisResult analysis = new DefaultWorkflowAnalyzer().Analyze(workflow, Catalog());
        await WriteEnvelopeAsync(RunnerEnvelope.FromAnalysis("analyze", analysis), cancellationToken).ConfigureAwait(false);
        return analysis.CanPlanExecution ? RunnerExitCodes.Success : RunnerExitCodes.Failed;
    }

    private async ValueTask<int> PlanAsync(RunnerOptions options, CancellationToken cancellationToken)
    {
        WorkflowDocument workflow = await ReadWorkflowAsync(options, cancellationToken).ConfigureAwait(false);
        WorkflowAnalysisResult analysis = new DefaultWorkflowAnalyzer().Analyze(workflow, Catalog());
        if (!analysis.CanPlanExecution)
        {
            await WriteEnvelopeAsync(RunnerEnvelope.FromAnalysis("plan", analysis), cancellationToken).ConfigureAwait(false);
            return RunnerExitCodes.Failed;
        }

        WorkflowExecutionPlanResult planning = new DefaultWorkflowExecutionPlanner().Plan(workflow, analysis);
        await WriteEnvelopeAsync(RunnerEnvelope.FromPlan("plan", planning), cancellationToken).ConfigureAwait(false);
        return planning.IsReady ? RunnerExitCodes.Success : RunnerExitCodes.Failed;
    }

    private async ValueTask<int> RunAsync(RunnerOptions options, CancellationToken cancellationToken)
    {
        WorkflowDocument workflow = await ReadWorkflowAsync(options, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<string, JsonNode?> inputs = await ReadInputsAsync(options, cancellationToken).ConfigureAwait(false);
        WorkflowNodeDefinitionCatalog catalog = Catalog();
        IReadOnlyList<INodeHandler> handlers = [.. BuiltInRuntimeHandlers.Create(), .. WebBuiltInRuntimeHandlers.Create()];
        DefaultWorkflowRuntime runtime = new(
            new WorkflowSemanticValidator(),
            new DefaultWorkflowAnalyzer(),
            new DefaultWorkflowExecutionPlanner(),
            catalog,
            new ImmutableNodeHandlerResolver(handlers),
            new NodeParameterMaterializer(),
            options: new WorkflowRuntimeOptions(maximumExecutedNodeAttempts: 1000),
            resourceProviders: [new PlaywrightPageResourceProvider()]);

        string executionId = options.ExecutionId ?? "execution";
        WorkflowRuntimeResult result = await runtime.ExecuteAsync(new WorkflowExecutionRequest(workflow, executionId, "plan:" + workflow.Id, inputs), cancellationToken).ConfigureAwait(false);
        bool accepted = result.Result.Status == WorkflowExecutionStatus.Succeeded;
        await WriteEnvelopeAsync(RunnerEnvelope.FromRuntime("run", accepted, result), cancellationToken).ConfigureAwait(false);
        return accepted ? RunnerExitCodes.Success : RunnerExitCodes.Failed;
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

    private static WorkflowNodeDefinitionCatalog Catalog()
    {
        return new WorkflowNodeDefinitionCatalog([.. BuiltInWorkflowNodeCatalog.Catalog.Definitions, .. WebBuiltInWorkflowNodeCatalog.Catalog.Definitions]);
    }

    private async ValueTask WriteEnvelopeAsync(RunnerEnvelope envelope, CancellationToken cancellationToken)
    {
        await _output.WriteLineAsync(JsonSerializer.Serialize(envelope, _jsonOptions).AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<int> UnknownCommandAsync(string command)
    {
        await _error.WriteLineAsync("Unknown command: " + command).ConfigureAwait(false);
        await WriteUsageAsync().ConfigureAwait(false);
        return RunnerExitCodes.Usage;
    }

    private async ValueTask WriteUsageAsync()
    {
        await _output.WriteLineAsync("skeletonkey <version|validate|analyze|plan|run|install-browsers> [--file <workflow.json>|-] [--inputs <json>] [--inputs-file <inputs.json>] [--browser <name>]").ConfigureAwait(false);
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

    public string? InputsJson { get; private init; }

    public string? InputsPath { get; private init; }

    public string? ExecutionId { get; private init; }

    public string? Browser { get; private init; }

    public static RunnerOptions Parse(IReadOnlyList<string> args)
    {
        string? workflowPath = null;
        string? inputsJson = null;
        string? inputsPath = null;
        string? executionId = null;
        string? browser = null;

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
                case "--inputs-file":
                    inputsPath = Next();
                    break;
                case "--execution-id":
                    executionId = Next();
                    break;
                case "--browser":
                    browser = Next();
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
            InputsJson = inputsJson,
            InputsPath = inputsPath,
            ExecutionId = executionId,
            Browser = browser,
        };
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
