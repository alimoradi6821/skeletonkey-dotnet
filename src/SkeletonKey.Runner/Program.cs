using System.Text.Json;
using SkeletonKey.Desktop.FlaUI;
using SkeletonKey.Runner.Core;

using CancellationTokenSource shutdown = new();
ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

Console.CancelKeyPress += cancelHandler;
try
{
    if (args.Length > 0 && string.Equals(args[0], "export", StringComparison.Ordinal))
    {
        return await ExecuteStandaloneExportAsync(args.Skip(1).ToArray(), shutdown.Token).ConfigureAwait(false);
    }

    return await new SkeletonKeyRunner(
        Console.In,
        Console.Out,
        Console.Error,
        [new FlaUiApplicationResourceProvider()])
        .ExecuteAsync(args, shutdown.Token)
        .ConfigureAwait(false);
}
finally
{
    Console.CancelKeyPress -= cancelHandler;
}

static async ValueTask<int> ExecuteStandaloneExportAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
{
    try
    {
        StandaloneExportResult result = await new StandaloneExporter().ExportAsync(args, cancellationToken).ConfigureAwait(false);
        await WriteEnvelopeAsync(RunnerEnvelope.Success("export", result)).ConfigureAwait(false);
        return RunnerExitCodes.Success;
    }
    catch (OperationCanceledException)
    {
        await WriteEnvelopeAsync(RunnerEnvelope.Failure("export", "Standalone export was cancelled.", "SKR1300")).ConfigureAwait(false);
        return RunnerExitCodes.Cancelled;
    }
    catch (StandaloneSettingsException exception)
    {
        await WriteEnvelopeAsync(RunnerEnvelope.Failure("export", exception.Message, exception.Code)).ConfigureAwait(false);
        return RunnerExitCodes.Failed;
    }
    catch (StandalonePackageException exception)
    {
        await WriteEnvelopeAsync(RunnerEnvelope.Failure("export", exception.Message, exception.Code)).ConfigureAwait(false);
        return RunnerExitCodes.Failed;
    }
    catch (StandaloneExportException exception)
    {
        await WriteEnvelopeAsync(RunnerEnvelope.Failure("export", exception.Message, exception.Code)).ConfigureAwait(false);
        return exception.Code.StartsWith("SKX302", StringComparison.Ordinal) ? RunnerExitCodes.Usage : RunnerExitCodes.Failed;
    }
    catch (Exception exception)
    {
        await WriteEnvelopeAsync(RunnerEnvelope.Failure("export", exception.Message, "SKX3999")).ConfigureAwait(false);
        return RunnerExitCodes.Exception;
    }
}

static async ValueTask WriteEnvelopeAsync(RunnerEnvelope envelope)
{
    JsonSerializerOptions options = new(JsonSerializerDefaults.Web) { WriteIndented = false };
    await Console.Out.WriteLineAsync(JsonSerializer.Serialize(envelope, options)).ConfigureAwait(false);
}
