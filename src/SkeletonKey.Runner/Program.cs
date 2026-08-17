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
    return await new SkeletonKeyRunner(Console.In, Console.Out, Console.Error)
        .ExecuteAsync(args, shutdown.Token)
        .ConfigureAwait(false);
}
finally
{
    Console.CancelKeyPress -= cancelHandler;
}
